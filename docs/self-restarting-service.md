# Self-restarting Windows services

## Restart from the spawned process

To support self-restarting services, winsw exposes `WINSW_EXECUTABLE` environment variable into the forked process,
  which refers to the full path of *WinSW.exe* that's managing the service.
To restart the service from within, execute `%WINSW_EXECUTABLE% restart!`.
Note that you are invoking `restart!` command, not `restart` command.
This hidden command is a flavor of the `restart` operation,
  where winsw creates another winsw process in a separate process group,
  and restarts the service from there.

This additional indirection is necessary because WinSW will kill child processes recursively when it stops a service.
Windows Service Control Manager (SCM) doesn't provide the restart operation as an atomic operation either, so winsw implements restart by a sequence of stop and start.
The second winsw process in a separate process group ensures that winsw can survive this massacre to execute the start call.

The account the service runs as must be allowed to stop and start the service by the service's security descriptor
  (see `sc.exe sdshow` and `sc.exe sdset`). `restart!` does not need an elevated token or administrator rights.
`restart!` returns as soon as the second winsw process is started and does not wait for the restart.
If the stop or the start fails, for example because the service's security descriptor denies it,
  the error is not returned to the caller; it is written to the wrapper log.
If the account may stop the service but not start it, the service is left stopped.
