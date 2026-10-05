using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ToolkitApp.Models;

namespace ToolkitApp.Services;

public enum OutputKind { Stdout, Stderr, Info }

public sealed class RunResult
{
    public int ExitCode { get; set; }
    public bool Cancelled { get; set; }
    public bool TimedOut { get; set; }
    /// <summary>Elevated console was opened and left running; no output/exit code available.</summary>
    public bool Launched { get; set; }
    public string? Error { get; set; }
    public TimeSpan Elapsed { get; set; }
}

/// <summary>Runs one command at a time, streaming output. No WPF dependencies.</summary>
public sealed class CommandRunner
{
    private static readonly Regex Ansi = new(@"\x1B\[[0-9;?]*[ -/]*[@-~]|\x1B\][^\a]*\a", RegexOptions.Compiled);
    private CancellationTokenSource? _cts;
    private Process? _proc;
    private StreamWriter? _stdin;

    public event Action<string, OutputKind>? Output;
    public bool IsRunning { get; private set; }

    private void Emit(string text, OutputKind kind) => Output?.Invoke(text, kind);

    private static Encoding Oem()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
        }
        catch { return new UTF8Encoding(false); }
    }

    public static string StripAnsi(string s) => Ansi.Replace(s, "");

    /// <summary>cmd cannot run multi-line commands; join lines with '&amp;'.</summary>
    public static string JoinLinesForCmd(string command) =>
        string.Join(" & ", command.Replace("\r", "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0));

    private static string PsQuote(string s) => "'" + s.Replace("'", "''") + "'";

    /// <summary>Splits "exe arg arg" honouring a leading quoted executable.</summary>
    public static (string File, string Args) SplitDirect(string command)
    {
        command = command.Trim();
        if (command.StartsWith('"'))
        {
            int end = command.IndexOf('"', 1);
            if (end > 0) return (command[1..end], command[(end + 1)..].Trim());
        }
        int sp = command.IndexOfAny(new[] { ' ', '\t' });
        return sp < 0 ? (command, "") : (command[..sp], command[(sp + 1)..].Trim());
    }

    /// <param name="elevated">Start via UAC (runas, shell-execute).</param>
    /// <param name="captureFile">Elevated + non-interactive: redirect all output into this file.</param>
    public static ProcessStartInfo BuildStartInfo(ToolItem tool, string command, bool elevated, string? captureFile)
    {
        string shell = tool.Shell;
        if (elevated && shell == "direct") shell = "cmd";      // shell-execute can't capture direct runs; cmd can
        bool keepOpen = elevated && captureFile == null;
        var psi = new ProcessStartInfo();

        if (!string.IsNullOrWhiteSpace(tool.WorkingDirectory) && Directory.Exists(tool.WorkingDirectory))
            psi.WorkingDirectory = tool.WorkingDirectory;

        string envPrefixCmd = "", envPrefixPs = "";
        if (elevated)   // shell-execute cannot pass an environment block, so inject variables into the command
        {
            foreach (var kv in tool.EnvVars)
            {
                envPrefixCmd += $"set \"{kv.Key}={kv.Value}\" & ";
                envPrefixPs += $"$env:{kv.Key}={PsQuote(kv.Value)}; ";
            }
        }

        switch (shell)
        {
            case "powershell":
            case "pwsh":
            {
                psi.FileName = shell == "pwsh" ? "pwsh.exe" : "powershell.exe";
                string body = envPrefixPs + command;
                if (captureFile != null) body = $"& {{ {body} }} *> {PsQuote(captureFile)}";
                string script = "$ProgressPreference='SilentlyContinue'; try{[Console]::OutputEncoding=[Text.Encoding]::UTF8}catch{}; " + body;
                string b64 = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
                psi.Arguments = $"-NoLogo -ExecutionPolicy Bypass {(keepOpen ? "-NoExit " : "")}-EncodedCommand {b64}";
                break;
            }
            case "wsl":
                psi.FileName = "wsl.exe";
                psi.ArgumentList.Add("--"); psi.ArgumentList.Add("bash"); psi.ArgumentList.Add("-lc"); psi.ArgumentList.Add(command);
                break;
            case "direct":
            {
                var (file, args) = SplitDirect(command);
                psi.FileName = file; psi.Arguments = args;
                break;
            }
            default:
            {
                psi.FileName = "cmd.exe";
                string inner = envPrefixCmd + JoinLinesForCmd(command);
                if (captureFile != null) inner = $"({inner}) > \"{captureFile}\" 2>&1";
                psi.Arguments = $"/d /s {(keepOpen ? "/k" : "/c")} \"{inner}\"";
                break;
            }
        }

        if (elevated)
        {
            psi.UseShellExecute = true;
            psi.Verb = "runas";
            psi.WindowStyle = captureFile != null ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal;
        }
        else
        {
            var enc = shell == "cmd" ? Oem() : new UTF8Encoding(false);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = psi.RedirectStandardError = psi.RedirectStandardInput = true;
            psi.StandardOutputEncoding = psi.StandardErrorEncoding = enc;
            psi.StandardInputEncoding = enc;
            foreach (var kv in tool.EnvVars) psi.Environment[kv.Key] = kv.Value;
        }
        return psi;
    }

    public async Task<RunResult> RunAsync(ToolItem tool, string command, bool admin)
    {
        if (IsRunning) throw new InvalidOperationException("A command is already running.");
        IsRunning = true;
        var result = new RunResult();
        var sw = Stopwatch.StartNew();
        string? capture = null;
        string shell = tool.Shell;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        if (shell == "wsl" && admin) { admin = false; Emit("Admin is not applicable to WSL; running normally.", OutputKind.Info); }
        if (admin && !tool.KeepWindowOpen) capture = Path.Combine(Path.GetTempPath(), $"toolkit-{Guid.NewGuid():N}.log");

        try
        {
            var psi = BuildStartInfo(tool, command, admin, capture);
            if (tool.TimeoutSeconds > 0) _cts.CancelAfter(TimeSpan.FromSeconds(tool.TimeoutSeconds));

            _proc = new Process { StartInfo = psi };
            if (!admin)
            {
                _proc.OutputDataReceived += (_, e) => { if (e.Data != null) Emit(StripAnsi(e.Data), OutputKind.Stdout); };
                _proc.ErrorDataReceived += (_, e) => { if (e.Data != null) Emit(StripAnsi(e.Data), OutputKind.Stderr); };
            }
            _proc.Start();
            if (!admin)
            {
                _stdin = _proc.StandardInput;
                _proc.BeginOutputReadLine();
                _proc.BeginErrorReadLine();
            }
            else if (capture == null)
            {
                Emit("Elevated console opened in a separate window.", OutputKind.Info);
                result.Launched = true;
                return result;
            }
            else Emit("Running elevated; output will appear when it finishes…", OutputKind.Info);

            try { await _proc.WaitForExitAsync(token); }
            catch (OperationCanceledException)
            {
                result.Cancelled = true;
                result.TimedOut = tool.TimeoutSeconds > 0 && sw.Elapsed >= TimeSpan.FromSeconds(tool.TimeoutSeconds) - TimeSpan.FromMilliseconds(500);
                KillTree();
                await Task.Run(() => _proc.WaitForExit(3000));
            }
            if (!result.Cancelled) result.ExitCode = _proc.ExitCode;

            if (capture != null && File.Exists(capture))
            {
                string text = File.ReadAllText(capture, shell == "cmd" ? Oem() : Encoding.UTF8);
                foreach (var line in text.Replace("\r", "").Split('\n')) Emit(StripAnsi(line), OutputKind.Stdout);
            }
        }
        catch (Win32Exception ex) when (admin && ex.NativeErrorCode == 1223)
        {
            result.Error = "Administrator permission was declined (UAC).";
        }
        catch (Win32Exception ex)
        {
            result.Error = $"Could not start '{tool.Shell}': {ex.Message}";
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
            ConfigService.Log("Run failed", ex);
        }
        finally
        {
            try { _stdin?.Dispose(); } catch { }
            _stdin = null;
            try { _proc?.Dispose(); } catch { }
            _proc = null;
            if (capture != null) try { File.Delete(capture); } catch { }
            _cts?.Dispose(); _cts = null;
            sw.Stop();
            result.Elapsed = sw.Elapsed;
            IsRunning = false;
        }
        return result;
    }

    public void SendInput(string line)
    {
        try { _stdin?.WriteLine(line); _stdin?.Flush(); } catch { }
    }

    public void Stop() { try { _cts?.Cancel(); } catch (ObjectDisposedException) { } }

    private void KillTree()
    {
        try { if (_proc != null && !_proc.HasExited) _proc.Kill(entireProcessTree: true); } catch { }
    }
}
