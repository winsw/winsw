# Installation guide

This page provides WinSW installation guidelines for different cases.

## Installation steps

In order to setup WinSW, you commonly need to perform the following steps:

1. Take *WinSW.exe* from the distribution, and rename it to your taste (such as *myapp.exe*)
1. Write *myapp.xml* (see [XML config file specification](xmlConfigFile.md) for more details)
1. Place those two files side by side, because that's how WinSW discovers its configuration.
1. Run `myapp.exe install <OPTIONS>` in order to install the service wrapper.
1. Optional - Perform additional configuration in the Windows Service Manager.
1. Optional - Perform extra configurations if required (guidelines are available below).
   * Declare that the executable is compatible with .NET 4 or above (**for WinSW v1 only**)
   * Enable the WinSW offline mode
1. Run the service from the Windows Service Manager.

There are some details for each step available below.

## Installation step details

### Step 2. Configuration file

You write the configuration file that defines your service. 
The example below is a primitive example being used in the Jenkins project:

```xml
<service>
  <id>jenkins</id>
  <name>Jenkins</name>
  <description>This service runs Jenkins continuous integration system.</description>
  <env name="JENKINS_HOME" value="%BASE%"/>
  <executable>java</executable>
  <arguments>-Xrs -Xmx256m -jar "%BASE%\jenkins.war" --httpPort=8080</arguments>
  <logmode>rotate</logmode>
</service>
```

The full specification of the configuration file is available [here](xmlConfigFile.md).

### Step 3. Service registration
 
You can then install the service like:

```
myapp.exe install <OPTIONS>
```

... and you can use the exit code from these processes to determine whether the operation was successful. 
Possible exit codes are described [here](https://docs.microsoft.com/windows/win32/cimwin32prov/create-method-in-class-win32-service#return-value). 
Beyond these error codes, all the non-zero exit code should be assumed as a failure.

The Installer can be also started with the `/p` option.
In such case it will prompt for an account name and password, which should be used as a service account.

### Step 4. Windows Service Manager

Once the service is installed, you can start it from Windows Service Manager.
If you open `Properties` for the service, you can also configure how the service should be launched. 

In particular, the following option can be set up:

* Service automatic startup on the Windows startup
* User or system account, under which the service runs
* Recovery options (how Windows recovers the service if it dies due to whatever reason)

In addition to the service manager, it is possible to make some additional configurations in the `Windows Registry Editor`.

Once the start button is clicked, Windows will start *myapp.exe*, 
  then *myapp.exe* will launch the executable specified in the configuration file (Java in this case). 
  If this process dies, *myapp.exe* will exit itself, and the service will be considered stopped.

## Extra configuration options

### Making WinSW v1 compatible with .NET runtime 4.0+

**IMPORTANT:** *Starting from WinSW v2 the release offers a new binary, which targets the .NET Framework 4.0.
Such configuration is no longer required.*

Modern versions of Windows (e.g. Windows Server 2012 or Windows 10) do not ship with .NET Framework 2.0, which is what *WinSW.exe* is built against. 
This is because unlike Java, where a newer runtime can host apps developed against earlier runtime, .NET apps need version specific runtimes.

One way to deal with this is to ensure that .NET Framework 2.0 is installed through your installer, but another way is to declare that *WinSW.exe* can be hosted on .NET Framework 4.0 by creating an app config file *WinSW.exe.config*.

```xml
<configuration>
  <startup>
    <supportedRuntime version="v2.0.50727" />
    <supportedRuntime version="v4.0" />
  </startup>
</configuration>
```

The way the runtime finds this file is by naming convention, so don't forget to rename a file based on your actual executable name (e.g. *myapp.exe*). 
For more information, see [How to: Configure an App to Support .NET Framework 4 or later versions](https://docs.microsoft.com/dotnet/framework/migration-guide/how-to-configure-an-app-to-support-net-framework-4-or-4-5).
None of the other flags are needed.
