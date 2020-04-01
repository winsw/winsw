# winsw: Windows service wrapper in less restrictive license

[![Github All Releases](https://img.shields.io/github/downloads/winsw/winsw/total?style=flat-square)](https://github.com/winsw/winsw/releases)
[![NuGet](https://img.shields.io/nuget/v/WinSW?style=flat-square)](https://www.nuget.org/packages/WinSW/)
[![Build Status](https://img.shields.io/appveyor/build/winsw/winsw?style=flat-square)](https://ci.appveyor.com/project/winsw/winsw)
[![Gitter](https://img.shields.io/gitter/room/winsw/winsw?style=flat-square)](https://gitter.im/winsw/winsw?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge)
[![License](https://img.shields.io/github/license/winsw/winsw?style=flat-square)](LICENSE.txt)

WinSW is an executable binary, which can be used to wrap and manage a custom process as a Windows service.
Once you download the installation package, you can rename *WinSW.exe* to any name, e.g. *MyService.exe*.

### Why?

See the [project manifest](MANIFEST.md).

### Download

Starting from WinSW v2, the releases are being hosted on [GitHub](https://github.com/winsw/winsw/releases) and [NuGet](https://www.nuget.org/packages/WinSW/).

Due to historical reasons, the project also uses  [Jenkins Maven repository](https://jenkins.io/index.html) as a secondary source. 
Binaries are available [here](http://repo.jenkins-ci.org/releases/com/sun/winsw/winsw/). 

The executables in all sources are [strong-named assemblies](https://msdn.microsoft.com/en-us/library/wd40t7ad%28v=vs.110%29.aspx), which are being signed by randomly generated keys.
Do not rely on such strong names for security (as well as on other strong names as it recommended by Microsoft). 
They provide a unique identity only.

### Usage

WinSW is being managed by configuration files: [Main XML Configuration file](doc/xmlConfigFile.md) and [EXE Config file](doc/exeConfigFile.md).

Your renamed *WinSW.exe* binary also accepts the following commands:

* `install` to install the service to Windows Service Controller.
  This command requires some preliminary steps described in the [Installation Guide](doc/installation.md).
* `uninstall` to uninstall the service. The opposite operation of above.
* `start` to start the service. The service must have already been installed.
* `stop` to stop the service.
* `restart` to restart the service. If the service is not currently running, this command acts like `start`.
* `status` to check the current status of the service.
  * This command prints one line to the console.
    * `NonExistent` indicates the service is not currently installed
    * `Started` to indicate the service is currently running
    * `Stopped` to indicate that the service is installed but not currently running.

### Supported .NET versions

#### WinSW v2

WinSW v2 offers two executables, which declare .NET Frameworks 2.0 and 4.0 as targets.
More executables can be added on-demand.
Please create an issue if you need such executables.

#### WinSW v1

WinSW v1 Executable is being built with a .NET Framework 2.0 target, and by defaut it will work only for .NET Framework versions below 3.5.
On the other hand, the code is known to be compatible with .NET Framework 4.0 and above.
It is possible to declare the support of this framework via the *.exe.config* file.
See the [Installation Guide](doc/installation.md) for more details.

### Documentation

User documentation:

* [Installation Guide](doc/installation.md) - Describes the installation process for different systems and .NET versions
* [Release notes](CHANGELOG.md)
* Configuration:
  * [Main XML Configuration file](doc/xmlConfigFile.md)
  * [EXE Configuration File](doc/exeConfigFile.md)
  * [Logging and Error Reporting](doc/loggingAndErrorReporting.md)
  * [Extensions](doc/extensions/extensions.md)
* Use-cases:
  * [Self-restarting services](doc/selfRestartingService.md)
  * [Deferred File Operations](doc/deferredFileOperations.md)
* Configuration Management:
  * [Puppet Forge Module](doc/puppetWinSW.md)

Developer documentation:

* [Developer Guide](DEVELOPER.md)

### Release lines

#### WinSW v2

This is a new baseline of WinSW with several major changes:
* Major documentation rework and update
* New executable package targeting the .NET Framework 4.0. .NET Framework 2.0 is still supported.
* [Extension engine](doc/extensions/extensions.md), which allows extending the wrapper's behavior. And a couple of extensions for it (Shared Directory Mapper, Runaway Process Killer)
* New release hosting: GitHub and NuGet
* Migration of the logging subsystem to Apache log4net
* Bugfixes

See the full changelog in the [release notes](CHANGELOG.md).

The version v2 is **fully compatible** with the v1 configuration file format, 
  hence the upgrade procedure just requires replacement of the executable file.

#### WinSW v1

This is an old baseline of WinSW.
Currently it is in the maintenance-only state.
New versions with fixes may be released on-demand.

## License

WinSW is licensed under the [MIT](LICENSE.txt) license.
