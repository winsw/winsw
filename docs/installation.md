# Get started

This page provides WinSW installation guidelines for different cases.

## Use WinSW as a global tool

1. Take *WinSW.exe* or *WinSW.zip* from the distribution.
1. Write *myapp.xml* (see the [XML config file specification](xml-config-file.md) for more details)
1. Run [`winsw install myapp.xml [options]`](cli-commands.md#install-command) to install the service.
1. Run [`winsw start myapp.xml`](cli-commands.md#start-command) to start the service.
1. Run [`winsw status myapp.xml`](cli-commands.md#status-command) to see if your service is up and running.

## Use WinSW as a bundled tool

In order to setup WinSW, you commonly need to perform the following steps:

1. Take *WinSW.exe* from the distribution, and rename it to your taste (such as *myapp.exe*)
1. Write *myapp.xml* (see the [XML config file specification](xml-config-file.md) for more details)
1. Place those two files side by side, because that's how WinSW discovers its configuration.
1. Run [`myapp.exe install [options]`](cli-commands.md#install-command) to install the service.
1. Run [`myapp.exe start`](cli-commands.md#start-command) to start the service.

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

The full specification of the configuration file is available [here](xml-config-file.md).

### Step 3. Service registration

The Installer can be also started with the `/p` option.
In such case it will prompt for an account name and password, which should be used as a service account.
