using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Timers;

namespace winsw
{
    // ReSharper disable once InconsistentNaming
    public interface EventLogger
    {
        void LogEvent(string message);
        void LogEvent(string message, EventLogEntryType type);
    }

    /// <summary>
    /// Abstraction for handling log.
    /// </summary>
    public abstract class LogHandler
    {
        // ReSharper disable once InconsistentNaming
        public abstract void log(Stream outputStream, Stream errorStream);

        /// <summary>
        /// Error and information about logging should be reported here.
        /// </summary>
        public EventLogger EventLogger { set; get; }

        /// <summary>
        /// Convenience method to copy stuff from StreamReader to StreamWriter
        /// </summary>
        protected void CopyStream(Stream i, Stream o)
        {
            byte[] buf = new byte[1024];
            while (true)
            {
                int sz = i.Read(buf, 0, buf.Length);
                if (sz == 0) break;
                o.Write(buf, 0, sz);
                o.Flush();
            }
            i.Close();
            o.Close();
        }

        /// <summary>
        /// File replacement.
        /// </summary>
        protected void CopyFile(string sourceFileName, string destFileName)
        {
            try
            {
                File.Delete(destFileName);
                File.Move(sourceFileName, destFileName);
            }
            catch (IOException e)
            {
                EventLogger.LogEvent("Failed to copy :" + sourceFileName + " to " + destFileName + " because " + e.Message);
            }
        }
    }

    /// <summary>
    /// Base class for file-based loggers
    /// </summary>
    public abstract class AbstractFileLogAppender : LogHandler
    {
        protected string BaseLogFileName { private set; get; }
        protected bool OutFileDisabled { private set; get; }
        protected bool ErrFileDisabled { private set; get; }
        protected string OutFilePattern { private set; get; }
        protected string ErrFilePattern { private set; get; }

        protected AbstractFileLogAppender(string logDirectory, string baseName, bool outFileDisabled, bool errFileDisabled, string outFilePattern, string errFilePattern)
        {
            BaseLogFileName = Path.Combine(logDirectory, baseName);
            OutFileDisabled = outFileDisabled;
            OutFilePattern = outFilePattern;
            ErrFileDisabled = errFileDisabled;
            ErrFilePattern = errFilePattern;
        }
    }

    public abstract class SimpleLogAppender : AbstractFileLogAppender
    {
        public FileMode FileMode { private set; get; }
        public string OutputLogFileName { private set; get; }
        public string ErrorLogFileName { private set; get; }

        protected SimpleLogAppender(string logDirectory, string baseName, FileMode fileMode, bool outFileDisabled, bool errFileDisabled, string outFilePattern, string errFilePattern)
            : base(logDirectory, baseName, outFileDisabled, errFileDisabled, outFilePattern, errFilePattern)
        {
            FileMode = fileMode;
            OutputLogFileName = BaseLogFileName + ".out.log";
            ErrorLogFileName = BaseLogFileName + ".err.log";
        }

        public override void log(Stream outputStream, Stream errorStream)
        {
            if (!OutFileDisabled) new Thread(delegate() { CopyStream(outputStream, new FileStream(OutputLogFileName, FileMode)); }).Start();
            if (!ErrFileDisabled) new Thread(delegate() { CopyStream(errorStream, new FileStream(ErrorLogFileName, FileMode)); }).Start();
        }
    }

    public class DefaultLogAppender : SimpleLogAppender
    {
        public DefaultLogAppender(string logDirectory, string baseName, bool outFileDisabled, bool errFileDisabled, string outFilePattern, string errFilePattern)
            : base(logDirectory, baseName, FileMode.Append, outFileDisabled, errFileDisabled, outFilePattern, errFilePattern)
        {
        }
    }

    public class ResetLogAppender : SimpleLogAppender
    {
        public ResetLogAppender(string logDirectory, string baseName, bool outFileDisabled, bool errFileDisabled, string outFilePattern, string errFilePattern)
            : base(logDirectory, baseName, FileMode.Create, outFileDisabled, errFileDisabled, outFilePattern, errFilePattern)
        {
        }
    }
    
    /// <summary>
    /// LogHandler that throws away output
    /// </summary>
    public class IgnoreLogAppender : LogHandler
    {
        public override void log(Stream outputStream, Stream errorStream)
        {
            new Thread(delegate() { CopyStream(outputStream, Stream.Null); }).Start();
            new Thread(delegate() { CopyStream(errorStream, Stream.Null); }).Start();
        }
    }

