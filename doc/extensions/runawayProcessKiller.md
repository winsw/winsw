# Runaway Process Killer extension

In particular cases Windows service wrapper may leak the process after the service completion.
It happens when WinSW gets terminated without executing the shutdown logic.
Examples: force kill of the service process, .NET Runtime crash, missing permissions to kill processes or a bug in the logic.

Such runaway processes may conflict with the service process once it restarts.
This extension allows preventing it by running the runaway process termination on startup before the executable gets started.

Since: WinSW 2.0.

## Usage

The extension can be configured via the [XML configuration file](../xmlConfigFile.md) or [YAML configuration file](../yamlConfigFile.md). 

### XML configuration sample

```xml
<?xml version="1.0" encoding="utf-8" ?>
<service>
  <id>sampleService</id>
  <name>Sample service</name>
  <description>This is a stub service.</description>
  <executable>%BASE%\sleep.bat</executable>
  <arguments></arguments>
  <log mode="roll"></log>

  <extensions>
	<!-- This is a sample configuration for the RunawayProcessKiller extension. -->
  <extension enabled="true" 
             className="winsw.Plugins.RunawayProcessKiller.RunawayProcessKillerExtension"
             id="killOnStartup">
      <!-- Absolute path to the PID file, which stores ID of the previously launched process. -->
      <pidfile>%BASE%\pid.txt</pidfile>
      <!-- Defines the process termination timeout in milliseconds. 
           This timeout will be applied multiple times for each child process.
           After the timeout WinSW will try to force kill the process.
      -->
      <stopTimeout>5000</stopTimeout>
      <!-- If true, the parent process will be terminated first if the runaway process gets terminated. -->
      <stopParentFirst>true</stopParentFirst>
    </extension>
  </extensions>
</service>
```

### YAML configuration sample

```yaml
id: sampleService
name: Sample Service
description: This is a stub service.
executable: '%BASE%\sleep.bat'
arguments: arg1 arg2
log:
  mode: roll
extensions:
    - id: killRunawayProcess
      enabled: yes
      className: winsw.Plugins.RunawayProcessKiller.RunawayProcessKillerExtension
      settings:
            pidfile: 'foo/bar/pid.txt'
            stopTimeOut: 5000
            StopParentFirst: true
```

## Notes

* The current implementation of the the extension checks only the root process (started executable)
* If the runaway process is detected the entire, the entire process tree gets terminated
* WinSW gives the runaway process a chance to the gracefully terminate. 
If it does not do it within the timeout, the process will be force-killed.
* If the force kill fails, the WinSW startup continues with a warning.
