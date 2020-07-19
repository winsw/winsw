# Installation guide

This page provides WinSW installation guidelines for different cases.

## Installation steps

In order to setup WinSW, you commonly need to perform the following steps:

1. Take *WinSW.exe* from the distribution, and rename it to your taste (such as *myapp.exe*)
1. Write *myapp.xml* (see the [XML config file specification](xmlConfigFile.md) for more details)
1. Place those two files side by side, because that's how WinSW discovers its configuration.
1. Run `myapp.exe install <OPTIONS>` in order to install the service wrapper.
1. Run `myapp.exe start` to start the service.

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
  <log mode="roll"></log>
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