    public class TimeBasedRollingLogAppender : AbstractFileLogAppender
    {
        public string Pattern { get; private set; }
        public int Period { get; private set; }

        public TimeBasedRollingLogAppender(string logDirectory, string baseName, bool outFileDisabled, bool errFileDisabled, string outFilePattern, string errFilePattern, string pattern, int period)
            : base(logDirectory, baseName, outFileDisabled, errFileDisabled, outFilePattern, errFilePattern)
        {
            Pattern = pattern;
            Period = period;
        }

        public override void log(Stream outputStream, Stream errorStream)
        {
            if (!OutFileDisabled) new Thread(delegate() { CopyStreamWithDateRotation(outputStream, OutFilePattern); }).Start();
            if (!ErrFileDisabled) new Thread(delegate() { CopyStreamWithDateRotation(errorStream, ErrFilePattern); }).Start();
        }

        /// <summary>
        /// Works like the CopyStream method but does a log rotation based on time.
        /// </summary>
        private void CopyStreamWithDateRotation(Stream data, string ext)
        {
            PeriodicRollingCalendar periodicRollingCalendar = new PeriodicRollingCalendar(Pattern, Period);
            periodicRollingCalendar.init();

            byte[] buf = new byte[1024];
            FileStream w = new FileStream(BaseLogFileName + "_" + periodicRollingCalendar.format + ext, FileMode.Append);
            while (true)
            {
                int len = data.Read(buf, 0, buf.Length);
                if (len == 0) break;    // EOF

                if (periodicRollingCalendar.shouldRoll)
                {// rotate at the line boundary
                    int offset = 0;
                    bool rolled = false;
                    for (int i = 0; i < len; i++)
                    {
                        if (buf[i] == 0x0A)
                        {// at the line boundary.
                            // time to rotate.
                            w.Write(buf, offset, i + 1);
                            w.Close();
                            offset = i + 1;

                            // create a new file and write everything to the new file.
                            w = new FileStream(BaseLogFileName + "_" + periodicRollingCalendar.format + ext, FileMode.Create);
                            rolled = true;
                            if (offset < len)
                            {
                                w.Write(buf, offset, len - offset);
                                break;
                            }
                        }
                    }

                    if (!rolled)
                    {// we didn't roll - most likely as we didnt find a line boundary, so we should log what we read and roll anyway.
                        w.Write(buf, 0, len);
                        w.Close();
                        w = new FileStream(BaseLogFileName + "_" + periodicRollingCalendar.format + ext, FileMode.Create);
                    }

                }
                else
                {// typical case. write the whole thing into the current file
                    w.Write(buf, 0, len);
                }

                w.Flush();
            }
            data.Close();
            w.Close();
        }

    }

    public class SizeBasedRollingLogAppender : AbstractFileLogAppender
    {
        // ReSharper disable once InconsistentNaming
        public static int BYTES_PER_KB = 1024;
        // ReSharper disable once InconsistentNaming
        public static int BYTES_PER_MB = 1024 * BYTES_PER_KB;
        // ReSharper disable once InconsistentNaming
        public static int DEFAULT_SIZE_THRESHOLD = 10 * BYTES_PER_MB; // rotate every 10MB.
        // ReSharper disable once InconsistentNaming
        public static int DEFAULT_FILES_TO_KEEP = 8;

        public int SizeTheshold { private set; get; }

        public int FilesToKeep { private set; get; }

        public SizeBasedRollingLogAppender(string logDirectory, string baseName, bool outFileDisabled, bool errFileDisabled, string outFilePattern, string errFilePattern, int sizeThreshold, int filesToKeep)
            : base(logDirectory, baseName, outFileDisabled, errFileDisabled, outFilePattern, errFilePattern)
        {
            SizeTheshold = sizeThreshold;
            FilesToKeep = filesToKeep;
        }

        public SizeBasedRollingLogAppender(string logDirectory, string baseName, bool outFileDisabled, bool errFileDisabled, string outFilePattern, string errFilePattern)
            : this(logDirectory, baseName, outFileDisabled, errFileDisabled, outFilePattern, errFilePattern, DEFAULT_SIZE_THRESHOLD, DEFAULT_FILES_TO_KEEP) { }

        public override void log(Stream outputStream, Stream errorStream)
        {
            if (!OutFileDisabled) new Thread(delegate() { CopyStreamWithRotation(outputStream, OutFilePattern); }).Start();
            if (!ErrFileDisabled) new Thread(delegate() { CopyStreamWithRotation(errorStream, ErrFilePattern); }).Start();
        }

