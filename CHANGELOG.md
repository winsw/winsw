Release Notes
====

Below you can find release notes for the trunk version of WinSW.

##### 2.0

Release date: Coming Soon

Improvements:
* Provide the executable for `.NET Framework 4.0`.
([PR #147](https://github.com/kohsuke/winsw/pull/147))
 * With this binary patching of `exe.config` is no longer required to get WinSW running on newest systems.
* Introduce the [WinSW extension engine](doc/extensions/extensions.md), which allows extending the wrapper's behavior.
([PR #42](https://github.com/kohsuke/winsw/pull/42))
* Add new `SharedDirectoriesMapper` extension. See the docs [here](doc/extensions/sharedDirectoryMapper.md)
([PR #42](https://github.com/kohsuke/winsw/pull/42)).
* Add new `RunawayProcessKiller` extension. See the docs [here](doc/extensions/runawayProcessKiller.md).
([PR #133](https://github.com/kohsuke/winsw/pull/133))
* Migrate event logging to [Apache log4net](https://logging.apache.org/log4net/). 
([PR #145](https://github.com/kohsuke/winsw/pull/145), [PR #73](https://github.com/kohsuke/winsw/pull/73) and others).
* [Issue #85](https://github.com/kohsuke/winsw/issues/85) -
Use `FileStream#SafeFileHandle` the deprecated `FileStream#Handle` in the CLI `redirect` mode.
([PR #167](https://github.com/kohsuke/winsw/pull/167))

Fixed issues:
* [Issue #124](https://github.com/kohsuke/winsw/issues/124) - 
Prevent CPU overutilization when waiting for the process to exit.
([PR #135](https://github.com/kohsuke/winsw/pull/135))
* [Issue #159](https://github.com/kohsuke/winsw/issues/159) -
Fix reading of `waithint`, `sleeptime`, `resetfailure`, and `stoptimeout` options from configuration files with XML metadata before `settings`.
([PR #175](https://github.com/kohsuke/winsw/pull/175))
* [Issue #164](https://github.com/kohsuke/winsw/issues/164) - 
Print warnings in the `uninstall` command when the service cannot be uninstalled immediately.
([PR #165](https://github.com/kohsuke/winsw/pull/165))
* [Issue #171](https://github.com/kohsuke/winsw/issues/171) -
Prevent  failure when `stoparguments` are defined without `stopexecutable` in the XML file.
([PR #170](https://github.com/kohsuke/winsw/pull/170))
* [Issue #59](https://github.com/kohsuke/winsw/issues/59) - 
Prevent failure during process termination if child processes cannot be retrieved due to the pending system shutdown.
([PR #172](https://github.com/kohsuke/winsw/pull/172))
* [Issue #54](https://github.com/kohsuke/winsw/issues/54) - 
Security: Do not dump WinSW environment variables to the Event log.
([PR #173](https://github.com/kohsuke/winsw/pull/173))
* Do not propagate exceptions from `Process.Kill()` if the process actually exits.
([PR #166](https://github.com/kohsuke/winsw/pull/166))

Non-code changes:
* Major documentation refactoring and update
* [Issue #154](https://github.com/kohsuke/winsw/issues/154) -
Provide WinSW configuration file samples.
([PR #170](https://github.com/kohsuke/winsw/pull/170)) 
 * Samples are available within release packages
* Introduce the CI flow being hosted on AppVeyor. The project page is [here](https://ci.appveyor.com/project/oleg-nenashev/winsw).
* [GitHub](https://github.com/kohsuke/winsw/releases) 
and [NuGet](https://www.nuget.org/packages/WinSW/) are the main release repositories now
 * Jenkins [Maven repository](http://repo.jenkins-ci.org/releases/com/sun/winsw/winsw/) will be periodically updated on-demand
* [Issue #80](https://github.com/kohsuke/winsw/issues/80) - 
Maven releases now pick releases from GitHub Releases. 
The package version is guaranteed to be same as the assembly version. 
([PR #162](https://github.com/kohsuke/winsw/pull/162))

Compatibility notes:
* WinSW `2.x` is **fully compatible** with WinSW `1.x` in terms of the command-line interface and configuration files.
* Any behavior difference will be considered as a bug
* New features like [WinSW extensions](doc/extensions/extensions.md) are disabled by default. 
They can be enabled via the configuration file.

##### 1.19.1

Release date: Nov 05, 2016

Fixed issues:

* Fix the version number in the executable file metadata and CLI

##### 1.19

Release date: Aug 02, 2016 

* No functional changes.

##### 1.18

Fixed issues: Aug 23, 2015

* [Issue #91](https://github.com/kohsuke/winsw/issues/91) - `%BASE%` contained the executable path instead of the directory path (regression in `1.17`)


##### 1.17

Release date: Mar 29, 2015

Changes: See the [winsw-1.17 milestone](https://github.com/kohsuke/winsw/milestone/1).

##### Previous versions

WinSW versions older than `1.17` have no explicit changelog.
If you need such info, see the [commit history](https://github.com/kohsuke/winsw/commits/master).
