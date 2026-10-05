<div align="center">

<img src="icon.jpg" width="96" alt="Tool Kit logo"/>

# Tool Kit

**A modern, customizable WPF launcher for command-line tools.**

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![Windows](https://img.shields.io/badge/Platform-Windows-0078D6?style=for-the-badge&logo=windows&logoColor=white)
![Version](https://img.shields.io/badge/Version-0.2.0--beta-brightgreen?style=for-the-badge)
![License](https://img.shields.io/badge/License-MIT-yellow.svg?style=for-the-badge)

</div>

## Features

**Running tools**
- **CMD, PowerShell, PowerShell 7, WSL, or any executable** (`direct`) as the shell for each tool.
- **Live output** with stdout/stderr colouring, ANSI-code stripping, correct console encoding, exit code and elapsed time.
- **Stop button, per-tool timeout, and an input box** to answer prompts of running programs.
- **Parameters**: write `{{host}}` or `{{host:google.com}}` in a command and Tool Kit asks for the value on each run.
- **Administrator mode** (UAC): keep the elevated console open *or* capture its output back into the app.
- Working directory and environment variables per tool; multi-line commands.

**Organising tools**
- Categories, favorites (pinned on Home), search across name/command/category/description.
- Add / edit / duplicate / delete, **drag & drop** `.ps1 .bat .cmd .exe .py .js .jar .sh` or folders.
- Export / import your tools as JSON; a starter pack of useful Windows tools on first run.

**GitHub integration**
- Search with sort (stars, updated, forks) and language filter, paging, README preview.
- **Clone & add**: clones with `git clone --depth 1` into your tools folder and adds *Update*, *Open folder* and any runnable scripts as tools.
- Works **without a token**; add an optional token in Settings for higher rate limits.

**Customization (Settings)**
- Dark / Light theme, any accent colour, interface font size.
- Terminal font, size and colour schemes (Classic, Green phosphor, Solarized, Monokai, Light, or your own hex colours).
- Default shell, startup page, output line limit, timestamps, wrapping, auto-scroll, confirmations.
- Window size/position remembered.

## Keyboard shortcuts

| Keys | Action |
|---|---|
| `Ctrl+1…4` | Home / Run Tools / Installed Tools / GitHub |
| `Ctrl+Q` | GitHub search |
| `Ctrl+,` | Settings |
| `Ctrl+F` / `Ctrl+N` | Search tools / New tool |
| `F5` or `Ctrl+Enter` | Run |
| `Esc` | Stop running command, or clear search |
| `F2` / `Del` | Edit / delete selected tool |

## Install

Download `ToolKit.exe` from [Releases](https://github.com/rose2x/toolskit/releases). It is self-contained, so no .NET install is needed.

## Build from source

Requires Windows and the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
git clone https://github.com/rose2x/toolskit.git
cd toolskit
dotnet run                 # run in development
build.bat                  # or: single-file self-contained exe in .\build
```

Pushing a tag like `v0.2.0` makes the included GitHub Actions workflow build and attach `ToolKit.exe` to a release.

## Where your data lives

| What | Location |
|---|---|
| Settings + tools | `%AppData%\ToolKit\config.json` (a `.bak` of the previous save is kept) |
| Logs | `%AppData%\ToolKit\logs\` |
| Cloned repos | `%USERPROFILE%\ToolKit\Tools` (changeable in Settings) |

A tools file from v0.1 (`tools_config.json` next to the exe) is imported automatically on first start.
If `config.json` is ever corrupted it is moved aside as `config.corrupt-*.json` instead of being overwritten.

## Security notes

Tool Kit runs whatever commands you give it, so only add tools you trust. Specific protections: the GitHub token is never
hard-coded or sent unless you set one; clone URLs must be `https://github.com/...` with a safe character set; PowerShell commands
are passed encoded so quotes can't break out; exported files never contain your token.

## Project layout

```
Models/     ToolItem, AppConfig / AppSettings
Services/   ConfigService, CommandRunner, GithubService, ToolFactory, PlaceholderResolver, ReadmeLocator, ThemeService
Views/      Home, Tools (run), Installed (library), GitHub, Settings, About, editor + parameter dialogs
```

The `Services` classes (except `ThemeService`) have no WPF dependency.

## Contributing

Issues and PRs are welcome. Fork → branch → commit → pull request.

## License

MIT, see [LICENSE](LICENSE).