        /// <summary>
        /// Works like the CopyStream method but does a log rotation.
        /// </summary>
        private void CopyStreamWithRotation(Stream data, string ext)
        {
            byte[] buf = new byte[1024];
            FileStream w = new FileStream(BaseLogFileName + ext, FileMode.Append);
            long sz = new FileInfo(BaseLogFileName + ext).Length;

            while (true)
            {
                int len = data.Read(buf, 0, buf.Length);
                if (len == 0) break;    // EOF
                if (sz + len < SizeTheshold)
                {// typical case. write the whole thing into the current file
                    w.Write(buf, 0, len);
                    sz += len;
                }
                else
                {
                    // rotate at the line boundary
                    int s = 0;
                    for (int i = 0; i < len; i++)
                    {
                        if (buf[i] != 0x0A) continue;
                        if (sz + i < SizeTheshold) continue;

                        // at the line boundary and exceeded the rotation unit.
                        // time to rotate.
                        w.Write(buf, s, i + 1);
                        w.Close();
                        s = i + 1;

                        try
                        {
                            for (int j = FilesToKeep; j >= 1; j--)
                            {
                                string dst = BaseLogFileName + "." + (j - 1) + ext;
                                string src = BaseLogFileName + "." + (j - 2) + ext;
                                if (File.Exists(dst))
                                    File.Delete(dst);
                                if (File.Exists(src))
                                    File.Move(src, dst);
                            }
                            File.Move(BaseLogFileName + ext, BaseLogFileName + ".0" + ext);
                        }
                        catch (IOException e)
                        {
                            EventLogger.LogEvent("Failed to rotate log: " + e.Message);
                        }

                        // even if the log rotation fails, create a new one, or else
                        // we'll infinitely try to rotate.
                        w = new FileStream(BaseLogFileName + ext, FileMode.Create);
                        sz = new FileInfo(BaseLogFileName + ext).Length;
                    }
                }

                w.Flush();
            }
            data.Close();
            w.Close();
        }
    }

    /// <summary>
    /// Rotate log when a service is newly started.
    /// </summary>
    public class RollingLogAppender : SimpleLogAppender
    {
        public RollingLogAppender(string logDirectory, string baseName, bool outFileDisabled, bool errFileDisabled, string outFilePattern, string errFilePattern)
            : base(logDirectory, baseName, FileMode.Append, outFileDisabled, errFileDisabled, outFilePattern, errFilePattern)
        {
        }

        public override void log(Stream outputStream, Stream errorStream)
        {
            if (!OutFileDisabled) CopyFile(OutputLogFileName, OutputLogFileName + ".old");
            if (!ErrFileDisabled) CopyFile(ErrorLogFileName, ErrorLogFileName + ".old");
            base.log(outputStream, errorStream);
        }
    }

    public class RollingSizeTimeLogAppender : AbstractFileLogAppender
    {
        public static int BYTES_PER_KB = 1024;
        public int SizeTheshold { private set; get; }
        public string FilePattern { private set; get; }
        public TimeSpan? AutoRollAtTime { private set; get; }

        public RollingSizeTimeLogAppender(string logDirectory, string baseName, bool outFileDisabled, bool errFileDisabled, string outFilePattern, string errFilePattern, int sizeThreshold, string filePattern, TimeSpan? autoRollAtTime)
            : base(logDirectory, baseName, outFileDisabled, errFileDisabled, outFilePattern, errFilePattern)
        {
            SizeTheshold = sizeThreshold;
            FilePattern = filePattern;
            AutoRollAtTime = autoRollAtTime;
        }

        public override void log(Stream outputStream, Stream errorStream)
        {
            if (!OutFileDisabled) new Thread(delegate () { CopyStreamWithRotation(outputStream, OutFilePattern); }).Start();
            if (!ErrFileDisabled) new Thread(delegate () { CopyStreamWithRotation(errorStream, ErrFilePattern); }).Start();
        }

