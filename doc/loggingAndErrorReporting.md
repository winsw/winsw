
WinSW Logging and Error Reporting
=======

### Logging

Winsw supports several different ways to capture stdout and stderr from the process you launch.

### Log directory
The `<logpath>` element specifies the directory in which the log files are created. If this element is absent, it'll default to the same directory where the configuration file resides.

### Append mode (default)
In this mode, `myapp.out.log` and `myapp.err.log` (where `myapp` is the base name of the executable and the configuration file) are created and outputs are simply appended to these files. Note that the file can get quite big.

    <log mode="append"/>

### Reset mode
Works like the append mode, except that every time the service starts, the old log files are truncated.

    <log mode="reset"/>

### Ignore mode
Throw away stdout and stderr, and do not produce any log files at all.

    <log mode="none"/>

### Rotate mode
Works like the append mode, but in addition, if the log file gets bigger than a set size, it gets rotated to `myapp.1.out.log`, `myapp.2.out.log` and so on. The nested `<sizeThreshold>` element specifies the rotation threshold in KB (defaults to 10MB), and the nested `<keepFiles>` element specifies the number of rotated files to keep (defaults to 8.)

```
    <log mode="roll-by-size">
      <sizeThreshold>10240</sizeThreshold>
      <keepFiles>8</keepFiles>
    </log>
```

### Rotate by time mode
Works like the rotate mode, except that instead of using the size as a threshold, use the time period as the threshold.

This configuration must accompany a nested `<pattern>` element, which specifies the timestamp pattern used as the log file name.

```
    <log mode="roll-by-time">
      <pattern>yyyyMMdd</pattern>
    </log>
```

The syntax of the pattern string is specified by [DateTime.ToString()](http://msdn.microsoft.com/en-us/library/zdtaw1bw.aspx). 
For example, in the above example, the log of Jan 1, 2013 gets written to `myapp.20130101.out.log` and `myapp.20130101.err.log`. 

### Rotate by size and time mode
Works in a combination of rotate size mode and rotate time mode, if the log file gets bigger than a set size, it gets rotated using `<pattern>` provided.

```
    <log mode="roll-by-size-time">
      <sizeThreshold>10240</sizeThreshold>
      <pattern>yyyyMMdd</pattern>
      <autoRollAtTime>00:00:00</autoRollAtTime>
      <zipOlderThanNumDays>5</zipOlderThanNumDays>
      <zipDateFormat>yyyyMM</zipDateFormat>
    </log>
```

The syntax of the pattern string is specified by [DateTime.ToString()](http://msdn.microsoft.com/en-us/library/zdtaw1bw.aspx). 
For example, in the above example, the log of Jan 1, 2013 gets written to `myapp.20130101.out.log` and `myapp.20130101.err.log`. 

The syntax of the autoRollAtTime is specified by [TimeSpan.ToString()](https://msdn.microsoft.com/en-us/library/1ecy8h51(v=vs.110).aspx).
For example, in the above example, at the start of the day it will roll the file over.

The zipOlderThanNumDays can only be used in conjection with autoRollAtTime, provide the number of days of files to keep.
```
    <log mode="roll-by-size-time">
      <autoRollAtTime>00:00:00</autoRollAtTime>
      <zipOlderThanNumDays>5</zipOlderThanNumDays>
    </log>
```
The zipDateFormat can only be used in conjection with autoRollAtTime, provide the zip file format using the [DateTime.ToString()](http://msdn.microsoft.com/en-us/library/zdtaw1bw.aspx).
```
    <log mode="roll-by-size-time">
      <autoRollAtTime>00:00:00</autoRollAtTime>
      <zipDateFormat>yyyyMM</zipDateFormat>
    </log>
```

### Error reporting

Winsw uses WMI underneath, and as such it uses its error code as the exit code. 
See the MSDN article [Create method of the Win32_Service class](http://msdn.microsoft.com/en-us/library/aa389390%28VS.85%29.aspx) for the complete list of exit code.

When winsw is running as a service, more detailed error information is reported to the Windows event log.
