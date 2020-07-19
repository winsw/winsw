# Logging and error reporting

## Logging

Winsw supports several different ways to capture stdout and stderr from the process you launch.

## Log directory

The `<logpath>` element specifies the directory in which the log files are created. If this element is absent, it'll default to the same directory where the configuration file resides.

## Append mode (default)

In this mode, *myapp.out.log* and *myapp.err.log* (where *myapp* is the base name of the executable and the configuration file) are created and outputs are simply appended to these files. Note that the file can get quite big.

```xml
<log mode="append"/>
```

## Reset mode

Works like the append mode, except that every time the service starts, the old log files are truncated.

```xml
<log mode="reset"/>
```

## Ignore mode

Throw away stdout and stderr, and do not produce any log files at all.

```xml
<log mode="none"/>
```

## Roll mode

Works like the append mode, but in addition, if the log file gets bigger than a set size, it gets rolled to *myapp.1.out.log*, *myapp.2.out.log* and so on. The nested `<sizeThreshold>` element specifies the rotation threshold in KB (defaults to 10MB), and the nested `<keepFiles>` element specifies the number of rolled files to keep (defaults to 8.)

```xml
<log mode="roll-by-size">
  <sizeThreshold>10240</sizeThreshold>
  <keepFiles>8</keepFiles>
</log>
```

## Roll by time mode

Works like the roll mode, except that instead of using the size as a threshold, use the time period as the threshold.

This configuration must accompany a nested `<pattern>` element, which specifies the timestamp pattern used as the log file name.

```xml
<log mode="roll-by-time">
  <pattern>yyyyMMdd</pattern>
</log>
```

The syntax of the pattern string is specified by [DateTime.ToString(String)](https://docs.microsoft.com/dotnet/api/system.datetime.tostring#System_DateTime_ToString_System_String_). 
For example, in the above example, the log of Jan 1, 2013 gets written to `myapp.20130101.out.log` and `myapp.20130101.err.log`. 

## Roll by size and time mode

Works in a combination of roll size mode and roll time mode, if the log file gets bigger than a set size, it gets rolled using `<pattern>` provided.

```xml
<log mode="roll-by-size-time">
  <sizeThreshold>10240</sizeThreshold>
  <pattern>yyyyMMdd</pattern>
  <autoRollAtTime>00:00:00</autoRollAtTime>
</log>
```

The syntax of the pattern string is specified by [DateTime.ToString(String)](https://docs.microsoft.com/dotnet/api/system.datetime.tostring#System_DateTime_ToString_System_String_). 
For example, in the above example, the log of Jan 1, 2013 gets written to `myapp.20130101.out.log` and `myapp.20130101.err.log`. 

The syntax of the autoRollAtTime is specified by [TimeSpan.ToString(String)](https://docs.microsoft.com/dotnet/api/system.timespan.tostring#System_TimeSpan_ToString_System_String_).
For example, in the above example, at the start of the day it will roll the file over.

### Automatic archiving of logs

:warning: This feature is reported to be broken in recent WinSW versions.
It is a potential subject for removal.


```xml
<log mode="roll-by-size-time">
  <zipOlderThanNumDays>5</zipOlderThanNumDays>
  <zipDateFormat>yyyyMM</zipDateFormat>
</log>
```

The `zipOlderThanNumDays` can only be used in conjection with autoRollAtTime, provide the number of days of files to keep.

```xml
<log mode="roll-by-size-time">
  <autoRollAtTime>00:00:00</autoRollAtTime>
  <zipOlderThanNumDays>5</zipOlderThanNumDays>
</log>
```

The zipDateFormat can only be used in conjection with autoRollAtTime, provide the zip file format using the [TimeSpan.ToString(String)](https://docs.microsoft.com/dotnet/api/system.timespan.tostring#System_TimeSpan_ToString_System_String_).

```xml
<log mode="roll-by-size-time">
  <autoRollAtTime>00:00:00</autoRollAtTime>
  <zipDateFormat>yyyyMM</zipDateFormat>
</log>
```

## Error reporting

WinSW uses WMI underneath, and as such it uses its error code as the exit code. 
For the complete list of exit codes, see [return values of the Create method of the Win32_Service class](https://docs.microsoft.com/windows/win32/cimwin32prov/create-method-in-class-win32-service#return-value).

When winsw is running as a service, more detailed error information is reported to the Windows event log.
