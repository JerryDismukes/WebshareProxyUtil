# WebshareProxyUtil

WebshareProxyUtil is an alpha Windows tray utility for Webshare proxy IP authorization and basic internet connectivity monitoring.

It can authorize the current public IP with Webshare, list authorized IPs, list proxies/replacements, monitor internet connectivity against configurable targets, and optionally run a headless boot-time scheduled task before user logon.

> **Alpha status:** This project is sample/alpha code. It is not production-ready and is provided as-is.

## Features

- Single-file Windows executable publish target.
- Tray-based GUI with first-run setup prompt.
- Webshare API key setup through the GUI.
- Authorized IP list, add, and remove actions.
- Proxy list and replacement views.
- Internet connectivity monitoring with editable IP/site targets.
- Debug logging for detailed checks.
- Low-frequency Info heartbeat logging so the log proves the utility is still running without filling up every few seconds.
- Runtime config shared between GUI and headless mode.
- Optional boot-time scheduled task that runs as `SYSTEM` using `--headless`.

## Webshare API key setup

WebshareProxyUtil requires a Webshare API key before it can list proxies, manage authorized IPs, or automatically authorize the current public IP.

To get your API key:

1. Sign in to your Webshare account.
2. Open the Webshare dashboard.
3. Go to the API / Developer / API Keys area.
4. Copy your API key.
5. Run `WebshareProxyUtil.exe`.
6. Open **Settings**.
7. Paste the API key into the **Webshare API Key** field.
8. Click **Save Settings**.

The utility stores the API key in Windows Credential Manager for the interactive user. It also stores a DPAPI LocalMachine-encrypted copy in the shared config file so the optional boot-time `SYSTEM` scheduled task can run before user logon.

The encrypted config value should look like this:

```json
"apiKey": "dpapi-localmachine:..."
```

Do not commit a real API key or a machine-specific runtime config file to GitHub.

## Runtime paths

- Shared config: `%ProgramData%\WebshareProxyUtil\MonitorConfig.json`
- Persistent log: `[folder containing WebshareProxyUtil.exe]\Logs\monitor.log`

The config is stored under ProgramData so the interactive GUI and boot-time SYSTEM scheduled task can both read the same settings. The log is intentionally written beside the published EXE, under `Logs`, using the actual launched process path rather than the .NET single-file extraction folder.

## GUI settings

Open the tray icon and choose **Open Manager** or **Settings**. The Settings tab includes:

- Webshare enabled/disabled.
- Webshare API key entry/change.
- Debug logging.
- Log size and rotation count.
- Internet check interval.
- Webshare public IP check interval.
- Max consecutive failures.
- Heartbeat log interval.
- Internet detection targets/IPs/sites, one per line.
- Startup task install/remove/check buttons.
- Config and log path shortcuts.
- Option to start minimized to tray when launched interactively.

## Logging behavior

The GUI log pane shows current session activity by default.

Use **Load File Log** to view the full persistent file log, or **Reset Log File** to clear the persistent log if it becomes cluttered.

Connectivity checks can run frequently, for example every 5 seconds. Those detailed checks are Debug-level. The persistent file log also writes a lower-frequency Info heartbeat based on the configured heartbeat interval, defaulting to 60 minutes.

## Headless startup mode

The GUI can install a boot-time scheduled task named `WebshareProxyUtil` that runs:

```text
WebshareProxyUtil.exe --headless
```

The task is created as `SYSTEM` with an `ONSTART` trigger, so it can run at machine startup before interactive user logon.

Installing, removing, or checking the startup task may require elevation. The GUI prompts to relaunch elevated when Windows denies access.

Command-line options:

```text
WebshareProxyUtil.exe --headless
WebshareProxyUtil.exe --install-startup-task
WebshareProxyUtil.exe --uninstall-startup-task
WebshareProxyUtil.exe --check-startup-task
```

## Build requirements

- Windows 10/11 or Windows Server.
- Visual Studio 2022 or later.
- .NET 8 SDK.
- Windows Forms desktop workload.

## Build from Visual Studio

1. Open `WebshareProxyUtil.sln`.
2. Right-click **WebshareProxyUtil**.
3. Select **Set as Startup Project**.
4. Build or publish the solution.

The executable project is under `IPMonitorApp\IPMonitorApp.csproj`, but the assembly name is `WebshareProxyUtil`, so the published executable is `WebshareProxyUtil.exe`.

## Publish from command line

From the repository root:

```powershell
dotnet publish .\IPMonitorApp\IPMonitorApp.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

The project file already enables the single-file publish settings.

## Repository hygiene

Before pushing to GitHub:

- Do not include `bin/`, `obj/`, `.vs/`, publish output, logs, or local runtime config.
- Do not include real API keys.
- Do not include screenshots showing secrets.
- Review `NOTICE.txt`, `SECURITY.md`, and `LICENSE` before publishing.

## License

This repository currently includes an MIT license. Replace it before publishing if you want different terms.
