using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using WinSW.Util;

namespace WinSW
{
    public interface IEventLogger
    {
        void LogEvent(string message);

        void LogEvent(string message, EventLogEntryType type);
    }

    /// <summary>
    /// Abstraction for handling log.
    /// </summary>
    public abstract class LogHandler
    {
        public abstract void Log(StreamReader outputReader, StreamReader errorReader);

        /// <summary>
        /// Error and information about logging should be reported here.
        /// </summary>
#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        public IEventLogger EventLogger { get; set; }
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.

        /// <summary>
        /// Convenience method to copy stuff from StreamReader to StreamWriter
        /// </summary>
        protected async Task CopyStreamAsync(StreamReader reader, StreamWriter writer)
        {
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
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
                this.EventLogger.LogEvent("Failed to move :" + sourceFileName + " to " + destFileName + " because " + e.Message);
            }
        }
    }

    /// <summary>
    /// Base class for file-based loggers
    /// </summary>
    public abstract class AbstractFileLogAppender : LogHandler
    {
        protected string BaseLogFileName { get; }

        protected bool OutFileDisabled { get; }

        protected bool ErrFileDisabled { get; }

        protected string OutFilePattern { get; }

        protected string ErrFilePattern { get; }

        protected AbstractFileLogAppender(string logDirectory, string baseName, bool outFileDisabled, bool errFileDisabled, string outFilePattern, string errFilePattern)
        {
            this.BaseLogFileName = Path.Combine(logDirectory, baseName);
            this.OutFileDisabled = outFileDisabled;
            this.OutFilePattern = outFilePattern;
            this.ErrFileDisabled = errFileDisabled;
            this.ErrFilePattern = errFilePattern;
        }

        public override void Log(StreamReader outputReader, StreamReader errorReader)
        {
            if (this.OutFileDisabled)
            {
                outputReader.Dispose();
            }
            else
            {
                this.SafeLogOutput(outputReader);
            }

            if (this.ErrFileDisabled)
            {
                errorReader.Dispose();
            }
            else
            {
                this.SafeLogError(errorReader);
            }
        }

        protected StreamWriter CreateWriter(FileStream stream) => new StreamWriter(stream) { AutoFlush = true };

        protected abstract Task LogOutput(StreamReader outputReader);

        protected abstract Task LogError(StreamReader errorReader);

        private async void SafeLogOutput(StreamReader outputReader)
        {
            try
            {
                await this.LogOutput(outputReader);
            }
            catch (Exception e)
            {
                this.EventLogger.LogEvent("Unhandled exception in task. " + e, EventLogEntryType.Error);
            }
        }

        private async void SafeLogError(StreamReader errorReader)
        {
            try
            {
                await this.LogError(errorReader);
            }
            catch (Exception e)
            {
                this.EventLogger.LogEvent("Unhandled exception in task. " + e, EventLogEntryType.Error);
            }
        }
    }

    public abstract class SimpleLogAppender : AbstractFileLogAppender
    {
        public FileMode FileMode { get; }

        public string OutputLogFileName { get; }

        public string ErrorLogFileName { get; }

        protected SimpleLogAppender(string logDirectory, string baseName, FileMode fileMode, bool outFileDisabled, bool errFileDisabled, string outFilePattern, string errFilePattern)
            : base(logDirectory, baseName, outFileDisabled, errFileDisabled, outFilePattern, errFilePattern)
        {
            this.FileMode = fileMode;
            this.OutputLogFileName = this.BaseLogFileName + ".out.log";
            this.ErrorLogFileName = this.BaseLogFileName + ".err.log";
        }

        protected override Task LogOutput(StreamReader outputReader)
        {
            return this.CopyStreamAsync(outputReader, this.CreateWriter(new FileStream(this.OutputLogFileName, this.FileMode)));
        }

        protected override Task LogError(StreamReader errorReader)
        {
            return this.CopyStreamAsync(errorReader, this.CreateWriter(new FileStream(this.ErrorLogFileName, this.FileMode)));
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
        public override void Log(StreamReader outputReader, StreamReader errorReader)
        {
            outputReader.Dispose();
            errorReader.Dispose();
        }
    }

    public class TimeBasedRollingLogAppender : AbstractFileLogAppender
    {
        public string Pattern { get; }

        public int Period { get; }

        public TimeBasedRollingLogAppender(string logDirectory, string baseName, bool outFileDisabled, bool errFileDisabled, string outFilePattern, string errFilePattern, string pattern, int period)
            : base(logDirectory, baseName, outFileDisabled, errFileDisabled, outFilePattern, errFilePattern)
        {
            this.Pattern = pattern;
            this.Period = period;
        }

        protected override Task LogOutput(StreamReader outputReader)
        {
            return this.CopyStreamWithDateRotationAsync(outputReader, this.OutFilePattern);
        }

        protected override Task LogError(StreamReader errorReader)
        {
            return this.CopyStreamWithDateRotationAsync(errorReader, this.ErrFilePattern);
        }

        /// <summary>
        /// Works like the CopyStream method but does a log rotation based on time.
        /// </summary>
        private async Task CopyStreamWithDateRotationAsync(StreamReader reader, string ext)
        {
            PeriodicRollingCalendar periodicRollingCalendar = new PeriodicRollingCalendar(this.Pattern, this.Period);
            periodicRollingCalendar.Init();

            StreamWriter writer = this.CreateWriter(new FileStream(this.BaseLogFileName + "_" + periodicRollingCalendar.Format + ext, FileMode.Append));
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (periodicRollingCalendar.ShouldRoll)
                {
                    writer.Dispose();
                    writer = this.CreateWriter(new FileStream(this.BaseLogFileName + "_" + periodicRollingCalendar.Format + ext, FileMode.Create));
                }

                writer.WriteLine(line);
            }

            reader.Dispose();
            writer.Dispose();
        }
    }

    public class SizeBasedRollingLogAppender : AbstractFileLogAppender
    {
        public const int BytesPerKB = 1024;
        public const int BytesPerMB = 1024 * BytesPerKB;
        public const int DefaultSizeThreshold = 10 * BytesPerMB; // roll every 10MB.
        public const int DefaultFilesToKeep = 8;

        public int SizeThreshold { get; }

        public int FilesToKeep { get; }

        public SizeBasedRollingLogAppender(string logDirectory, string baseName, bool outFileDisabled, bool errFileDisabled, string outFilePattern, string errFilePattern, int sizeThreshold, int filesToKeep)
            : base(logDirectory, baseName, outFileDisabled, errFileDisabled, outFilePattern, errFilePattern)
        {
            this.SizeThreshold = sizeThreshold;
            this.FilesToKeep = filesToKeep;
        }

        public SizeBasedRollingLogAppender(string logDirectory, string baseName, bool outFileDisabled, bool errFileDisabled, string outFilePattern, string errFilePattern)
            : this(logDirectory, baseName, outFileDisabled, errFileDisabled, outFilePattern, errFilePattern, DefaultSizeThreshold, DefaultFilesToKeep)
        {
        }

        protected override Task LogOutput(StreamReader outputReader)
        {
            return this.CopyStreamWithRotationAsync(outputReader, this.OutFilePattern);
        }

        protected override Task LogError(StreamReader errorReader)
        {
            return this.CopyStreamWithRotationAsync(errorReader, this.ErrFilePattern);
        }

        /// <summary>
        /// Works like the CopyStream method but does a log rotation.
        /// </summary>
        private async Task CopyStreamWithRotationAsync(StreamReader reader, string ext)
        {
            StreamWriter writer = this.CreateWriter(new FileStream(this.BaseLogFileName + ext, FileMode.Append));
            long fileLength = new FileInfo(this.BaseLogFileName + ext).Length;

            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                int lengthToWrite = (line.Length + Environment.NewLine.Length) * sizeof(char);
                if (fileLength + lengthToWrite > this.SizeThreshold)
                {
                    writer.Dispose();

                    try
                    {
                        for (int j = this.FilesToKeep; j >= 1; j--)
                        {
                            string dst = this.BaseLogFileName + "." + (j - 1) + ext;
                            string src = this.BaseLogFileName + "." + (j - 2) + ext;
                            if (File.Exists(dst))
                            {
                                File.Delete(dst);
                            }

                            if (File.Exists(src))
                            {
                                File.Move(src, dst);
                            }
                        }

                        File.Move(this.BaseLogFileName + ext, this.BaseLogFileName + ".0" + ext);
                    }
                    catch (IOException e)
                    {
                        this.EventLogger.LogEvent("Failed to roll log: " + e.Message);
                    }

                    // even if the log rotation fails, create a new one, or else
                    // we'll infinitely try to roll.
                    writer = this.CreateWriter(new FileStream(this.BaseLogFileName + ext, FileMode.Create));
                    fileLength = new FileInfo(this.BaseLogFileName + ext).Length;
                }

                writer.WriteLine(line);
                fileLength += lengthToWrite;
            }

            reader.Dispose();
            writer.Dispose();
        }
    }

    /// <summary>
    /// Roll log when a service is newly started.
    /// </summary>
    public class RollingLogAppender : SimpleLogAppender
    {
        public RollingLogAppender(string logDirectory, string baseName, bool outFileDisabled, bool errFileDisabled, string outFilePattern, string errFilePattern)
            : base(logDirectory, baseName, FileMode.Append, outFileDisabled, errFileDisabled, outFilePattern, errFilePattern)
        {
        }

        public override void Log(StreamReader outputReader, StreamReader errorReader)
        {
            if (!this.OutFileDisabled)
            {
                this.MoveFile(this.OutputLogFileName, this.OutputLogFileName + ".old");
            }

            if (!this.ErrFileDisabled)
            {
                this.MoveFile(this.ErrorLogFileName, this.ErrorLogFileName + ".old");
            }

            base.Log(outputReader, errorReader);
        }
    }

    public class RollingSizeTimeLogAppender : AbstractFileLogAppender
    {
        public const int BytesPerKB = 1024;

        public int SizeThreshold { get; }

        public string FilePattern { get; }

        public TimeSpan? AutoRollAtTime { get; }

        public int? ZipOlderThanNumDays { get; }

        public string ZipDateFormat { get; }

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
            this.SizeThreshold = sizeThreshold;
            this.FilePattern = filePattern;
            this.AutoRollAtTime = autoRollAtTime;
            this.ZipOlderThanNumDays = zipolderthannumdays;
            this.ZipDateFormat = zipdateformat;
        }

        protected override Task LogOutput(StreamReader outputReader)
        {
            return this.CopyStreamWithRotationAsync(outputReader, this.OutFilePattern);
        }

        protected override Task LogError(StreamReader errorReader)
        {
            return this.CopyStreamWithRotationAsync(errorReader, this.ErrFilePattern);
        }

        private async Task CopyStreamWithRotationAsync(StreamReader reader, string extension)
        {
            // lock required as the timer thread and the thread that will write to the stream could try and access the file stream at the same time
            var fileLock = new object();

            var baseDirectory = Path.GetDirectoryName(this.BaseLogFileName)!;
            var baseFileName = Path.GetFileName(this.BaseLogFileName);
            var logFile = this.BaseLogFileName + extension;

            var writer = this.CreateWriter(new FileStream(logFile, FileMode.Append));
            var fileLength = new FileInfo(logFile).Length;

            // We auto roll at time is configured then we need to create a timer and wait until time is elasped and roll the file over
            if (this.AutoRollAtTime is TimeSpan autoRollAtTime)
            {
                // Run at start
                var tickTime = this.SetupRollTimer(autoRollAtTime);
                var timer = new System.Timers.Timer(tickTime);
                timer.Elapsed += (_, _) =>
                {
                    try
                    {
                        timer.Stop();
                        lock (fileLock)
                        {
                            writer.Dispose();

                            var now = DateTime.Now.AddDays(-1);
                            var nextFileNumber = this.GetNextFileNumber(extension, baseDirectory, baseFileName, now);
                            var nextFileName = Path.Combine(baseDirectory, string.Format("{0}.{1}.#{2:D4}{3}", baseFileName, now.ToString(this.FilePattern), nextFileNumber, extension));
                            File.Move(logFile, nextFileName);

                            writer = this.CreateWriter(new FileStream(logFile, FileMode.Create));
                            fileLength = new FileInfo(logFile).Length;
                        }

                        // Next day so check if file can be zipped
                        this.ZipFiles(baseDirectory, extension, baseFileName);
                    }
                    catch (Exception ex)
                    {
                        this.EventLogger.LogEvent($"Failed to to trigger auto roll at time event due to: {ex.Message}");
                    }
                    finally
                    {
                        // Recalculate the next interval
                        timer.Interval = this.SetupRollTimer(autoRollAtTime);
                        timer.Start();
                    }
                };
                timer.Start();
            }

            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                lock (fileLock)
                {
                    int lengthToWrite = (line.Length + Environment.NewLine.Length) * sizeof(char);
                    if (fileLength + lengthToWrite > this.SizeThreshold)
                    {
                        try
                        {
                            // roll file
                            var now = DateTime.Now;
                            var nextFileNumber = this.GetNextFileNumber(extension, baseDirectory, baseFileName, now);
                            var nextFileName = Path.Combine(
                                    baseDirectory,
                                    string.Format("{0}.{1}.#{2:D4}{3}", baseFileName, now.ToString(this.FilePattern), nextFileNumber, extension));
                            File.Move(logFile, nextFileName);

                            // even if the log rotation fails, create a new one, or else
                            // we'll infinitely try to roll.
                            writer = this.CreateWriter(new FileStream(logFile, FileMode.Create));
                            fileLength = new FileInfo(logFile).Length;
                        }
                        catch (Exception e)
                        {
                            this.EventLogger.LogEvent($"Failed to roll size time log: {e.Message}");
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
            if (this.ZipOlderThanNumDays is null || this.ZipOlderThanNumDays <= 0)
            {
                return;
            }

            try
            {
                foreach (string path in Directory.GetFiles(directory, "*" + fileExtension))
                {
                    var fileInfo = new FileInfo(path);
                    if (fileInfo.LastWriteTimeUtc >= DateTime.UtcNow.AddDays(-this.ZipOlderThanNumDays.Value))
                    {
                        continue;
                    }

                    string sourceFileName = Path.GetFileName(path);
                    string zipFilePattern = fileInfo.LastAccessTimeUtc.ToString(this.ZipDateFormat);
                    string zipFilePath = Path.Combine(directory, $"{zipFileBaseName}.{zipFilePattern}.zip");
                    this.ZipOneFile(path, sourceFileName, zipFilePath);

                    File.Delete(path);
                }
            }
            catch (Exception e)
            {
                this.EventLogger.LogEvent($"Failed to Zip files. Error {e.Message}");
            }
        }

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
                this.EventLogger.LogEvent($"Failed to Zip the File {sourceFilePath}. Error {e.Message}");
            }
            finally
            {
                zipArchive?.Dispose();
            }
        }

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
            {
                scheduledTime = scheduledTime.AddDays(1);
            }

            double tickTime = (scheduledTime - DateTime.Now).TotalMilliseconds;
            return tickTime;
        }

        private int GetNextFileNumber(string ext, string baseDirectory, string baseFileName, DateTime now)
        {
            var nextFileNumber = 0;
            var files = Directory.GetFiles(baseDirectory, string.Format("{0}.{1}.#*{2}", baseFileName, now.ToString(this.FilePattern), ext));
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
                        if (int.TryParse(lastNumberAsString, out int lastNumber))
                        {
                            if (lastNumber > nextFileNumber)
                            {
                                nextFileNumber = lastNumber;
                            }
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
                {
                    throw new IOException("Cannot roll the file because matching pattern not found");
                }

                nextFileNumber++;
            }

            return nextFileNumber;
        }
    }
}
