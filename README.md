# Windows Service Wrapper in a permissive license

[![Github All Releases](https://img.shields.io/github/downloads/winsw/winsw/total?style=flat-square)](https://github.com/winsw/winsw/releases)
[![GitHub Release](https://img.shields.io/github/v/release/winsw/winsw?include_prereleases&style=flat-square)](https://github.com/winsw/winsw/releases)
[![NuGet](https://img.shields.io/nuget/v/WinSW?style=flat-square)](https://www.nuget.org/packages/WinSW/)
[![Build Status](https://img.shields.io/azure-devops/build/winsw/aabe43dd-6f6d-4660-b5dd-5b79e1e2ef4e/1?style=flat-square)](https://dev.azure.com/winsw/winsw/_build?definitionId=1&_a=summary)
[![Deployment Status](https://img.shields.io/azure-devops/release/winsw/aabe43dd-6f6d-4660-b5dd-5b79e1e2ef4e/1/1?style=flat-square)](https://dev.azure.com/winsw/winsw/_release?_a=releases&view=mine&definitionId=1)
[![Gitter](https://img.shields.io/gitter/room/winsw/winsw?style=flat-square)](https://gitter.im/winsw/winsw?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge)
[![License](https://img.shields.io/github/license/winsw/winsw?style=flat-square)](LICENSE.txt)

WinSW is an executable binary, which can be used to wrap and manage a custom process as a Windows service.
Once you download the installation package, you can rename *WinSW.exe* to any name, e.g. *MyService.exe*.

**We are actively developing WinSW v3. Please refer to the v2 branch for previous version documentation.**

**Please help us prioritize items by voting or commenting on the issues!**

## Why?

See the [project manifest](MANIFEST.md).

## Supported platforms

WinSW offers executables for .NET Framework 2.0, 4.0 and 4.6.1.
It can run on Windows platforms which have these versions of .NET Framework installed.
For systems without .NET Framework, the project provides native 64-bit and 32-bit executables based on .NET Core.

More executables can be added upon request.

## Download

WinSW binaries are available on [GitHub Releases](https://github.com/winsw/winsw/releases) and [NuGet](https://www.nuget.org/packages/WinSW/).

Alternative sources:

* [Maven packaging](https://github.com/jenkinsci/winsw-maven-packaging) for executables, hosted by the [Jenkins project](https://jenkins.io/). 
Binaries are available [here](https://repo.jenkins-ci.org/releases/com/sun/winsw/winsw/).

## Usage

WinSW is being managed by the [XML configuration file](docs/xml-config-file.md).

Your renamed *WinSW.exe* binary also accepts the following commands:

| Command                                               | Description |
| -----------                                           | ----------- |
| [`install`](docs/cli-commands.md#install-command)     | Installs the service. This command requires some preliminary steps described in the [Installation guide](docs/installation.md). |
| [`uninstall`](docs/cli-commands.md#uninstall-command) | Uninstalls the service. |
| [`start`](docs/cli-commands.md#start-command)         | Starts the service. |
| [`stop`](docs/cli-commands.md#stop-command)           | Stops the service. |
| [`restart`](docs/cli-commands.md#restart-command)     | Stops and then starts the service. |
| [`status`](docs/cli-commands.md#status-command)       | Checks the status of the service. |
| [`test`](docs/cli-commands.md#test-command)           | Checks if the service can be started and then stopped without installation. |

Most commands require Administrator privileges to execute. Since 2.8, WinSW will prompt for UAC in non-elevated sessions.

## Documentation

User documentation:

* [Installation guide](docs/installation.md) - Describes the installation process for different systems and .NET versions
* [Migration guide](docs/migrate-to-3-x) - Migrate to WinSW 3.x.
* Configuration:
  * [XML configuration file](docs/xml-config-file.md)
  * [Logging and error reporting](docs/logging-and-error-reporting.md)
  * [Extensions](docs/extensions/extensions.md)
* Use-cases:
  * [Self-restarting services](docs/self-restarting-service.md)
  * [Deferred file operations](docs/deferred-file-operations.md)

Developer documentation:

* [Developer guide](docs/developer)

## Contributing

Contributions are welcome!
No Contributor License Agreement is needed, just submit your pull requests.
See the [contributing guidelines](CONTRIBUTING.md) for more information.

## License

WinSW is licensed under the [MIT](LICENSE.txt) license.
