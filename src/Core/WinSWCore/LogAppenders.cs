using System;
using System.Diagnostics;
#if VNEXT
using System.IO.Compression;
#endif
using System.IO;
using System.Threading;
#if !VNEXT
using ICSharpCode.SharpZipLib.Zip;
#endif

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
        public EventLogger EventLogger { get; set; }

        /// <summary>
        /// Convenience method to copy stuff from StreamReader to StreamWriter
        /// </summary>
        protected void CopyStream(Stream i, Stream o)
        {
            byte[] buf = new byte[1024];
            while (true)
            {
                int sz = i.Read(buf, 0, buf.Length);
                if (sz == 0)
                    break;

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
        protected string BaseLogFileName { get; private set; }
        protected bool OutFileDisabled { get; private set; }
        protected bool ErrFileDisabled { get; private set; }
        protected string OutFilePattern { get; private set; }
        protected string ErrFilePattern { get; private set; }

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
        public FileMode FileMode { get; private set; }
        public string OutputLogFileName { get; private set; }
        public string ErrorLogFileName { get; private set; }

        protected SimpleLogAppender(string logDirectory, string baseName, FileMode fileMode, bool outFileDisabled, bool errFileDisabled, string outFilePattern, string errFilePattern)
            : base(logDirectory, baseName, outFileDisabled, errFileDisabled, outFilePattern, errFilePattern)
        {
            FileMode = fileMode;
            OutputLogFileName = BaseLogFileName + ".out.log";
            ErrorLogFileName = BaseLogFileName + ".err.log";
        }

        public override void log(Stream outputStream, Stream errorStream)
        {
            if (!OutFileDisabled)
                new Thread(() => CopyStream(outputStream, new FileStream(OutputLogFileName, FileMode))).Start();

            if (!ErrFileDisabled)
                new Thread(() => CopyStream(errorStream, new FileStream(ErrorLogFileName, FileMode))).Start();
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
            new Thread(() => CopyStream(outputStream, Stream.Null)).Start();
            new Thread(() => CopyStream(errorStream, Stream.Null)).Start();
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
            if (!OutFileDisabled)
                new Thread(() => CopyStreamWithDateRotation(outputStream, OutFilePattern)).Start();

            if (!ErrFileDisabled)
                new Thread(() => CopyStreamWithDateRotation(errorStream, ErrFilePattern)).Start();
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
                if (len == 0)
                    break;    // EOF

                if (periodicRollingCalendar.shouldRoll)
                {
                    // rotate at the line boundary
                    int offset = 0;
                    bool rolled = false;
                    for (int i = 0; i < len; i++)
                    {
                        if (buf[i] == 0x0A)
                        {
                            // at the line boundary.
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
                    {
                        // we didn't roll - most likely as we didnt find a line boundary, so we should log what we read and roll anyway.
                        w.Write(buf, 0, len);
                        w.Close();
                        w = new FileStream(BaseLogFileName + "_" + periodicRollingCalendar.format + ext, FileMode.Create);
                    }
                }
                else
                {
                    // typical case. write the whole thing into the current file
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

        public int SizeTheshold { get; private set; }

        public int FilesToKeep { get; private set; }

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
            if (!OutFileDisabled)
                new Thread(() => CopyStreamWithRotation(outputStream, OutFilePattern)).Start();

            if (!ErrFileDisabled)
                new Thread(() => CopyStreamWithRotation(errorStream, ErrFilePattern)).Start();
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
                if (len == 0)
                    break;    // EOF

                if (sz + len < SizeTheshold)
                {
                    // typical case. write the whole thing into the current file
                    w.Write(buf, 0, len);
                    sz += len;
                }
                else
                {
                    // rotate at the line boundary
                    int s = 0;
                    for (int i = 0; i < len; i++)
                    {
                        if (buf[i] != 0x0A)
                            continue;

                        if (sz + i < SizeTheshold)
                            continue;

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
            if (!OutFileDisabled)
                CopyFile(OutputLogFileName, OutputLogFileName + ".old");

            if (!ErrFileDisabled)
                CopyFile(ErrorLogFileName, ErrorLogFileName + ".old");

            base.log(outputStream, errorStream);
        }
    }

    public class RollingSizeTimeLogAppender : AbstractFileLogAppender
    {
        public static int BYTES_PER_KB = 1024;
        public int SizeTheshold { get; private set; }
        public string FilePattern { get; private set; }
        public TimeSpan? AutoRollAtTime { get; private set; }
        public int? ZipOlderThanNumDays { get; private set; }
        public string ZipDateFormat { get; private set; }

        public RollingSizeTimeLogAppender(
            string logDirectory,
            string baseName,
            bool outFileDisabled,
            bool errFileDisabled,
            string outFilePattern,
            string errFilePattern,
            int sizeThreshold,
            string filePattern,
            TimeSpan? autoRollAtTime,
            int? zipolderthannumdays,
            string zipdateformat)
            : base(logDirectory, baseName, outFileDisabled, errFileDisabled, outFilePattern, errFilePattern)
        {
            SizeTheshold = sizeThreshold;
            FilePattern = filePattern;
            AutoRollAtTime = autoRollAtTime;
            ZipOlderThanNumDays = zipolderthannumdays;
            ZipDateFormat = zipdateformat;
        }

        public override void log(Stream outputStream, Stream errorStream)
        {
            if (!OutFileDisabled)
                new Thread(() => CopyStreamWithRotation(outputStream, OutFilePattern)).Start();

            if (!ErrFileDisabled)
                new Thread(() => CopyStreamWithRotation(errorStream, ErrFilePattern)).Start();
        }

        private void CopyStreamWithRotation(Stream data, string extension)
        {
            // lock required as the timer thread and the thread that will write to the stream could try and access the file stream at the same time
            var fileLock = new object();

            var buf = new byte[1024];

            var baseDirectory = Path.GetDirectoryName(BaseLogFileName);
            var baseFileName = Path.GetFileName(BaseLogFileName);
            var logFile = BaseLogFileName + extension;

            var w = new FileStream(logFile, FileMode.Append);
            var sz = new FileInfo(logFile).Length;

            // We auto roll at time is configured then we need to create a timer and wait until time is elasped and roll the file over
            if (AutoRollAtTime != null)
            {
                // Run at start
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

                            var now = DateTime.Now.AddDays(-1);
                            var nextFileNumber = GetNextFileNumber(extension, baseDirectory, baseFileName, now);
                            var nextFileName = Path.Combine(baseDirectory, string.Format("{0}.{1}.#{2:D4}{3}", baseFileName, now.ToString(FilePattern), nextFileNumber, extension));
                            File.Move(logFile, nextFileName);

                            w = new FileStream(logFile, FileMode.Create);
                            sz = new FileInfo(logFile).Length;
                        }

                        // Next day so check if file can be zipped
                        ZipFiles(baseDirectory, extension, baseFileName);
                    }
                    catch (Exception et)
                    {
                        EventLogger.LogEvent(string.Format("Failed to to trigger auto roll at time event due to: {0}", et.Message));
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
                if (len == 0)
                    break;    // EOF

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
                                if (buf[i] != 0x0A)
                                    continue;

                                if (sz + i < SizeTheshold)
                                    continue;

                                // at the line boundary and exceeded the rotation unit.
                                // time to rotate.
                                w.Write(buf, s, i + 1);
                                w.Close();
                                s = i + 1;

                                // rotate file
                                var now = DateTime.Now;
                                var nextFileNumber = GetNextFileNumber(extension, baseDirectory, baseFileName, now);
                                var nextFileName =
                                    Path.Combine(baseDirectory,
                                        string.Format("{0}.{1}.#{2:D4}{3}", baseFileName, now.ToString(FilePattern), nextFileNumber, extension));
                                File.Move(logFile, nextFileName);

                                // even if the log rotation fails, create a new one, or else
                                // we'll infinitely try to rotate.
                                w = new FileStream(logFile, FileMode.Create);
                                sz = new FileInfo(logFile).Length;
                            }
                        }
                        catch (Exception e)
                        {
                            EventLogger.LogEvent(string.Format("Failed to roll size time log: {0}", e.Message));
                        }
                    }

                    w.Flush();
                }
            }

            data.Close();
            w.Close();
        }

        private void ZipFiles(string directory, string fileExtension, string zipFileBaseName)
        {
            if (ZipOlderThanNumDays == null || !(ZipOlderThanNumDays > 0))
                return;

            try
            {
                foreach (string path in Directory.GetFiles(directory, "*" + fileExtension))
                {
                    var fileInfo = new FileInfo(path);
                    if (fileInfo.LastWriteTimeUtc >= DateTime.UtcNow.AddDays(-ZipOlderThanNumDays.Value))
                        continue;

                    string sourceFileName = Path.GetFileName(path);
                    string zipFilePattern = fileInfo.LastAccessTimeUtc.ToString(ZipDateFormat);
                    string zipFilePath = Path.Combine(directory, $"{zipFileBaseName}.{zipFilePattern}.zip");
                    ZipOneFile(path, sourceFileName, zipFilePath);

                    File.Delete(path);
                }
            }
            catch (Exception e)
            {
                EventLogger.LogEvent($"Failed to Zip files. Error {e.Message}");
            }
        }

#if VNEXT
        private void ZipOneFile(string sourceFilePath, string entryName, string zipFilePath)
        {
            ZipArchive zipArchive = null;
            try
            {
                zipArchive = ZipFile.Open(zipFilePath, ZipArchiveMode.Update);

                if (zipArchive.GetEntry(entryName) is null)
                {
                    zipArchive.CreateEntryFromFile(sourceFilePath, entryName);
                }
            }
            catch (Exception e)
            {
                EventLogger.LogEvent($"Failed to Zip the File {sourceFilePath}. Error {e.Message}");
            }
            finally
            {
                zipArchive?.Dispose();
            }
        }
#else
        private void ZipOneFile(string sourceFilePath, string entryName, string zipFilePath)
        {
            ZipFile zipFile = null;
            try
            {
                zipFile = new ZipFile(File.Open(zipFilePath, FileMode.OpenOrCreate));
                zipFile.BeginUpdate();

                if (zipFile.FindEntry(entryName, false) < 0)
                {
                    zipFile.Add(sourceFilePath, entryName);
                }

                zipFile.CommitUpdate();
            }
            catch (Exception e)
            {
                EventLogger.LogEvent($"Failed to Zip the File {sourceFilePath}. Error {e.Message}");
                zipFile?.AbortUpdate();
            }
            finally
            {
                zipFile?.Close();
            }
        }
#endif

        private double SetupRollTimer()
        {
            var nowTime = DateTime.Now;
            var scheduledTime = new DateTime(nowTime.Year, nowTime.Month, nowTime.Day, AutoRollAtTime.Value.Hours,
                AutoRollAtTime.Value.Minutes, AutoRollAtTime.Value.Seconds, 0); // Specify your time HH,MM,SS
            if (nowTime > scheduledTime)
                scheduledTime = scheduledTime.AddDays(1);

            double tickTime = (scheduledTime - DateTime.Now).TotalMilliseconds;
            return tickTime;
        }

        private int GetNextFileNumber(string ext, string baseDirectory, string baseFileName, DateTime now)
        {
            var nextFileNumber = 0;
            var files = Directory.GetFiles(baseDirectory, string.Format("{0}.{1}.#*{2}", baseFileName, now.ToString(FilePattern), ext));
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
                        var hashIndex = filenameOnly.IndexOf('#');
                        var lastNumberAsString = filenameOnly.Substring(hashIndex + 1, 4);
                        // var lastNumberAsString = filenameOnly.Substring(filenameOnly.Length - 4, 4);
                        if (int.TryParse(lastNumberAsString, out int lastNumber))
                        {
                            if (lastNumber > nextFileNumber)
                                nextFileNumber = lastNumber;
                        }
                        else
                        {
                            throw new IOException(string.Format("File {0} does not follow the pattern provided", f));
                        }
                    }
                    catch (Exception e)
                    {
                        throw new IOException(string.Format("Failed to process file {0} due to error {1}", f, e.Message), e);
                    }
                }

                if (nextFileNumber == 0)
                    throw new IOException("Cannot roll the file because matching pattern not found");

                nextFileNumber++;
            }

            return nextFileNumber;
        }
    }
}
