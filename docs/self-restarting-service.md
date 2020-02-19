# Self-restarting Windows services

## Restart from the spawned process

To support self-restarting services, winsw exposes `WINSW_EXECUTABLE` environment variable into the forked process, 
  which refers to the full path of *WinSW.exe* that's managing the service.
To restart the service from within, execute `%WINSW_EXECUTABLE% restart!`. 
Note that you are invoking `restart!` command, not `restart` command. 
This hidden command is a flavor of the `restart` operation, 
  where winsw creates another winsw process in a separate process group, 
  and restarts the service from there.

This additional indirection is necessary because Windows Service Control Manager (SCM) will kill child processes recursively when it stops a service. 
SCM doesn't provide the restart operation as an atomic operation either, so winsw implements restart by a sequence of stop and start. 
The second winsw process in a separate process group ensures that winsw can survive this massacre to execute the start call.
