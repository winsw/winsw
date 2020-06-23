# Shared Directory Mapper extension

By default Windows does not establish shared drive mapping for services even if it is configured in the Windows service profile.
And sometimes it is impossible to workaround it due to the domain policies.

This extension allows mapping external shared directories before starting up the executable.

Since: WinSW 2.0.

## Usage

The extension can be configured via the [XML configuration file](../xmlConfigFile.md). 
Configuration sample:

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

## Notes

* If the extension fails to map the drive, the startup fails
