Release Notes
====

Below you can find release notes for the trunk version of WinSW.

##### 2.0

Release date: Coming Soon

Improvements:
* Provide the executable for `.NET Framework 4.0`.
With this binary patching of `exe.config` is no longer required to get WinSW running on newest systems.
([PR #147](https://github.com/kohsuke/winsw/pull/147))
* Introduce the [WinSW extension engine](doc/extensions/extensions.md), which allows extending the wrapper's behavior.
([PR #42](https://github.com/kohsuke/winsw/pull/42)).
* Add new `SharedDirectoriesMapper` extension. See the docs [here](doc/extensions/sharedDirectoryMapper.md).
([PR #42](https://github.com/kohsuke/winsw/pull/42)).
* Add new `RunawayProcessKiller` extension. See the docs [here](doc/extensions/runawayProcessKiller.md).
([PR #133](https://github.com/kohsuke/winsw/pull/133)).
* Migrate event logging to [Apache log4net](https://logging.apache.org/log4net/). 
([PR #145](https://github.com/kohsuke/winsw/pull/145), [PR #73](https://github.com/kohsuke/winsw/pull/73) and others).

Fixed issues:
* [Issue #124](https://github.com/kohsuke/winsw/issues/124) - 
Prevent the CPU overutilization when waiting for the process to exit.
([PR #135](https://github.com/kohsuke/winsw/pull/135))
 * It should also fix issues related to the process termination failures, to be confirmed

Non-code changes:
* Documentation refactoring and update
* Introduced the CI flow being hosted on AppVeyor. The project page is [here](https://ci.appveyor.com/project/oleg-nenashev/winsw)
* Starting from WinSW 2.0, [GitHub](https://github.com/kohsuke/winsw/releases) and NuGet will be the release sources
 * [Maven repository](http://repo.jenkins-ci.org/releases/com/sun/winsw/winsw/) will be periodically updated on-demand

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
