Release Notes
====

##### 2.2.0

Release date: Jan 06, 2019

* [PR #247](https://github.com/kohsuke/winsw/pull/247) -
Intoduce new logging configuration options to allow renaming and disabling logs
(`logname`, `outfiledisabled`, `errfiledisabled`, `outfilepattern`, `errfilepattern`)
* [PR #259](https://github.com/kohsuke/winsw/pull/259) -
Add support of archiving old log files to the 'roll-by-size-time' log appender 
(`zipOlderThanNumDays` and `zipDateFormat` options)
* [PR #239](https://github.com/kohsuke/winsw/pull/239) -
Improve logging for process termination by Runaway Process Killer
* [PR #254](https://github.com/kohsuke/winsw/pull/254) -
Performance: prevent double loading of the service descriptors on startup

##### 2.1.2

Release date: July 8, 2017

Fixed issues:

* [PR #228](https://github.com/kohsuke/winsw/pull/228) - 
Runaway Process Killer extension was not using the `stopTimeoutMs` parameter.

##### 2.1.1

Release date: June 12, 2017

Fixed issues:

* [Issue #206](https://github.com/kohsuke/winsw/issues/206) - 
Prevent printing of log entries in the `status` command.
([PR #214](https://github.com/kohsuke/winsw/pull/214))
* [Issue #218](https://github.com/kohsuke/winsw/issues/218) - 
Prevent hanging of the `stopexecutable` when its logs are not being drained do the parent process.
([PR #220](https://github.com/kohsuke/winsw/pull/220), [PR #224](https://github.com/kohsuke/winsw/pull/224))

##### 2.1.0

Release date: April 19, 2017

Improvements:
* [Issue #183](https://github.com/kohsuke/winsw/issues/183) -
Add support of the Delayed Automatic Start mode definition in config XML.
[More Info](doc/xmlConfigFile.md#delayedautostart).
([PR #205](https://github.com/kohsuke/winsw/pull/205))
* [Issue #126](https://github.com/kohsuke/winsw/issues/126) - 
Add support of BASIC and [SSPI](https://en.wikipedia.org/wiki/Security_Support_Provider_Interface) authentication in the `<download>` action.
[More Info](https://github.com/kohsuke/winsw/blob/master/doc/xmlConfigFile.md#download).
([PR #194](https://github.com/kohsuke/winsw/pull/194), [PR #203](https://github.com/kohsuke/winsw/pull/203))
* Introduce the `failOnError` option in the `<download>` action.
[More Info](https://github.com/kohsuke/winsw/blob/master/doc/xmlConfigFile.md#download).
([PR #195](https://github.com/kohsuke/winsw/pull/195))

##### 2.0.3

Release date: Apr 01, 2017

Fixed issues:
* [Issue #201](https://github.com/kohsuke/winsw/issues/201) -
Prevent conversion of environment variables to lowercase in the started executable.
([PR #202](https://github.com/kohsuke/winsw/pull/202))

##### 2.0.2

Release date: Feb 13, 2017

Fixed issues:
* [Issue #181](https://github.com/kohsuke/winsw/issues/181) - 
Fix metadata of the `WinSW.NET2.exe` executable to make it really running on .NET Framework 2.
([PR #188](https://github.com/kohsuke/winsw/pull/188))
* [Issue #95](https://github.com/kohsuke/winsw/issues/95) - 
During process tree termination rebuild the child process tree after the termination if `stopparentprocessfirst` is set.
Enhances the fix of [Issue #59](https://github.com/kohsuke/winsw/issues/59) in WinSW 2.0. 
([PR #186](https://github.com/kohsuke/winsw/pull/186))

##### 2.0.1

Release date: Jan 06, 2017

Fixed issues:
* [Issue #178](https://github.com/kohsuke/winsw/issues/178) - 
Fix processing of the legacy `arguments` parameter.
Regression in `2.0`.
([PR #179](https://github.com/kohsuke/winsw/pull/179))

##### 2.0

Release date: Dec 30, 2016

Improvements:
* [Issue #103](https://github.com/kohsuke/winsw/issues/103) -
Provide the executable for `.NET Framework 4.0`.
([PR #147](https://github.com/kohsuke/winsw/pull/147))
 * With this binary patching of `exe.config` is no longer required to get WinSW running on newest systems.
* [Issue #154](https://github.com/kohsuke/winsw/issues/154) -
 Provide WinSW configuration file samples.
 ([PR #170](https://github.com/kohsuke/winsw/pull/170)) 
  * Samples are available within release packages
* Introduce the new [WinSW Extension Engine](doc/extensions/extensions.md).
([PR #42](https://github.com/kohsuke/winsw/pull/42))
* Add new `SharedDirectoriesMapper` extension. See the docs [here](doc/extensions/sharedDirectoryMapper.md)
([PR #42](https://github.com/kohsuke/winsw/pull/42)).
* [Issue #125](https://github.com/kohsuke/winsw/issues/125) - 
Add new `RunawayProcessKiller` extension. See the docs [here](doc/extensions/runawayProcessKiller.md).
([PR #133](https://github.com/kohsuke/winsw/pull/133))
* [Issue #69](https://github.com/kohsuke/winsw/issues/69) - 
Migrate event logging to [Apache log4net](https://logging.apache.org/log4net/). 
([PR #145](https://github.com/kohsuke/winsw/pull/145), [PR #73](https://github.com/kohsuke/winsw/pull/73) and others).
* [Issue #85](https://github.com/kohsuke/winsw/issues/85) -
Use `FileStream#SafeFileHandle` the deprecated `FileStream#Handle` in the CLI `redirect` mode.
([PR #167](https://github.com/kohsuke/winsw/pull/167))

Fixed issues:
* [Issue #124](https://github.com/kohsuke/winsw/issues/124) - 
Prevent CPU overutilization when waiting for the process to exit.
([PR #135](https://github.com/kohsuke/winsw/pull/135))
* [Issue #159](https://github.com/kohsuke/winsw/issues/159) -
Properly retrieve `waithint`, `sleeptime`, `resetfailure`, and `stoptimeout` options from XML configs with metadata before `settings`.
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
* Major documentation refactoring and update.
* Use [GitHub Releases](https://github.com/kohsuke/winsw/releases) as a main release source.
 * Jenkins [Maven repository](http://repo.jenkins-ci.org/releases/com/sun/winsw/winsw/) is no longer the main release source
 * It will be periodically updated on-demand
* [Issue #65](https://github.com/kohsuke/winsw/issues/65) -
Introduce NuGet packaging and publishing.
 * Releases are being published on `www.nuget.org`.
[Package page](https://www.nuget.org/packages/WinSW/) 
* [Issue #80](https://github.com/kohsuke/winsw/issues/80) - 
Maven releases now pick releases from GitHub Releases. 
The package version is guaranteed to be same as the assembly version. 
([PR #162](https://github.com/kohsuke/winsw/pull/162))
* [Issue #142](https://github.com/kohsuke/winsw/issues/142) - 
Introduce the CI/CD flow being hosted on AppVeyor. The project page is [here](https://ci.appveyor.com/project/oleg-nenashev/winsw).

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
