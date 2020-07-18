# Windows Service Wrapper in less restrictive license

[![Github All Releases](https://img.shields.io/github/downloads/winsw/winsw/total?style=flat-square)](https://github.com/winsw/winsw/releases)
[![GitHub Release](https://img.shields.io/github/v/release/winsw/winsw?include_prereleases&style=flat-square)](https://github.com/winsw/winsw/releases)
[![NuGet](https://img.shields.io/nuget/v/WinSW?style=flat-square)](https://www.nuget.org/packages/WinSW/)
[![Build Status](https://img.shields.io/azure-devops/build/winsw/aabe43dd-6f6d-4660-b5dd-5b79e1e2ef4e/1?style=flat-square)](https://dev.azure.com/winsw/winsw/_build?definitionId=1&_a=summary)
[![Gitter](https://img.shields.io/gitter/room/winsw/winsw?style=flat-square)](https://gitter.im/winsw/winsw?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge)
[![License](https://img.shields.io/github/license/winsw/winsw?style=flat-square)](LICENSE.txt)

WinSW is an executable binary, which can be used to wrap and manage a custom process as a Windows service.
Once you download the installation package, you can rename *WinSW.exe* to any name, e.g. *MyService.exe*.

**We are actively developing WinSW v3. Please refer to the v2 branch for previous version documentation.**

## Why?

See the [project manifest](MANIFEST.md).

## Supported platforms

WinSW offers executables for .NET Framework 2.0, 4.0 and 4.6.1.
It can run on Windows platforms which have these versions of .NET Framework installed.
For systems without .NET Framework, the project provides native 64-bit and 32-bit executables which are based on .NET Core 3.1.

More executables can be added upon request.

## Download

WinSW binaries are available on [GitHub Releases](https://github.com/winsw/winsw/releases) and [NuGet](https://www.nuget.org/packages/WinSW/).

Alternative sources:

* [Maven packaging](https://github.com/jenkinsci/winsw-maven-packaging) for executables, hosted by the [Jenkins project](https://jenkins.io/). 
Binaries are available [here](https://repo.jenkins-ci.org/releases/com/sun/winsw/winsw/).

## Usage

WinSW is being managed by configuration files: [Main XML configuration file](docs/xml-config-file.md) and [EXE configuration file](docs/exe-config-file.md).

Your renamed *WinSW.exe* binary also accepts the following commands:

* `install`

  Install the service to Windows Service Controller.
  This command requires some preliminary steps described in the [Installation guide](docs/installation.md).

* `uninstall`

  Uninstall the service. The opposite operation of above.

* `start`

  Start the service. The service must have already been installed.

* `stop`

  Stop the service.

* `stopwait`

  Stop the service and wait until it's actually stopped.

* `restart`

  Restart the service. If the service is not currently running, this command acts like `start`.

* `status`

  Check the current status of the service.
  * This command prints one line to the console.
    * `NonExistent` indicates the service is not currently installed
    * `Started` to indicate the service is currently running
    * `Stopped` to indicate that the service is installed but not currently running.

Most commands require Administrator privileges to execute. Since v2.8, WinSW will prompt for UAC in non-elevated sessions.

## Documentation

User documentation:

* [Installation guide](docs/installation.md) - Describes the installation process for different systems and .NET versions
* Configuration:
  * [Main XML configuration file](docs/xml-config-file.md)
  * [EXE configuration file](docs/exe-config-file.md)
  * [Logging and error reporting](docs/logging-and-error-reporting.md)
  * [Extensions](docs/extensions/extensions.md)
* Use-cases:
  * [Self-restarting services](docs/self-restarting-service.md)
  * [Deferred file operations](docs/deferred-file-operations.md)
* Configuration Management:
  * [Puppet Forge Module](docs/puppetWinSW.md)

Developer documentation:

* [Developer guide](DEVELOPER.md)

## Contributing

Contributions are welcome!
No Contributor License Agreement is needed, just submit your pull requests.
See the [contributing guidelines](CONTRIBUTING.md) for more information.

## License

WinSW is licensed under the [MIT](LICENSE.txt) license.
