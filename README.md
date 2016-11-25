winsw: Windows service wrapper in less restrictive license
=========================

### Why?

Here is a cite from [Kohsuke Kawaguchi](https://github.com/kohsuke/), who is the original author of this project:

> Now, I think the first question that people would ask is, why another, when there's [Java Service Wrapper project](http://wrapper.tanukisoftware.org/doc/english/download.jsp) already available. 
The main reason for writing my own was the license — Java Service Wrapper project is in GPL (so that they can sell their commercial version in a different license), and that made it difficult for [Jenkins](http://jenkins-ci.org/) (which is under the MIT license) to use it.

> Functionality-wise, there's really not much that's worth noting; the problem of wrapping a process as a Windows service is so well defined that there aren't really any room for substantial innovation. 
You basically write a configuration file specifying how you'd like your process to be launched, and we provide programmatic means to install/uninstall/start/stop services. 
Another notable difference is that winsw can host any executable, whereas Java Service Wrapper can only host Java apps. 
Whether you like this or not depends on your taste, so I wouldn't claim mine is better. 
It's just different.

> As the name implies, this is for Windows only. 
Unix systems have their own conventions for daemons, so a good behaving Unix daemon should just be using `launchd/upstart/SMF/etc`, instead of custom service wrapper.

### Download
[Binaries are available here](http://repo.jenkins-ci.org/releases/com/sun/winsw/winsw/)

### Description

WinSW is an executable binary, which can be used to wrap and manage a custom process as a Windows service.
Once you download the installation package, you can rename `winsw.exe` to any name, e.g. `myService.exe`.

WinSW is being managed by configuration files. 

* 

Your renamed `winsw.exe` binary accepts the following commands:

* `install` to install the service to Windows Service Controller.
  This command requires some preliminary steps described in the [Installation Guide](doc/installation.md).
* `uninstall` to uninstall the service. The opposite operation of above.
* `start` to start the service. The service must have already been installed.
* `stop` to stop the service.
* `restart` to restart the service. If the service is not currently running, this command acts like `start`.
* `status` to check the current status of the service. This command prints one line to the console. `NonExistent` to indicate the service is not currently installed, `Started` to indicate the service is currently running, and `Stopped` to indicate that the service is installed but not currently running.

### Documentation

* [Installation Guide](doc/installation.md) - Describes the installation process for different systems and .NET versions
* [Release notes](CHANGELOG.md)
* Configuration:
 * [Main XML Configuration file](doc/xmlConfigFile.md)
 * [Configuration File](doc/xmlConfigFile.md)
 * [Logging and Error Reporting](doc/loggingAndErrorReporting.md)
* Use-cases:
 * [Self-restarting services](doc/selfRestartingService.md)
 * [Deferred File Operations](doc/deferredFileOperations.md)

### Release lines

#### WinSW 2.x

This is a new release line under active development.
API stability is not guaranteed till the first release, the project structure is in flux.

Major changes since 1.x:
* Rework of the project structure
* Better logging
* Internal plugin engine, which allows extending the WinSW behavior

#### WinSW 1.x

This is an old baseline of WinSW.
Currently it is in the maintenance-only state.
New versions with fixes may be released on-demand.

### Build Environment

* IDE: [Visual Studio Community 2013](http://www.visualstudio.com/en-us/news/vs2013-community-vs.aspx) (free for open-source projects)
* winsw_cert.pfx should be available in the project's root
 * You can generate the certificate in "Project Settings/Signing"
 * The certificate is in <code>.gitignore</code> list. Please do not add it to the repository
