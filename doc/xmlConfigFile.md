# XML configuration file

This page describes the configuration file, which controls the behavior of the Windows service.

You can find configuration file samples in the [examples](../examples) directory of the source code repository.
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

## Configuration entries

### id

Specifies the ID that Windows uses internally to identify the service. 
This has to be unique among all the services installed in a system, 
  and it should consist entirely out of alpha-numeric characters.

### name

Short display name of the service, which can contain spaces and other characters.
This shouldn't be too long, like `<id>`, and this also needs to be unique among all the services in a given system.

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
For more information, see [Startup Processes and Delayed Automatic Start](https://techcommunity.microsoft.com/t5/ask-the-performance-team/ws2008-startup-processes-and-delayed-automatic-start/ba-p/372692).

Please note that this startup mode will not take affect on old Windows versions older than Windows 7 and Windows Server 2008.
Windows service installation may fail in such case.

```xml
<delayedAutoStart/>
```

### depend
Specify IDs of other services that this service depends on. 
When service `X` depends on service `Y`, `X` can only run if `Y` is running.

Multiple elements can be used to specify multiple dependencies.

```xml
<depend>Eventlog</depend>
<depend>W32Time</depend>
```

### logging

Optionally set a different logging directory with `<logpath>` and startup `mode`: append (default), reset (clear log), ignore, roll (move to `\*.old`).

See the [Logging and error reporting](loggingAndErrorReporting.md) page for more info.

### Arguments

`<argument>` element specifies the arguments to be passed to the executable. 
Winsw will quote each argument if necessary, so do not put quotes in `<argument>` to avoid double quotation.

```xml
<argument>arg1</argument>
<argument>arg2</argument>
<argument>arg3</argument>
```

`<arguments>` element can be used instead to specify the whole command line in a single element.

### stopargument/stopexecutable

~~When the service is requested to stop, winsw simply calls [TerminateProcess function](https://docs.microsoft.com/windows/win32/api/processthreadsapi/nf-processthreadsapi-terminateprocess) to kill the service instantly.~~
However, if `<stopargument>`/`<stoparguments>` elements are present, winsw will instead launch another process of `<executable>` (or `<stopexecutable>` if that's specified) with the specified arguments, and expects that to initiate the graceful shutdown of the service process.

Winsw will then wait for the two processes to exit on its own, before reporting back to Windows that the service has terminated.

When you use the `<stopargument>`/`<stoparguments>`, you must use `<startargument>`/`<startarguments>` instead of `<argument>`. See the complete example below:

```xml
<executable>catalina.sh</executable>
<startargument>jpda</startargument>
<startargument>run</startargument>

<stopexecutable>catalina.sh</stopexecutable>
<stopargument>stop</stopargument>
```

### stoptimeout

When the service is requested to stop, winsw first attempts to send a Ctrl+C signal, 
  then wait for up to 15 seconds for the process to exit by itself gracefully. 
A process failing to do that (or if the process does not have a console), 
  then winsw resorts to calling [TerminateProcess function](https://docs.microsoft.com/windows/win32/api/processthreadsapi/nf-processthreadsapi-terminateprocess) to kill the service instantly.

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
<interactive />
```

Note that since the introduction UAC (Windows Vista and onward), services are no longer really allowed to interact with the desktop. 
In those OSes, all that this does is to allow the user to switch to a separate window station to interact with the service.

### beeponshutdown

This optional element is to emit [simple tones](https://docs.microsoft.com/windows/win32/api/utilapiset/nf-utilapiset-beep) when the service shuts down. 
This feature should be used only for debugging, as some operating systems and hardware do not support this functionality.

### download

This optional element can be specified multiple times to have the service wrapper retrieve resources from URL and place it locally as a file.
This operation runs when the service is started, before the application specified by `<executable>` is launched.

For servers requiring authentication some parameters must be specified depending on the type of authentication. Only the basic authentication requires additional sub-parameters. Supported authentication types are:

* `none`:  default, must not be specified
* `sspi`: Windows [Security Support Provider Interface](https://docs.microsoft.com/windows/win32/secauthn/sspi) including Kerberos, NTLM etc. 
* `basic`: Basic authentication, sub-parameters:
	* `user="UserName"`
	* `password="Passw0rd"`
	* `unsecureAuth="true": default="false"`

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

Since v2.7, if the destination file exists, WinSW will send its last write time in the `If-Modified-Since` header and skip downloading if `304 Not Modified` is received.

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

* `restart`: restart the service
* `reboot`: reboot Windows
* `none`: do nothing and leave the service stopped

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
<securtityDescriptor></securtityDescriptor>
```

### Service account

The service is installed as the [LocalSystem account](https://docs.microsoft.com/windows/win32/services/localsystem-account) by default. If your service does not need a high privilege level, consider using the [LocalService account](https://docs.microsoft.com/windows/win32/services/localservice-account), the [NetworkService account](https://docs.microsoft.com/windows/win32/services/networkservice-account) or a user account.

To use a user account, specify a `<serviceaccount>` element like this:

```xml
<serviceaccount>
  <domain>YOURDOMAIN</domain>
  <user>useraccount</user>
  <password>Pa55w0rd</password>
  <allowservicelogon>true</allowservicelogon>
</serviceaccount>
```

The `<domain>` is optional and defaults to the local computer.

The `<allowservicelogon>` is optional. 
If set to `true`, will automatically set the "Allow Log On As A Service" right to the listed account.

To use [Group Managed Service Accounts](https://docs.microsoft.com/windows-server/security/group-managed-service-accounts/group-managed-service-accounts-overview), append `$` to the account name and remove `<password>` element:

```xml
<serviceaccount>
  <domain>YOURDOMAIN</domain>
  <user>gmsa_account$</user>
  <allowservicelogon>true</allowservicelogon>
</serviceaccount>
```

#### LocalSystem account

To explicitly use the [LocalSystem account](https://docs.microsoft.com/windows/win32/services/localsystem-account), specify the following:

```xml
<serviceaccount>
  <user>LocalSystem</user>
</serviceaccount>
```

Note that this account does not have a password, so any password provided is ignored.

#### LocalService account

To use the [LocalService account](https://docs.microsoft.com/windows/win32/services/localservice-account), specify the following:

```xml
<serviceaccount>
  <domain>NT AUTHORITY</domain>
  <user>LocalService</user>
</serviceaccount>
```

Note that this account does not have a password, so any password provided is ignored.

#### NetworkService account

To use the [NetworkService account](https://docs.microsoft.com/windows/win32/services/networkservice-account), specify the following:

```xml
<serviceaccount>
  <domain>NT AUTHORITY</domain>
  <user>NetworkService</user>
</serviceaccount>
```

Note that this account does not have a password, so any password provided is ignored.

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

### Stop parent process first

Optionally specify the order of service shutdown. 
If `true`, the parent process is shutdown first. 
This is useful when the main process is a console, which can respond to Ctrl+C command and will gracefully shutdown child processes.

```xml
<stopparentprocessfirst>true</stopparentprocessfirst>
```
