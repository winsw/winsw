WinSW Installation Guide
======

This page provides WinSW installation guidelines for different cases.

### Installation steps

In order to setup WinSW, you commonly need to perform the following steps:

0. Take `winsw.exe` from the distribution, and rename it to your taste (such as `myapp.exe`)
0. Write `myapp.xml `(see [XML Config File specification](xmlConfigFile.md) for more details)
0. Place those two files side by side, because that's how WinSW discovers its configuration.
0. Run `myapp.exe install <OPTIONS>` in order to install the service wrapper.
0. Optional - Perform additional configuration in the Windows Service Manager.
0. Optional - Perform extra configurations if required (guidelines are available below).
 * Declare that the executable is compatible with .NET 4 or above (for WinSW 1.x **only**)
 * Enable the WinSW offline mode
0. Run the service from the Windows Service Manager.

There are some details for each step available below.

### Installation step details

#### Step 2. Configuration file

You write the configuration file that defines your service. 
The example below is a primitive example being used in the Jenkins project:

```
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

#### Step 3. Service registration
 
You can then install the service like:

```
    myapp.exe install <OPTIONS>
```

... and you can use the exit code from these processes to determine whether the operation was successful. 
Possible return error codes are described  [here](http://msdn.microsoft.com/en-us/library/aa389390%28VS.85%29.aspx). 
Beyond these error codes, all the non-zero exit code should be assumed as a failure.

The Installer can be also started with the `/p` option.
In such case it will prompt for an account name and password, which should be used as a service account.

#### Step 4. Windows Service Manager

Once the service is installed, you can start it from Windows Service Manager.
If you open `Properties` for the service, you can also configure how the service should be launched. 

In particular, the following option can be set up:

* Service automatic startup on the Windows startup
* User or system account, under which the service runs
* Recovery options (how Windows recovers the service if it dies due to whatever reason)

In addition to the service manager, it is possible to make some additional configurations in the `Windows Registry Editor`.

Once the start button is clicked, Windows will start `myapp.exe`, 
  then `myapp.exe` will launch the executable specified in the configuration file (Java in this case). 
  If this process dies, `myapp.exe` will exit itself, and the service will be considered stopped.
  
### Extra configuration options
  
#### Making WinSW 1.x compatible with .NET runtime 4.0+

**NOTE.** _Starting from WinSW `2.0` the release offers a new binary, which targets the .NET Framework 4.0.
Such configuration is no longer required._  

Modern versions of Windows (e.g. Windows Server 2012 or Windows 10) do not ship with .NET runtime `2.0`, which is what `winsw.exe` is built against. 
This is because unlike Java, where a newer runtime can host apps developed against earlier runtime, .NET apps need version specific runtimes.

One way to deal with this is to ensure that `.NET 2.0` runtime is installed through your installer, but another way is to declare that `winsw.exe` can be hosted on `.NET 4.0` runtime by creating an app config file `winsw.exe.config`.

```
  <configuration>
    <startup>
      <supportedRuntime version="v2.0.50727" />
      <supportedRuntime version="v4.0" />
    </startup>
  </configuration>
```

The way the runtime finds this file is by naming convention, so don't forget to rename a file based on your actual executable name (e.g. `myapp.exe`). 
See [this post](http://www.davidmoore.info/2010/12/17/running-net-2-runtime-applications-under-the-net-4-runtime/) for more about this. 
None of the other flags are needed.

#### WinSW Offline mode and Authenticode

To work with UAC-enabled Windows, winsw ships with a digital signature.
This causes Windows to automatically verify this digital signature when the application is launched (see [more discussions](http://msdn.microsoft.com/en-us/library/bb629393.aspx)). 
This adds some delay to the launch of the service, and more importantly, it prevents winsw from running in a server that has no internet connection. 
This is because a part of the signature verification involves checking certificate revocation list.

To prevent this problem, create `myapp.exe.config` in the same directory as `myapp.exe` (renamed `winsw.exe`) and put the following in it:

```
    <configuration>
      <runtime>
        <generatePublisherEvidence enabled="false"/> 
      </runtime>
    </configuration>
```

See [KB 936707](http://support.microsoft.com/kb/936707) for more details.
