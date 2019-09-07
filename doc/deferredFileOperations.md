Deferred file operations
====

To support self updating services, winsw offers a mechanism to perform file operations before the process you specified in the configuration file gets launched. 
This is often necessary because Windows prevents a file from being overwritten while it's in use.

To perform file operations, write a text file (in the UTF-8 encoding) at `myapp.copies` 
  (that is, it's in the same directory as `myapp.xml` and `myapp.exe` but with a different file extension), 
  and for each operation add one line:

* To move a file, write a line `src>dst`. If the `dst` file already exists it will be overwritten.

The success or failure of these operations will be recorded in the event log.

`src` and `dst` should be the full path to file, or you will see failed to copy file.

example:

```
c:\soft\sshd.exe.new>c:\bin\ssh.exe
```
Note that it is apparently possible to [rename executables even when it's running](http://superuser.com/questions/488127/why-can-i-rename-a-running-executable-but-not-delete-it), which makes sense if you think about file handles.
Kohsuke has failed to find any authoritative source of information about this, but experimentally this even works on Windows XP and presumably on all the later Windows versions. 
This behavior can be used to update `winsw.exe` itself.
Also see `WINSW_EXECUTABLE` environment variable.

The `<download>` element in the configuration file also provides an useful building block for a self updating service.
