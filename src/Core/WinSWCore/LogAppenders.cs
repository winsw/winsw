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
using winsw.Util;

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
        public abstract void log(StreamReader outputReader, StreamReader errorReader);

        /// <summary>
        /// Error and information about logging should be reported here.
        /// </summary>
#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        public EventLogger EventLogger { get; set; }
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.

        /// <summary>
        /// Convenience method to copy stuff from StreamReader to StreamWriter
        /// </summary>
        protected void CopyStream(StreamReader reader, StreamWriter writer)
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                writer.WriteLine(line);
            }

            reader.Dispose();
            writer.Dispose();
        }

        /// <summary>
        /// File replacement.
        /// </summary>
        protected void MoveFile(string sourceFileName, string destFileName)
        {
            try
            {
                FileHelper.MoveOrReplaceFile(sourceFileName, destFileName);
            }
            catch (IOException e)
            {
                EventLogger.LogEvent("Failed to move :" + sourceFileName + " to " + destFileName + " because " + e.Message);
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

        protected StreamWriter CreateWriter(FileStream stream) => new StreamWriter(stream) { AutoFlush = true };
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

        public override void log(StreamReader outputReader, StreamReader errorReader)
        {
            if (!OutFileDisabled)
                new Thread(() => CopyStream(outputReader, CreateWriter(new FileStream(OutputLogFileName, FileMode)))).Start();

            if (!ErrFileDisabled)
                new Thread(() => CopyStream(errorReader, CreateWriter(new FileStream(ErrorLogFileName, FileMode)))).Start();
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
        public override void log(StreamReader outputReader, StreamReader errorReader)
        {
            new Thread(() => CopyStream(outputReader, StreamWriter.Null)).Start();
            new Thread(() => CopyStream(errorReader, StreamWriter.Null)).Start();
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

        public override void log(StreamReader outputReader, StreamReader errorReader)
        {
            if (!OutFileDisabled)
                new Thread(() => CopyStreamWithDateRotation(outputReader, OutFilePattern)).Start();

            if (!ErrFileDisabled)
                new Thread(() => CopyStreamWithDateRotation(errorReader, ErrFilePattern)).Start();
        }

        /// <summary>
        /// Works like the CopyStream method but does a log rotation based on time.
        /// </summary>
        private void CopyStreamWithDateRotation(StreamReader reader, string ext)
        {
            PeriodicRollingCalendar periodicRollingCalendar = new PeriodicRollingCalendar(Pattern, Period);
            periodicRollingCalendar.init();

            StreamWriter writer = CreateWriter(new FileStream(BaseLogFileName + "_" + periodicRollingCalendar.format + ext, FileMode.Append));
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (periodicRollingCalendar.shouldRoll)
                {
                    writer.Dispose();
                    writer = CreateWriter(new FileStream(BaseLogFileName + "_" + periodicRollingCalendar.format + ext, FileMode.Create));
                }

                writer.WriteLine(line);
            }

            reader.Dispose();
            writer.Dispose();
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

        public override void log(StreamReader outputReader, StreamReader errorReader)
        {
            if (!OutFileDisabled)
                new Thread(() => CopyStreamWithRotation(outputReader, OutFilePattern)).Start();

            if (!ErrFileDisabled)
                new Thread(() => CopyStreamWithRotation(errorReader, ErrFilePattern)).Start();
        }

        /// <summary>
        /// Works like the CopyStream method but does a log rotation.
        /// </summary>
        private void CopyStreamWithRotation(StreamReader reader, string ext)
        {
            StreamWriter writer = CreateWriter(new FileStream(BaseLogFileName + ext, FileMode.Append));
            long fileLength = new FileInfo(BaseLogFileName + ext).Length;

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                int lengthToWrite = (line.Length + Environment.NewLine.Length) * sizeof(char);
                if (fileLength + lengthToWrite > SizeTheshold)
                {
                    writer.Dispose();

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
                    writer = CreateWriter(new FileStream(BaseLogFileName + ext, FileMode.Create));
                    fileLength = new FileInfo(BaseLogFileName + ext).Length;
                }

                writer.WriteLine(line);
                fileLength += lengthToWrite;
            }

            reader.Dispose();
            writer.Dispose();
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

        public override void log(StreamReader outputReader, StreamReader errorReader)
        {
            if (!OutFileDisabled)
                MoveFile(OutputLogFileName, OutputLogFileName + ".old");

            if (!ErrFileDisabled)
                MoveFile(ErrorLogFileName, ErrorLogFileName + ".old");

            base.log(outputReader, errorReader);
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

        public override void log(StreamReader outputReader, StreamReader errorReader)
        {
            if (!OutFileDisabled)
                new Thread(() => CopyStreamWithRotation(outputReader, OutFilePattern)).Start();

            if (!ErrFileDisabled)
                new Thread(() => CopyStreamWithRotation(errorReader, ErrFilePattern)).Start();
        }

        private void CopyStreamWithRotation(StreamReader reader, string extension)
        {
            // lock required as the timer thread and the thread that will write to the stream could try and access the file stream at the same time
            var fileLock = new object();

            var baseDirectory = Path.GetDirectoryName(BaseLogFileName)!;
            var baseFileName = Path.GetFileName(BaseLogFileName);
            var logFile = BaseLogFileName + extension;

            var writer = CreateWriter(new FileStream(logFile, FileMode.Append));
            var fileLength = new FileInfo(logFile).Length;

            // We auto roll at time is configured then we need to create a timer and wait until time is elasped and roll the file over
            if (AutoRollAtTime is TimeSpan autoRollAtTime)
            {
                // Run at start
                var tickTime = SetupRollTimer(autoRollAtTime);
                var timer = new System.Timers.Timer(tickTime);
                timer.Elapsed += (s, e) =>
                {
                    try
                    {
                        timer.Stop();
                        lock (fileLock)
                        {
                            writer.Dispose();

                            var now = DateTime.Now.AddDays(-1);
                            var nextFileNumber = GetNextFileNumber(extension, baseDirectory, baseFileName, now);
                            var nextFileName = Path.Combine(baseDirectory, string.Format("{0}.{1}.#{2:D4}{3}", baseFileName, now.ToString(FilePattern), nextFileNumber, extension));
                            File.Move(logFile, nextFileName);

                            writer = CreateWriter(new FileStream(logFile, FileMode.Create));
                            fileLength = new FileInfo(logFile).Length;
                        }

                        // Next day so check if file can be zipped
                        ZipFiles(baseDirectory, extension, baseFileName);
                    }
                    catch (Exception ex)
                    {
                        EventLogger.LogEvent($"Failed to to trigger auto roll at time event due to: {ex.Message}");
                    }
                    finally
                    {
                        // Recalculate the next interval
                        timer.Interval = SetupRollTimer(autoRollAtTime);
                        timer.Start();
                    }
                };
                timer.Start();
            }

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                lock (fileLock)
                {
                    int lengthToWrite = (line.Length + Environment.NewLine.Length) * sizeof(char);
                    if (fileLength + lengthToWrite > SizeTheshold)
                    {
                        try
                        {
                            // rotate file
                            var now = DateTime.Now;
                            var nextFileNumber = GetNextFileNumber(extension, baseDirectory, baseFileName, now);
                            var nextFileName =
                                Path.Combine(baseDirectory,
                                    string.Format("{0}.{1}.#{2:D4}{3}", baseFileName, now.ToString(FilePattern), nextFileNumber, extension));
                            File.Move(logFile, nextFileName);

                            // even if the log rotation fails, create a new one, or else
                            // we'll infinitely try to rotate.
                            writer = CreateWriter(new FileStream(logFile, FileMode.Create));
                            fileLength = new FileInfo(logFile).Length;
                        }
                        catch (Exception e)
                        {
                            EventLogger.LogEvent($"Failed to roll size time log: {e.Message}");
                        }
                    }

                    writer.WriteLine(line);
                    fileLength += lengthToWrite;
                }
            }

            reader.Dispose();
            writer.Dispose();
        }

        private void ZipFiles(string directory, string fileExtension, string zipFileBaseName)
        {
            if (ZipOlderThanNumDays is null || ZipOlderThanNumDays <= 0)
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
            ZipArchive? zipArchive = null;
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
            ZipFile? zipFile = null;
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

        private double SetupRollTimer(TimeSpan autoRollAtTime)
        {
            var nowTime = DateTime.Now;
            var scheduledTime = new DateTime(
                nowTime.Year,
                nowTime.Month,
                nowTime.Day,
                autoRollAtTime.Hours,
                autoRollAtTime.Minutes,
                autoRollAtTime.Seconds,
                0);
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
                            throw new IOException($"File {f} does not follow the pattern provided");
                        }
                    }
                    catch (Exception e)
                    {
                        throw new IOException($"Failed to process file {f} due to error {e.Message}", e);
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
