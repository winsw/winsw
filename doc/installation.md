WinSW Installation Guide
======

This page provides WinSW installation guidelines for different cases.

### Basic setup

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

You'll rename `winsw.exe` into something like `jenkins.exe`, then you put this XML file as `jenkins.xml`. 
The executable locates the configuration file via this file name convention. 
You can then install the service like:

```
    jenkins.exe install
```

... and you can use the exit code from these processes to determine whether the operation was successful. 
There are other commands to perform other operations, like `uninstall`, `start`, `stop`, and so on.

Once the service is installed, you can start it from Windows service manager. Windows will start `jenkins.exe`, 
  then `jenkins.exe` will launch the executable specified in the configuration file (Java in this case). 
  If this process dies, winsw will exit itself, and the service will be considered stopped.
  
### Making WinSW compatible with .NET runtime 4.0+

<!--TODO: modify the text. Newer => Modern-->
Newer versions of Windows (confirmed on Windows Server 2012, possibly with Windows 8, too) do not ship with .NET runtime `2.0`, which is what `winsw.exe` is built against. 
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

The way the runtime finds this file is by naming convention, so don't forget to rename a file based on your actual executable name. 
See [this post](http://www.davidmoore.info/2010/12/17/running-net-2-runtime-applications-under-the-net-4-runtime/) for more about this. 
<!--TODO: Modify the text-->
To our knowledge, none of the other flags are needed.

### WinSW Offline mode and Authenticode

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
