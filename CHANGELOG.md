# Changelog

## 0.1.0-alpha

Initial alpha sample release.

### Added

- Windows Forms tray utility for Webshare proxy IP authorization.
- Authorized IP management.
- Proxy and replacement listing views.
- Connectivity monitoring using configurable IP/site targets.
- Periodic heartbeat logging for long-running sessions.
- First-run setup prompt.
- GUI settings for API key, intervals, debug logging, heartbeat interval, target list, and startup behavior.
- Persistent config in `%ProgramData%\WebshareProxyUtil\MonitorConfig.json`.
- Persistent logs beside the published executable under `Logs\monitor.log`.
- Optional boot-time scheduled task using `--headless`.
- DPAPI LocalMachine protection for the shared config API key copy.
