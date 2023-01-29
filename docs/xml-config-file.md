# XML configuration file

This page describes the configuration file, which controls the behavior of the Windows service.

You can find configuration file samples in the [samples](../samples) directory of the source code repository.
Actual samples are being also published as a part of releases on GitHub and NuGet.

## File structure

The root element of this XML file must be `<service>`, and it supports the following child element.

Example:

```xml
<service>
  <id>jenkins</id>
  <name>Jenkins</name>
  <description>This service runs Jenkins continuous integration system.</description>
  <env name="JENKINS_HOME" value="%BASE%"/>
  <executable>java</executable>
  <arguments>-Xrs -Xmx256m -jar "%BASE%\jenkins.war" --httpPort=8080</arguments>
  <log mode="roll"></log>
</service>
```

## Environment variable expansion

Configuration XML files can include environment variable expansions of the form `%Name%`.
Such occurrences, if found, will be automatically replaced by the actual values of the variables.
If an undefined environment variable is referenced, no substitution occurs.

Also, the service wrapper sets the environment variable `BASE` by itself, which points to a directory that contains the renamed *WinSW.exe*.
This is useful to refer to other files in the same directory.
Since this is an environment variable by itself, this value can be also accessed from the child process launched from the service wrapper.

## Relative paths and the default working directory

