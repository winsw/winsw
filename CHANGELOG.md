Release Notes
====

Below you can find release notes for the trunk version of WinSW.

##### 2.0

Release date: Coming Soon

Improvements:
* Introduced the [WinSW extension engine](doc/extensions/extensions.md), which allows extending the wrapper's behavior.
([PR #42](https://github.com/kohsuke/winsw/pull/42)).
* [SharedDirectoriesMapper extension](doc/extensions/sharedDirectoryMapper.md)
([PR #42](https://github.com/kohsuke/winsw/pull/42)).
* [RunawayProcessKiller extension](doc/extensions/runawayProcessKiller.md)
([PR #133](https://github.com/kohsuke/winsw/pull/133)).
* Migrate event logging to `log4j`
([PR #73](https://github.com/kohsuke/winsw/pull/73)).

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