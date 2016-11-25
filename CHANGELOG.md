Release Notes
====

Below you can release notes for the trunk version of WinSW.

##### 2.0

Release date: Coming Soon

Improvements:
* Introduce a concept of WinSW extensions, which allow extending the wrapper's behavior
([PR #42](https://github.com/kohsuke/winsw/pull/42)).
 * Right now the extensions are not guaranteed to be binary compatible
  * Engine does not support their inclusion from external DLLs (but you can repackage the executable if you want to extend WinSW)
* `SharedDirectoriesMapper` extension for mapping shared directories when the Windows service starts up 
([PR #42](https://github.com/kohsuke/winsw/pull/42)).
* Migrate event logging handlers to log4j
([PR #73](https://github.com/kohsuke/winsw/pull/73)).

##### 1.19.1

Release date: Nov 05, 2016

Fixed issues:

* Fix the version number in the executable file metadata and CLI

##### 1.19

Release date: Aug 02, 2016 

No functional changes.

##### 1.18

Fixed issues: Aug 23, 2015

* [Issue #91](https://github.com/kohsuke/winsw/issues/91) - `%BASE%` contained the executable path instead of the directory path (regression in `1.17`)


##### 1.17

Release date: Mar 29, 2015

Changes: See the [winsw-1.17 milestone](https://github.com/kohsuke/winsw/milestone/1).

##### Previous versions

WinSW versions older than `1.17` have no explicit changelog.
If you need such info, see the [commit history](https://github.com/kohsuke/winsw/commits/master).