        private void CopyStreamWithRotation(Stream data, string ext)
        {
            // lock required as the timer thread and the thread that will write to the stream could try and access the file stream at the same time
            var fileLock = new object();

            var buf = new byte[1024];

            var baseDirectory = Path.GetDirectoryName(BaseLogFileName);
            var baseFileName = Path.GetFileName(BaseLogFileName);
            var logFile = $"{BaseLogFileName}{ext}";

            var w = new FileStream(logFile, FileMode.Append);
            var sz = new FileInfo(logFile).Length;

            // We auto roll at time is configured then we need to create a timer and wait until time is elasped and roll the file over
            if (AutoRollAtTime != null)
            {
                var tickTime = SetupRollTimer();
                var timer = new System.Timers.Timer(tickTime);
                timer.Elapsed += (s, e) =>
                {
                    try
                    {
                        timer.Stop();
                        lock (fileLock)
                        {
                            w.Close();

                            var nextFileNumber = GetNextFileNumber(ext, baseDirectory, baseFileName);
                            var nextFileName =  Path.Combine(baseDirectory, $"{baseFileName}.{DateTime.UtcNow.ToString(FilePattern)}.#{nextFileNumber:D4}{ext}");
                            File.Move(logFile, nextFileName);

                            w = new FileStream(logFile, FileMode.Create);
                            sz = new FileInfo(logFile).Length;
                        }
                    }
                    catch (Exception et)
                    {
                        EventLogger.LogEvent($"Failed to to trigger auto roll at time event due to: {et.Message}");
                    }
                    finally
                    {
                        // Recalculate the next interval
                        timer.Interval = SetupRollTimer();
                        timer.Start();
                    }
                };
                timer.Start();
            }
            while (true)
            {
                var len = data.Read(buf, 0, buf.Length);
                if (len == 0) break;    // EOF
                lock (fileLock)
                {
                    if (sz + len < SizeTheshold)
                    {
                        // typical case. write the whole thing into the current file
                        w.Write(buf, 0, len);
                        sz += len;
                    }
                    else
                    {
                        try
                        {
                            // rotate at the line boundary
                            int s = 0;
                            for (int i = 0; i < len; i++)
                            {
                                if (buf[i] != 0x0A) continue;
                                if (sz + i < SizeTheshold) continue;

                                // at the line boundary and exceeded the rotation unit.
                                // time to rotate.
                                w.Write(buf, s, i + 1);
                                w.Close();
                                s = i + 1;

                                // rotate file
                                var nextFileNumber = GetNextFileNumber(ext, baseDirectory, baseFileName);
                                var nextFileName =
                                    Path.Combine(baseDirectory,
                                        $"{baseFileName}.{DateTime.UtcNow.ToString(FilePattern)}.#{nextFileNumber:D4}{ext}");
                                File.Move(logFile, nextFileName);

                                // even if the log rotation fails, create a new one, or else
                                // we'll infinitely try to rotate.
                                w = new FileStream(logFile, FileMode.Create);
                                sz = new FileInfo(logFile).Length;
                            }
                        }
                        catch (Exception e)
                        {
                            EventLogger.LogEvent($"Failed to roll size time log: {e.Message}");
                        }
                    }
                    w.Flush();
                }
            }
            data.Close();
            w.Close();
        }

        private double SetupRollTimer()
        {
            var nowTime = DateTime.Now.ToUniversalTime();
            var scheduledTime = new DateTime(nowTime.Year, nowTime.Month, nowTime.Day, AutoRollAtTime.Value.Hours,
                AutoRollAtTime.Value.Minutes, AutoRollAtTime.Value.Seconds, 0).ToUniversalTime(); //Specify your time HH,MM,SS
            if (nowTime > scheduledTime)
                scheduledTime = scheduledTime.AddDays(1);

            double tickTime = (double) (scheduledTime - DateTime.Now.ToUniversalTime()).TotalMilliseconds;
            return tickTime;
        }

        private int GetNextFileNumber(string ext, string baseDirectory, string baseFileName)
        {
            var nextFileNumber = 0;
            var files = Directory.GetFiles(baseDirectory, $"{baseFileName}.{DateTime.UtcNow.ToString(FilePattern)}.#*{ext}");
            if (files.Length == 0)
            {
                nextFileNumber = 1;
            }
            else
            {
                foreach (var f in files)
                {
                    try
                    {
                        var filenameOnly = Path.GetFileNameWithoutExtension(f);
                        var lastNumberAsString = filenameOnly.Substring(filenameOnly.Length - 4, 4);
                        int lastNumber = 0;
                        if (int.TryParse(lastNumberAsString, out lastNumber))
                        {
                            if (lastNumber > nextFileNumber)
                                nextFileNumber = lastNumber;
                        }
                        else
                            throw new IOException($"File {f} does not follow the pattern provided");
                    }
                    catch (Exception e)
                    {
                        throw new IOException($"Failed to process file {f} due to error {e.Message}", e);
                    }
                }
                if (nextFileNumber == 0) throw new IOException("Cannot roll the file because matching pattern not found");
                nextFileNumber++;
            }
            return nextFileNumber;
        }
    }
}
