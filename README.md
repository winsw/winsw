# Windows Service Wrapper in a permissive license

[![Github All Releases](https://img.shields.io/github/downloads/winsw/winsw/total?style=flat-square)](https://github.com/winsw/winsw/releases)
[![GitHub Release](https://img.shields.io/github/v/release/winsw/winsw?include_prereleases&sort=semver&style=flat-square)](https://github.com/winsw/winsw/releases)
[![NuGet](https://img.shields.io/nuget/v/WinSW?style=flat-square)](https://www.nuget.org/packages/WinSW/)
[![Build Status](https://img.shields.io/azure-devops/build/winsw/aabe43dd-6f6d-4660-b5dd-5b79e1e2ef4e/1?style=flat-square)](https://dev.azure.com/winsw/winsw/_build?definitionId=1&_a=summary)
[![Deployment Status](https://img.shields.io/azure-devops/release/winsw/aabe43dd-6f6d-4660-b5dd-5b79e1e2ef4e/1/1?style=flat-square)](https://dev.azure.com/winsw/winsw/_release?_a=releases&view=mine&definitionId=1)
[![Gitter](https://img.shields.io/gitter/room/winsw/winsw?style=flat-square)](https://gitter.im/winsw/winsw?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge)
[![License](https://img.shields.io/github/license/winsw/winsw?style=flat-square)](LICENSE.txt)

WinSW wraps and manages any application as a Windows service.

**We are actively developing WinSW 3. Please refer to the [v2](https://github.com/winsw/winsw/tree/master) branch for previous version documentation.**

## Why?

See the [project manifest](MANIFEST.md).

## Supported platforms

WinSW 3 can run on Windows platforms with .NET Framework 4.6.1 or later versions installed.
For systems without .NET Framework, the project provides native 64-bit and 32-bit executables based on .NET 7.

More executables can be added upon request.

[.NET Framework system requirements](https://docs.microsoft.com/dotnet/framework/get-started/system-requirements)\
Preinstalled since Windows 10, version 1511 and Windows Server 2016.\
Installable since Windows 7 SP1 and Windows Server 2008 R2 SP1.

[.NET 7 system requirements](https://github.com/dotnet/core/blob/main/release-notes/7.0/supported-os.md)\
Supported since Windows 10, version 1607, Windows Server (Core) 2012 R2 and Nano Server, version 1809.

## Download

Latest release and pre-release WinSW binaries are available on [GitHub Releases](https://github.com/winsw/winsw/releases).

Alternative sources:

* CI builds are available on [Azure Pipelines](https://dev.azure.com/winsw/winsw/_build?definitionId=1).
* [NuGet](https://www.nuget.org/packages/WinSW/). (2.x)
* [Maven packaging](https://github.com/jenkinsci/winsw-maven-packaging) for executables, hosted by the [Jenkins project](https://jenkins.io/).
Binaries are available [here](https://repo.jenkins-ci.org/releases/com/sun/winsw/winsw/). (2.x)

## Get started

### Use WinSW as a global tool

1. Take *WinSW.exe* or *WinSW.zip* from the distribution.
1. Write *myapp.xml* (see the [XML config file specification](docs/xml-config-file.md) and [samples](samples) for more details).
1. Run [`winsw install myapp.xml [options]`](docs/cli-commands.md#install-command) to install the service.
1. Run [`winsw start myapp.xml`](docs/cli-commands.md#start-command) to start the service.
1. Run [`winsw status myapp.xml`](docs/cli-commands.md#status-command) to see if your service is up and running.

### Use WinSW as a bundled tool

1. Take *WinSW.exe* or *WinSW.zip* from the distribution, and rename the *.exe* to your taste (such as *myapp.exe*).
1. Write *myapp.xml* (see the [XML config file specification](docs/xml-config-file.md) and [samples](samples) for more details).
1. Place those two files side by side, because that's how WinSW discovers its co-related configuration.
1. Run [`myapp.exe install [options]`](docs/cli-commands.md#install-command) to install the service.
1. Run [`myapp.exe start`](docs/cli-commands.md#start-command) to start the service.

### Sample configuration file

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

The full specification of the configuration file is available [here](docs/xml-config-file.md).
You can find more samples [here](samples).

## Usage

WinSW is being managed by the [XML configuration file](docs/xml-config-file.md).

Your renamed *WinSW.exe* binary also accepts the following commands:

| Command                                             | Description |
| -------                                             | ----------- |
| [install](docs/cli-commands.md#install-command)     | Installs the service. |
| [uninstall](docs/cli-commands.md#uninstall-command) | Uninstalls the service. |
| [start](docs/cli-commands.md#start-command)         | Starts the service. |
| [stop](docs/cli-commands.md#stop-command)           | Stops the service. |
| [restart](docs/cli-commands.md#restart-command)     | Stops and then starts the service. |
| [status](docs/cli-commands.md#status-command)       | Checks the status of the service. |
| [refresh](docs/cli-commands.md#refresh-command)     | Refreshes the service properties without reinstallation. |
| [customize](docs/cli-commands.md#customize-command) | Customizes the wrapper executable. |
| dev                                                 | Experimental commands. |

Experimental commands:

| Command                                           | Description |
| -------                                           | ----------- |
| [dev ps](docs/cli-commands.md#dev-ps-command)     | Draws the process tree associated with the service. |
| [dev kill](docs/cli-commands.md#dev-kill-command) | Terminates the service if it has stopped responding. |
| [dev list](docs/cli-commands.md#dev-list-command) | Lists services managed by the current executable. |

Most commands require Administrator privileges to execute. WinSW will prompt for UAC in non-elevated sessions.

## Documentation

* [Migrate to WinSW 3.x](docs/migrate-to-3-x.md)
* Configuration:
  * [XML configuration file](docs/xml-config-file.md)
  * [Logging and error reporting](docs/logging-and-error-reporting.md)
  * [Extensions](docs/extensions/extensions.md)
* Use cases:
  * [Self-restarting services](docs/self-restarting-service.md)
  * [Deferred file operations](docs/deferred-file-operations.md)

## Contributing

Contributions are welcome!
See the [contributing guidelines](CONTRIBUTING.md) for more information.

## License

WinSW is licensed under the [MIT](LICENSE.txt) license.
