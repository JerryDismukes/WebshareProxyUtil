# Security Policy

## Supported status

WebshareProxyUtil is currently alpha/sample code. Security fixes may be made as the project evolves, but there is no formal support SLA.

## Secrets and configuration

Do not commit real Webshare API keys or machine-specific runtime files.

The utility stores the API key in two places:

1. Windows Credential Manager for the interactive user.
2. `%ProgramData%\WebshareProxyUtil\MonitorConfig.json` as a DPAPI LocalMachine-protected value so the optional boot-time SYSTEM scheduled task can decrypt it on the same computer.

The config value should look like this after saving settings:

```json
"apiKey": "dpapi-localmachine:..."
```

If a raw API key appears in the config file, run the latest build, open Settings, and save the API key again so it is rewritten in encrypted form.

## Reporting issues

For an alpha public repository, use GitHub Issues for non-sensitive bugs. Do not post API keys, logs containing secrets, screenshots with secrets, or exported runtime config files in public issues.
