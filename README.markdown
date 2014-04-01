winsw: Windows service wrapper in less restrictive license
=========================

Why?
----
Now, I think the first question that people would ask is, why another, when there's [Java Service Wrapper project](http://wrapper.tanukisoftware.org/doc/english/download.jsp) already available. The main reason for writing my own was the license — Java Service Wrapper project is in GPL (so that they can sell their commercial version in a different license), and that made it difficult for [Jenkins](http://jenkins-ci.org/) (which is under the MIT license) to use it.

Functionality-wise, there's really not much that's worth noting; the problem of wrapping a process as a Windows service is so well defined that there aren't really any room for substantial innovation. You basically write a configuration file specifying how you'd like your process to be launched, and we provide programmatic means to install/uninstall/start/stop services. Another notable different is that winsw can host any executable, whereas Java Service Wrapper can only host Java apps. Whether you like this or not depends on your taste, so I wouldn't claim mine is better. It's just different.

As the name implies, this is for Windows only. Unix systems have their own conventions for daemons, so a good behaving Unix daemon should just be using launchd/upstart/SMF/etc, instead of custom service wrapper.

Download
--------
[Binaries are available here](http://repo.jenkins-ci.org/releases/com/sun/winsw/winsw/)

Usage
-----
You write the configuration file that defines your service. This is the one I use for Jenkins:

    <service>
      <id>jenkins</id>
      <name>Jenkins</name>
      <description>This service runs Jenkins continuous integration system.</description>
      <env name="JENKINS_HOME" value="%BASE%"/>
      <executable>java</executable>
      <arguments>-Xrs -Xmx256m -jar "%BASE%\jenkins.war" --httpPort=8080</arguments>
      <logmode>rotate</logmode>
    </service>

You'll rename `winsw.exe` into something like `jenkins.exe`, then you put this XML file as `jenkins.xml`. The executable locates the configuration file via this file name convention. You can then install the service like:

    jenkins.exe install

... and you can use the exit code from these processes to determine whether the operation was successful. There are other commands to perform other operations, like `uninstall`, `start`, `stop`, and so on.

Once the service is installed, you can start it from Windows service manager. Windows will start `jenkins.exe`, then `jenkins.exe` will launch the executable specified in the configuration file (Java in this case.) If this process dies, winsw will exit itself, and the service will be considered stopped.

Available commands
------------------
Your renamed `winsw.exe` accepts the following commands:

* `install` to install the service to Windows Service Controller
* `uninstall` to uninstall the service. The opposite operation of above.
* `start` to start the service. The service must have already been installed.
* `stop` to stop the service.
* `restart` to restart the service. If the service is not currently running, this command acts like `start`.
* `status` to check the current status of the service. This command prints one line to the console. `NonExistent` to indicate the service is not currently installed, `Started` to indicate the service is currently running, and `Stopped` to indicate that the service is installed but not currently running.


Error reporting
---------------
Winsw uses WMI underneath, and as such it uses its error code as the exit code. See <a href="http://msdn.microsoft.com/en-us/library/aa389390(VS.85).aspx">MSDN article</a> for the complete list of exit code.

When winsw is running as a service, more detailed error information is reported to the Windows event log.

Deferred file operations
------------------------
To support self updating services, winsw offers a mechanism to perform file operations before the process you specified in the configuration file gets launched. This is often necessary because Windows prevents files from overwritten while it's in use.

To perform file operations, write a text file (in the UTF-8 encoding) at `myapp.copies` (that is, it's in the same directory as `myapp.xml` and `myapp.exe` but with a different file extension), and for each operation add one line:

* To move a file, write a line `src>dst`. If the `dst` file already exists it will be overwritten.

The success or failure of these operations will be recorded in the event log.

Note that it is apparently possible to [rename executables even when it's running](http://superuser.com/questions/488127/why-can-i-rename-a-running-executable-but-not-delete-it), which makes sense if you think about file handles.
I have failed to find any authoritative source of information about this, but experimentally this even works on Windows XP and presumably on all the later Windows versions. This behavior can be used to update `winsw.exe` itself.
Also see `WINSW_EXECUTABLE` environment variable.

The `<download>` element in the configuration file also provides an useful building block for a self updating services.

Restarting service from itself
------------------------------
To support self-restarting services, winsw exposes `WINSW_EXECUTABLE` environment variable into the forked process, which refers to the full path of `winsw.exe` that's managing the service.
To restart the service from within, execute `%WINSW_EXECUTABLE% restart!`. Note that you are invoking `restart!` command, not `restart` command. This hidden command is a flavor of the `restart` operation,
where winsw creates another winsw process in a separate process group, and restarts the service from there.

This additional indirection is necessary because Windows Service Control Manager (SCM) will kill child processes recursively when it stops a service. SCM doesn't provide the restart operation
as an atomic operation either, so winsw implements restart by a sequence of stop and start. The 2nd winsw process in a separate process group ensures that winsw can survive this massacre to
execute the start call.


Logging
-------
Winsw supports several different ways to capture stdout and stderr from the process you launch.

### Log directory
The `<logpath>` element specifies the directory in which the log files are created. If this element is absent, it'll default to the same directory where the configuration file resides.

### Append mode (default)
In this mode, `myapp.out.log` nad `myapp.err.log` (where `myapp` is the base name of the executable and the configuration file) are created and outputs are simply appended to these files. Note that the file can get quite big.

    <log mode="append"/>

### Reset mode
Works like the append mode, except that every time the service starts, the old log files are truncated.

    <log mode="reset"/>

### Ignore mode
Throw away stdout and stderr, and do not produce any log files at all.

    <log mode="none"/>

### Rotate mode
Works like the append mode, but in addition, if the log file gets bigger than a set size, it gets rotated to `myapp.1.out.log`, `myapp.2.out.log` and so on. The nested `<sizeThreshold>` element specifies the rotation threshold in KB (defaults to 10MB), and the nested `<keepFiles>` element specifies the number of rotated files to keep (defaults to 8.)

    <log mode="roll-by-size">
      <sizeThreshold>10240</sizeThreshold>
      <keepFiles>8</keepFiles>
    </log>

### Rotate by time mode
Works like the rotate mode, except that instead of using the size as a threshold, use the time period as the threshold.

This configuration must accompany a nested `<pattern>` element, which specifies the timestamp pattern used as the log file name.

    <log mode="roll-by-time">
      <pattern>yyyyMMdd</pattern>
    </log>

The syntax of the pattern string is specified by [DateTime.ToString()](http://msdn.microsoft.com/en-us/library/zdtaw1bw.aspx). For example, in the above example, the log of Jan 1, 2013 gets written to `myapp.20130101.out.log` and `myapp.20130101.err.log`. 


Offline Environment and Authenticode
------------------------------------
To work with UAC-enabled Windows, winsw ships with a digital signature. This causes Windows to automatically verify this digital signature when the application is launched (see [more discussions](http://msdn.microsoft.com/en-us/library/bb629393.aspx)). This adds some delay to the launch of the service, and more importantly, it prevents winsw from running in a server that has no internet connection. This is because a part of the signature verification involves checking certificate revocation list.

To prevent this problem, create `myapp.exe.config` in the same directory as `myapp.exe` (renamed `winsw.exe`) and put the following in it:

    <configuration>
      <runtime>
        <generatePublisherEvidence enabled="false"/> 
      </runtime>
    </configuration>

See [KB 936707](http://support.microsoft.com/kb/936707) for more details.

.NET runtime 4.0+
-----------------
Newer versions of Windows (confirmed on Windows Server 2012, possibly with Windows 8, too) do not ship with .NET runtime 2.0, which is what `winsw.exe` is built against. This is because unlike Java, where a newer runtime can host apps developed against earlier runtime, .NET apps need version specific runtimes.

One way to deal with this is to ensure that .NET 2.0 runtime is installed through your installer, but another way is to declare that `winsw.exe` can be hosted on .NET 4.0 runtime by creating an app config file `winsw.exe.config`.

    <configuration>
      <startup>
        <supportedRuntime version="v2.0.50727" />
        <supportedRuntime version="v4.0" />
      </startup>
    </configuration>

The way the runtime finds this file is by naming convention, so don't forget to rename a file based on your actual executable name. See [this post](http://www.davidmoore.info/2010/12/17/running-net-2-runtime-applications-under-the-net-4-runtime/) for more about this. To our knowledge, none of the other flags are needed.

Environment Variable Expansion in Configuration File
----------------------------------------------------
Configuration XML files can include environment variable expansions of the form `%Name%`. Such occurences, if found, will be automatically replaced by the actual values of the variables. If an undefined environment variable is referenced, no substituion occurs.

Configuration File Syntax
-------------------------
The behaviour of the service is controlled by the XML configuration file. The root element of this XML file must be `<service>`, and it supports the following child element.

### id
Specifies the ID that Windows uses internally to identify the service. This has to be unique among all the services installed in a system, and (while I haven't verified this) this must consist entirely out of alpha-numeric characters.

### name
Short display name of the service, which can contain spaces and other characters. This shouldn't be too long, like `<id>`, and this also needs to be unique among all the services in a given system.

### description
Long human-readable description of the service. This gets displayed in Windows service manager when the service is selected.

### executable
This element specifies the executable to be launched. It can be either absolute path, or you can just specify the executable name and let it be searched from `PATH` (although note that the services often run in a different user account and therefore it might have different `PATH` than your shell does.)

### depend
Specify IDs of other services that this service depends on. When service X depends on service Y, X can only run if Y is running.

Multiple elements can be used to specify multiple dependencies.

    <depend>Eventlog</depend>
    <depend>W32Time</depend>

### logging

Optionally set a different logging directory with <logpath> and startup <logmode>: reset (clear log), roll (move to \*.old) or append (default).

### argument
This element specifies the arguments to be passed to the executable. Winsw will quote each argument if necessary, so do not put quotes in `<argument>` to avoid double quotation.

    <argument>arg1</argument>
    <argument>arg2</argument>
    <argument>arg3</argument>

For backward compatibility, `<arguments>` element can be used instead to specify the whole command line in a single element.

### stopargument/stopexecutable
When the service is requested to stop, winsw simply calls <a href="http://msdn.microsoft.com/en-us/library/windows/desktop/ms686714(v=vs.85).aspx">TerminateProcess</a> API to kill the service instantly. However, if `<stopargument>` elements are present, winsw will instead launch another process of `<executable>` (or `<stopexecutable>` if that's specified) with the `<stopargument>` arguments, and expects that to initiate the graceful shutdown of the service process.

Winsw will then wait for the two processes to exit on its own, before reporting back to Windows that the service has terminated.

When you use the `<stopargument>`, you must use `<startargument>` instead of `<argument>`. See the complete example below:

    <executable>catalina.sh</executable>
    <startargument>jpda</startargument>
    <startargument>run</startargument>
    
    <stopexecutable>catalina.sh</stopexecutable>
    <stopargument>stop</stopargument>

Note that the name of the element is `startargument` and not `startarguments`. As such, to specify multiple arguments, you'll specify multiple elements.

### stoptimeout
When the service is requested to stop, winsw first attempts to <a href="http://msdn.microsoft.com/en-us/library/windows/desktop/ms683155(v=vs.85).aspx">send Ctrl+C signal to the process</a>, then wait for up to 15 seconds for the process to exit by itself gracefully. A process failing to do that (or if the process does not have a console), then winsw resorts to calling <a href="http://msdn.microsoft.com/en-us/library/windows/desktop/ms686714(v=vs.85).aspx">TerminateProcess</a> API to kill the service instantly.

This optional element allows you to change this "15 seconds" value, so that you can control how long winsw gives the service to shut itself down. See `<onfailure>` below for how to specify time duration:

    <stoptimeout>10sec</stoptimeout>

### env
This optional element can be specified multiple times if necessary to specify environment variables to be set for the child process. The syntax is:

    <env name="HOME" value="c:\abc" />

### interactive
If this optional element is specified, the service will be allowed to interact with the desktop, such as by showing a new window and dialog boxes. If your program requires GUI, set this like the following:

    <interactive />

Note that since the introduction UAC (Windows Vista and onward), services are no longer really allowed to interact with the desktop. In those OSes, all that this does is to allow the user to switch to a separate window station to interact with the service.

### beeponshutdown
This optional element is to emit [simple tone](http://msdn.microsoft.com/en-us/library/ms679277%28VS.85%29.aspx) when the service shuts down. This feature should be used only for debugging, as some operating systems and hardware do not support this functionality.

### download
This optional element can be specified multiple times to have the service wrapper retrieve resources from URL and place it locally as a file. This operation runs when the service is started, before the application specified by `<executable>` is launched.

    <download from="http://example.com/some.dat" to="%BASE%\some.dat"/>

This is another useful building block for developing a self-updating service.

### log
See the "Logging" section above for more details.

### workingdirectory
This optional element sets the current directory of the process launched by winsw.

    <workingdirectory>%SystemDrive%\</workingdirectory>

### onfailure
This optional repeatable element controls the behaviour when the process launched by winsw fails (i.e., exits with non-zero exit code).

    <onfailure action="restart" delay="10 sec"/>
    <onfailure action="restart" delay="20 sec"/>
    <onfailure action="reboot" />

For example, the above configuration causes the service to restart in 10 seconds after the first failure, restart in 20 seconds after the second failure, then Windows will reboot if the service fails one more time.

Each element contains a mandatory `action` attribute, which controls what Windows SCM will do, and optional `delay` attribute, which controls the delay until the action is taken. The legal values for action are:

* `restart`: restart the service
* `reboot`: reboot Windows
* `none`: do nothing and leave the service stopped

The possible suffix for the delay attribute is sec/secs/min/mins/hour/hours/day/days. If missing, the delay attribute defaults to 0.

If the service keeps failing and it goes beyond the number of `<onfailure>` configured, the last action will be repeated. Therefore, if you just want to always restart the service automatically, simply specify one `<onfailure>` element like this:

    <onfailure action="restart" />

### resetfailure
This optional element controls the timing in which Windows SCM resets the failure count. For example, if you specify `<resetfailure>1 hour</resetfailure>` and your service continues to run longer than one hour, then the failure count is reset to zero. This affects the behaviour of the failure actions (see `<onfailure>` above).

In other words, this is the duration in which you consider the service has been running successfully. Defaults to 1 day.

### Service account
It is possible to specify the useraccount (and password) that the service will run as. To do this, specify a `<serviceaccount>` element like this:

    <serviceaccount>
       <domain>YOURDOMAIN</domain>
       <user>useraccount</user>
       <password>Pa55w0rd</password>
    </serviceaccount>

### Working directory
Some services need to run with a working directory specified. To do this, specify a `<workingdirectory>` element like this:

    <workingdirectory>C:\application</workingdirectory>

### priority
Optionally specify the scheduling priority of the service process (equivalent of Unix nice)
Possible values are `idle`, `belownormal`, `normal`, `abovenormal`, `high`, `realtime` (case insensitive.)

    <priority>idle</priority>

Specifying a priority higher than normal has unintended consequences. See <a href="http://msdn.microsoft.com/en-us/library/system.diagnostics.processpriorityclass(v=vs.110).aspx">MSDN discussion</a> for details. This feature is intended primarily to launch a process in a lower priority so as not to interfere with the computer's interactive usage.
