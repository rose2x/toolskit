# Changelog

## 0.2.0
- Rebuilt app: new theme system, settings page, tool editor, favorites, categories, search, parameters.
- Fixed: project did not build from a fresh clone (missing `icon.ico`); GitHub search always sent an invalid token.
- Fixed: quotes in commands broke execution; output encoding/ANSI garbage; config location depended on working directory;
  cancelling the details window still saved; no way to delete tools; admin runs closed instantly.
- New: stop, timeout, stdin, exit code, elapsed time, WSL/pwsh/direct shells, admin capture mode, GitHub clone-and-add with README preview, import/export, CI workflow.
