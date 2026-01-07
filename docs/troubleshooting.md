# Troubleshooting

If your service fails to start, first check:

- The stdout/stderr logs configured via the [`log` element](logging-and-error-reporting.md#logging)
- The wrapper log (`<config-file-name>.wrapper.log`) in the log directory (respects `<logpath>`; defaults to the configuration file directory)
- The Windows Event Log (WinSW reports more details there when running as a service)

## The system cannot find the file specified.

This can happen for multiple reasons:

- The executable is not in the `PATH` of the account that runs the service.
  - Solution: use a full path in the configuration file, or add the executable directory to `PATH` for the service account (or the machine-wide `PATH`).
- The executable is a batch file (`.bat` / `.cmd`) and you omit the extension.
  - Solution: use the full path and include the extension.
  - Why: `Process.Start` with `UseShellExecute=false` cannot resolve batch files from `PATH` without the file extension.

## The executable is found in PowerShell/cmd but not by WinSW

This is usually the same root cause as “The system cannot find the file specified.”:

- Services often run under a different user account.
- The service environment can have a different `PATH` than your interactive shell.

Prefer full paths in your WinSW configuration when diagnosing startup issues.

## I can't access/do X while running through WinSW

- Make sure the account that runs the service has permission to access X.
- Keep in mind that Windows services have limitations (for example, UI interaction). Interactive services are not recommended by Microsoft and may not work reliably. If you need UI interaction, consider running the app in the background and using Task Scheduler to start it automatically.

## The service exits with error 1067

Error `1067` (“The process terminated unexpectedly”) usually means the wrapped process exited shortly after starting.

- Enable stdout/stderr logging via the `log` element and re-check the logs.
- Check the wrapper log (`<config-file-name>.wrapper.log`) for WinSW-side errors (config parsing, process start failures).
- Check the Windows Event Log for errors reported by WinSW.
- Run the executable manually under the same account and working directory as the service to reproduce the failure.

## Not working on Windows 7

WinSW 3 can run on Windows 7 if you have .NET Framework 4.6.1 (or later) installed.

See:
- Supported platforms in `README.md`
- [.NET Framework system requirements](https://docs.microsoft.com/dotnet/framework/get-started/system-requirements)

## See also

- [XML configuration file](xml-config-file.md)
- [Logging and error reporting](logging-and-error-reporting.md)
- [CLI commands](cli-commands.md)
