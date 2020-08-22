# Shared Directory Mapper extension

By default Windows does not establish shared drive mapping for services even if it is configured in the Windows service profile.
And sometimes it is impossible to workaround it due to the domain policies.

This extension allows mapping external shared directories before starting up the executable.

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
    <extension enabled="true" className="winsw.Plugins.SharedDirectoryMapper.SharedDirectoryMapper" id="mapNetworDirs">
      <mapping>
        <map enabled="false" label="N:" uncpath="\\UNC"/>
        <map enabled="false" label="M:" uncpath="\\UNC2"/>
      </mapping>
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
    - id: mapNetworDirs
      className: winsw.Plugins.SharedDirectoryMapper.SharedDirectoryMapper
      enabled: true
      settings:
          mapping:
              - enabled: false
                label: N
                uncpath: \\UNC
              - enabled: false
                label: M
                uncpath: \\UNC2
```

## Notes

* If the extension fails to map the drive, the startup fails