Relative paths are resolved based on the [working directory](#working-directory).
The default working directory of the wrapper and it's child processes is the directory where the configuration file is located.

## Configuration entries

### id

**Required**
Specifies the ID that Windows uses internally to identify the service.
This has to be unique among all the services installed in a system,
  and it should consist entirely out of alpha-numeric characters.

### executable

**Required**
This element specifies the executable to be launched.
It can be either absolute path, or you can just specify the executable name and let it be searched from `PATH` (although note that the services often run in a different user account and therefore it might have different `PATH` than your shell does.)

### name

**Optional**
Short display name of the service, which can contain spaces and other characters.
This shouldn't be too long, like `<id>`, and this also needs to be unique among all the services in a given system.

### description

**Optional**
Long human-readable description of the service.
This gets displayed in Windows service manager when the service is selected.

### startmode

**Optional**
This element specifies the start mode of the Windows service.
It can be one of the following values: Automatic, or Manual.
For more information, see the [ChangeStartMode method](https://docs.microsoft.com/windows/win32/cimwin32prov/changestartmode-method-in-class-win32-service).
The default value is `Automatic`.

### delayedAutoStart

**Optional**
This Boolean option enables the delayed start mode if the `Automatic` start mode is defined.
For more information, see [Startup Processes and Delayed Automatic Start](https://techcommunity.microsoft.com/t5/ask-the-performance-team/ws2008-startup-processes-and-delayed-automatic-start/ba-p/372692).

Please note that this startup mode will not take affect on old Windows versions older than Windows 7 and Windows Server 2008.
Windows service installation may fail in such case.

```xml
<delayedAutoStart>true</delayedAutoStart>
```

### depend

**Optional**
Specify IDs of other services that this service depends on.
When service `X` depends on service `Y`, `X` can only run if `Y` is running.

Multiple elements can be used to specify multiple dependencies.

```xml
<depend>Eventlog</depend>
<depend>W32Time</depend>
```

### logging

**Optional**
Optionally set a different logging directory with `<logpath>` and startup `mode`: append (default), reset (clear log), ignore, roll (move to `\*.old`).

See the [Logging and error reporting](logging-and-error-reporting.md) page for more info.

### Arguments

**Optional**
The `<arguments>` element specifies the arguments to be passed to the executable.

```xml
<arguments>arg1 arg2 arg3</arguments>
```

-or-

```xml
<arguments>
  arg1
  arg2
  arg3
</arguments>
```

### stopargument/stopexecutable

**Optional**
~~When the service is requested to stop, winsw simply calls [TerminateProcess function](https://docs.microsoft.com/windows/win32/api/processthreadsapi/nf-processthreadsapi-terminateprocess) to kill the service instantly.~~
However, if the `<stoparguments>` element is present, winsw will instead launch another process of `<executable>` (or `<stopexecutable>` if that's specified) with the specified arguments, and expects that to initiate the graceful shutdown of the service process.

Winsw will then wait for the two processes to exit on its own, before reporting back to Windows that the service has terminated.

When you use the `<stoparguments>`, you must use `<startarguments>` instead of `<arguments>`. See the complete example below:

```xml
<executable>catalina.sh</executable>
<startarguments>jpda run</startarguments>

<stopexecutable>catalina.sh</stopexecutable>
<stoparguments>stop</stoparguments>
```

### Additional commands

```xml
<prestart>
  <executable></executable>
  <arguments></arguments>
  <stdoutPath></stdoutPath>
  <stderrPath></stderrPath>
</prestart>
```

The pre-start command is executed when the service is starting and before the main process is started.

```xml
<poststart>
  <!-- ... -->
</poststart>
```

The post-start command is executed when the service is starting and after the main process is started.

```xml
<prestop>
  <!-- ... -->
</prestop>
```

The pre-stop command is executed when the service is stopping and before the main process is stopped.

```xml
<poststop>
  <!-- ... -->
</poststop>
```

The post-stop command is executed when the service is stopping and after the main process is stopped.

`stdoutPath` specifies the path to redirect the standard output to.

`stderrPath` specifies the path to redirect the standard error output to.

Specify `NUL` in `stdoutPath` or `stderrPath` to dispose of the corresponding stream.

### Preshutdown

```xml
<preshutdown>false</preshutdown>
<preshutdownTimeout>3 min</preshutdown>
```

Gives the service more time to stop when the system is being shut down.

The system default preshutdown timeout is three minutes.

### stoptimeout

When the service is requested to stop, winsw first attempts to send a Ctrl+C signal to a console application, or post a close message to a Windows application,
  then wait for up to 15 seconds for the process to exit by itself gracefully.
If the timeout expires or the signal or message can't be sent,
  then winsw resorts to terminate the service instantly.

This optional element allows you to change this "15 seconds" value, so that you can control how long winsw gives the service to shut itself down.
See `<onfailure>` below for how to specify time duration:

```xml
<stoptimeout>10sec</stoptimeout>
```

### Environment

This optional element can be specified multiple times if necessary to specify environment variables to be set for the child process. The syntax is:

```xml
<env name="HOME" value="c:\abc" />
```

### interactive

If this optional element is specified, the service will be allowed to interact with the desktop, such as by showing a new window and dialog boxes.
If your program requires GUI, set this like the following:

```xml
<interactive>true</interactive>
```

Note that since the introduction UAC (Windows Vista and onward), services are no longer really allowed to interact with the desktop.
In those OSes, all that this does is to allow the user to switch to a separate window station to interact with the service.

### beeponshutdown

This optional element is to emit [simple tones](https://docs.microsoft.com/windows/win32/api/utilapiset/nf-utilapiset-beep) when the service shuts down.
This feature should be used only for debugging, as some operating systems and hardware do not support this functionality.

```xml
<beeponshutdown>true</beeponshutdown>
```

### download

This optional element can be specified multiple times to have the service wrapper retrieve resources from URL and place it locally as a file.
This operation runs when the service is started, before the application specified by `<executable>` is launched.

For servers requiring authentication some parameters must be specified depending on the type of authentication. Only the basic authentication requires additional sub-parameters. Supported authentication types are:

- `none`:  default, must not be specified
- `sspi`: Windows [Security Support Provider Interface](https://docs.microsoft.com/windows/win32/secauthn/sspi) including Kerberos, NTLM etc.
- `basic`: Basic authentication, sub-parameters:
  - `user="UserName"`
  - `password="Passw0rd"`
  - `unsecureAuth="true": default="false"`

The parameter `unsecureAuth` is only effective when the transfer protocol is HTTP - unencrypted data transfer. This is a security vulnerability because the credentials are send in clear text! For a SSPI authentication this is not relevant because the authentication tokens are encrypted.

For target servers using the HTTPS transfer protocol it is necessary, that the CA which issued the server certificate is trusted by the client. This is normally the situation when the server ist located in the Internet. When an organisation is using a self issued CA for the intranet this probably is not the case. In this case it is necessary to import the CA to the Certificate MMC of the Windows client. Have a look to the instructions on [Manage Trusted Root Certificates](https://docs.microsoft.com/previous-versions/windows/it-pro/windows-server-2008-R2-and-2008/cc754841(v=ws.11)). The self issued CA must be imported to the Trusted Root Certification Authorities for the computer.

By default, the `download` command does not fail the service startup if the operation fails (e.g. `from` is not available).
In order to force the download failure in such case, it is possible to specify the `failOnError` boolean attribute.

To specify a custom proxy use the parameter `proxy` with the following formats:

- With credentials: `http://USERNAME:PASSWORD@HOST:PORT/`.
- Without credentials: `http://HOST:PORT/`.

Examples:

```xml
<download from="http://example.com/some.dat" to="%BASE%\some.dat" />

<download from="http://example.com/some.dat" to="%BASE%\some.dat" failOnError="true"/>

<download from="http://example.com/some.dat" to="%BASE%\some.dat" proxy="http://192.168.1.5:80/"/>

<download from="https://example.com/some.dat" to="%BASE%\some.dat" auth="sspi" />

<download from="https://example.com/some.dat" to="%BASE%\some.dat" failOnError="true"
          auth="basic" user="aUser" password="aPassw0rd" />

<download from="http://example.com/some.dat" to="%BASE%\some.dat"
          proxy="http://aUser:aPassw0rd@192.168.1.5:80/"
          auth="basic" unsecureAuth="true"
          user="aUser" password="aPassw0rd" />
```

This is another useful building block for developing a self-updating service.

Since 2.7, if the destination file exists, WinSW will send its last write time in the `If-Modified-Since` header and skip downloading if `304 Not Modified` is received.

### log

See the "Logging" section above for more details.

### onfailure

This optional repeatable element controls the behaviour when the process launched by winsw fails (i.e., exits with non-zero exit code).

```xml
<onfailure action="restart" delay="10 sec"/>
<onfailure action="restart" delay="20 sec"/>
<onfailure action="reboot" />
```

For example, the above configuration causes the service to restart in 10 seconds after the first failure, restart in 20 seconds after the second failure, then Windows will reboot if the service fails one more time.

Each element contains a mandatory `action` attribute, which controls what Windows SCM will do, and optional `delay` attribute, which controls the delay until the action is taken.
The legal values for action are:

- `restart`: restart the service
- `reboot`: reboot Windows. A blue screen with the [CRITICAL_PROCESS_DIED](https://docs.microsoft.com/windows-hardware/drivers/debugger/bug-check-0xef--critical-process-died) bug check code will be displayed
- `none`: do nothing and leave the service stopped

The possible suffix for the delay attribute is sec/secs/min/mins/hour/hours/day/days. If missing, the delay attribute defaults to 0.

If the service keeps failing and it goes beyond the number of `<onfailure>` configured, the last action will be repeated.
Therefore, if you just want to always restart the service automatically, simply specify one `<onfailure>` element like this:

```xml
<onfailure action="restart" />
```

### resetfailure

This optional element controls the timing in which Windows SCM resets the failure count.
For example, if you specify `<resetfailure>1 hour</resetfailure>` and your service continues to run longer than one hour, then the failure count is reset to zero.
This affects the behaviour of the failure actions (see `<onfailure>` above).

In other words, this is the duration in which you consider the service has been running successfully.
Defaults to 1 day.

### Security descriptor

The security descriptor string for the service in SDDL form.

For more information, see [Security Descriptor Definition Language](https://docs.microsoft.com/windows/win32/secauthz/security-descriptor-definition-language).

```xml
<securityDescriptor></securityDescriptor>
```

### Service account

The service is installed as the [LocalSystem account](https://docs.microsoft.com/windows/win32/services/localsystem-account) by default. If your service does not need a high privilege level, consider using the [LocalService account](https://docs.microsoft.com/windows/win32/services/localservice-account), the [NetworkService account](https://docs.microsoft.com/windows/win32/services/networkservice-account) or a user account.

To use a user account, specify a `<serviceaccount>` element like this:

```xml
<serviceaccount>
  <username>DomainName\UserName</username>
  <password>Pa55w0rd</password>
  <allowservicelogon>true</allowservicelogon>
</serviceaccount>
```

The `<username>` is in the form `DomainName\UserName` or `UserName@DomainName`. If the account belongs to the built-in domain, you can specify `.\UserName`.

The `<allowservicelogon>` is optional.
If set to `true`, will automatically set the "Allow Log On As A Service" right to the listed account.

To use [Group Managed Service Accounts](https://docs.microsoft.com/windows-server/security/group-managed-service-accounts/group-managed-service-accounts-overview), append `$` to the account name and remove `<password>` element:

```xml
<serviceaccount>
  <username>DomainName\GmsaUserName$</username>
  <allowservicelogon>true</allowservicelogon>
</serviceaccount>
```

#### LocalSystem account

To explicitly use the [LocalSystem account](https://docs.microsoft.com/windows/win32/services/localsystem-account), specify the following:

```xml
<serviceaccount>
  <username>LocalSystem</username>
</serviceaccount>
```

Note that this account does not have a password, so any password provided is ignored.

#### LocalService account

To use the [LocalService account](https://docs.microsoft.com/windows/win32/services/localservice-account), specify the following:

```xml
<serviceaccount>
  <username>NT AUTHORITY\LocalService</username>
</serviceaccount>
```

Note that this account does not have a password, so any password provided is ignored.

#### NetworkService account

To use the [NetworkService account](https://docs.microsoft.com/windows/win32/services/networkservice-account), specify the following:

```xml
<serviceaccount>
  <username>NT AUTHORITY\NetworkService</username>
</serviceaccount>
```

Note that this account does not have a password, so any password provided is ignored.

#### `prompt`

Optional. Prompts for a user name and a password.

```xml
<serviceaccount>
  <prompt>dialog|console</prompt>
</serviceaccount>
```

- `dialog`

  Prompts using a dialog box.

- `console`

  Prompts at the console.

### Working directory

Some services need to run with a working directory specified.
To do this, specify a `<workingdirectory>` element like this:

```xml
<workingdirectory>C:\application</workingdirectory>
```

### Priority

Optionally specify the scheduling priority of the service process (equivalent of Unix nice)
Possible values are `idle`, `belownormal`, `normal`, `abovenormal`, `high`, `realtime` (case insensitive.)

```xml
<priority>idle</priority>
```

Specifying a priority higher than normal has unintended consequences.
For more information, see [ProcessPriorityClass Enumeration](https://docs.microsoft.com/dotnet/api/system.diagnostics.processpriorityclass) in .NET docs.
This feature is intended primarily to launch a process in a lower priority so as not to interfere with the computer's interactive usage.

### Auto refresh

```xml
<autoRefresh>true</autoRefresh>
```

Automatically refreshes the service properties when the service starts or the following commands are executed:

- [start](cli-commands.md#start-command)
- [stop](cli-commands.md#stop-command)
- [restart](cli-commands.md#restart-command)

The default value is `true`.

### `sharedDirectoryMapping`

By default Windows does not establish shared drive mapping for services even if it is configured in the Windows service profile.
And sometimes it is impossible to workaround it due to the domain policies.

This allows mapping external shared directories before starting up the executable.

```xml
<sharedDirectoryMapping>
  <map label="N:" uncpath="\\UNC" />
  <map label="M:" uncpath="\\UNC2" />
</sharedDirectoryMapping>
```
