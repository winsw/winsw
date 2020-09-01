# YAML configuration file

This page describes the YAML configuration file, which controls the behavior of the Windows service.
[Find the YAML configuration support project demo here.](https://youtu.be/G05unV7aDrg)

You can find configuration file samples in the [examples](../examples) directory of the source code repository.
Actual samples are also being published as part of releases on GitHub and NuGet.

## File structure

YAML Configuration file should be in following format

Example:

```yaml
id: jenkins
name: Jenkins
description: This service runs Jenkins continuous integration system.
env:
    - name: JENKINS_HOME
      value: '%BASE%'
executable: java
arguments: >
    -Xrs
    -Xmx256m
    -jar "%BASE%\jenkins.war"
    --httpPort=8080
log:
    mode: roll
```

## YAML configuration schema validation

Users can validate YAML configurations file against JSON schema.
You can use YAML utility tool for VSCode to validate your
YAML configurations file with this JSON schema.
[Download YAML utility tool for VSCode from Visual Studio Marketplace.](https://marketplace.visualstudio.com/items?itemName=redhat.vscode-yaml)

## Environment variable expansion

Configuration YAML files can include environment variable expansions of the form `%Name%`. 
Such occurrences, if found, will be automatically replaced by the actual values of the variables. 

[Read more about Environment variable expansion](xmlConfigFile.md#environment-variable-expansion)

## Configuration entries

### id

Specifies the ID that Windows uses internally to identify the service.
This has to be unique among all the services installed in a system, and it should consist entirely out of alpha-numeric characters.

### name

Short display name of the service, which can contain spaces and other characters.
This shouldn't be too long, like `id`, and this also needs to be unique among all the services in a given system.

### description

Long human-readable description of the service.
This gets displayed in Windows service manager when the service is selected.

### executable

This element specifies the executable to be launched.
It can be either absolute path, or you can just specify the executable name and let it be searched from `PATH` (although note that the services often run in a different user account and therefore it might have different `PATH` than your shell does.)

### startmode

This element specifies the start mode of the Windows service.
It can be one of the following values: Boot, System, Automatic, or Manual.
For more information, see the [ChangeStartMode method](https://docs.microsoft.com/windows/win32/cimwin32prov/changestartmode-method-in-class-win32-service).
The default value is `Automatic`.

### delayedAutoStart

This Boolean option enables the delayed start mode if the `Automatic` start mode is defined. 

[Read more about delayedAutoStart](xmlConfigFile.md#delayedautostart)

```yaml
delayedAutoStart: false
```

### depend
Specify IDs of other services that this service depends on. 
When service `X` depends on service `Y`, `X` can only run if `Y` is running.

YAML list can be used to specify multiple dependencies.

```yaml
depend:
    - Eventlog
    - W32Time
```

### log

Optionally set a different logging directory with `logpath` and startup `mode`: append (default), reset (clear log), ignore, roll (move to `\*.old`).

User can specify all log configurations as a single YAML dictionary

```yaml
log:
    mode: roll-by-size
    logpath: '%BASE/log%'
    sizeThreshold: 10240
    keepFiles: 8
```

See the [Logging and error reporting](loggingAndErrorReporting.md) page for more info.

### Arguments

`arguments` element specifies the arguments to be passed to the executable. User can specify all the commands as a single line.

```yaml
arguments: arg1 arg2 arg3
```

Also user can specify the arguments in more structured way with YAML multiline strings.

```yaml
arguments: >
  arg1
  arg2
  arg3
```

### stoparguments/stopexecutable

~~When the service is requested to stop, winsw simply calls [TerminateProcess function](https://docs.microsoft.com/windows/win32/api/processthreadsapi/nf-processthreadsapi-terminateprocess) to kill the service instantly.~~
However, if `stoparguments` elements is present, winsw will instead launch another process of `executable` (or `stopexecutable` if that's specified) with the specified arguments, and expects that to initiate the graceful shutdown of the service process.

Winsw will then wait for the two processes to exit on its own, before reporting back to Windows that the service has terminated.

When you use the `stoparguments`, you must use `startarguments` instead of `arguments`. See the complete example below:

```yaml
executable: catalina.sh
startarguments: >
  jpda
  run
stopexecutable: catalina.sh
stoparguments: stop
```

### stoptimeout

This optional element allows you to change this "15 seconds" value, so that you can control how long winsw gives the service to shut itself down.

[Read more about stoptimeout](xmlConfigFile.md#stoptimeout)

See `onfailure` below for how to specify time duration:

```yaml
stoptimeout: 15 sec
```

### Environment

User can use list of YAML dictionaries, if necessary to specify environment variables to be set for the child process. The syntax is:

```yaml
env:
    -
        name: MY_TOOL_HOME
        value: 'C:\etc\tools\myTool'
    -
        name: LM_LICENSE_FILE
        value: host1;host2
```

### interactive

If this optional element is specified, the service will be allowed to interact with the desktop, such as by showing a new window and dialog boxes. 
If your program requires GUI, set this like the following:

```yaml
interactive: true
```

Note that since the introduction UAC (Windows Vista and onward), services are no longer really allowed to interact with the desktop. 
In those OSes, all that this does is to allow the user to switch to a separate window station to interact with the service.

### beeponshutdown

This optional element is to emit [simple tones](https://docs.microsoft.com/windows/win32/api/utilapiset/nf-utilapiset-beep) when the service shuts down. 
This feature should be used only for debugging, as some operating systems and hardware do not support this functionality.

### download

This optional element can be specified to have the service wrapper retrieve resources from URL and place it locally as a file.
This operation runs when the service is started, before the application specified by `executable` is launched.

[Read more about download](xmlConfigFile.md#download)

Examples:

```yaml
download:
    -
        from: "http://www.google.com/"
        to: '%BASE%\index.html'
    -
        from: "http://www.nosuchhostexists.com/"
        to: '%BASE%\dummy.html'
        failOnError: true
    -
        from: "http://example.com/some.dat"
        to: '%BASE%\some.dat'
        auth: basic
        unsecureAuth: true
        username: aUser
        password: aPa55w0rd
    -
        from: "https://example.com/some.dat"
        to: '%BASE%\some.dat'
        auth: basic
        username: aUser
        password: aPa55w0rd
    -
        from: "https://example.com/some.dat"
        to: '%BASE%\some.dat'
        auth: sspi
```

### onfailure

This optional element controls the behaviour when the process launched by winsw fails (i.e., exits with non-zero exit code).

```yaml
onFailure:
    -
        action: restart
        delay: 10 sec
    -
        action: restart
        delay: 20 sec
    -
        action: reboot
```

[Read more about onFailure](xmlConfigFile.md#onfailure)

### resetfailure

This optional element controls the timing in which Windows SCM resets the failure count. 
For example, if you specify `resetfailure: 1 hour` and your service continues to run longer than one hour, then the failure count is reset to zero. 
This affects the behavior of the failure actions (see `onfailure` above).

In other words, this is the duration in which you consider the service has been running successfully. 
Defaults to 1 day.


### Security descriptor

The security descriptor string for the service in SDDL form.

For more information, see [Security Descriptor Definition Language](https://docs.microsoft.com/windows/win32/secauthz/security-descriptor-definition-language).

```yaml
securtityDescriptor: 'D:(A;;DCSWRPDTRC;;;BA)(A;;DCSWRPDTRC;;;SY)S:NO\_ACCESS\_CONTROL'
```

### Service account

The service is installed as the [LocalSystem account](https://docs.microsoft.com/windows/win32/services/localsystem-account) by default. If your service does not need a high privilege level, consider using the [LocalService account](https://docs.microsoft.com/windows/win32/services/localservice-account), the [NetworkService account](https://docs.microsoft.com/windows/win32/services/networkservice-account) or a user account.

To use a user account, specify a `serviceaccount` element like this:

```yaml
serviceaccount:
  domain: YOURDOMAIN
  user: useraccount
  password: Pa55w0rd
  allowservicelogon: true
```

[Read more about Service account](xmlConfigFile.md#service-account)

### Working directory

Some services need to run with a working directory specified. 
To do this, specify a `workingdirectory` element like this:

```yaml
workingdirectory: 'C:\application'
```

### Priority

Optionally specify the scheduling priority of the service process (equivalent of Unix nice)
Possible values are `idle`, `belownormal`, `normal`, `abovenormal`, `high`, `realtime` (case insensitive.)

```yaml
priority: idle
```

Specifying a priority higher than normal has unintended consequences.
For more information, see [ProcessPriorityClass Enumeration](https://docs.microsoft.com/dotnet/api/system.diagnostics.processpriorityclass) in .NET docs.
This feature is intended primarily to launch a process in a lower priority so as not to interfere with the computer's interactive usage.

### Stop parent process first

Optionally specify the order of service shutdown. 
If `true`, the parent process is shutdown first. 
This is useful when the main process is a console, which can respond to Ctrl+C command and will gracefully shutdown child processes.

```yaml
stopparentprocessfirst: true
```
