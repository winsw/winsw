using System;
using System.Diagnostics;
#if VNEXT
using System.IO.Compression;
#endif
using System.IO;
using System.Text;
using System.Threading;
#if !VNEXT
using ICSharpCode.SharpZipLib.Zip;
#endif
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
#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        protected LogHandler(bool outFileDisabled, bool errFileDisabled)
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        {
            this.OutFileDisabled = outFileDisabled;
            this.ErrFileDisabled = errFileDisabled;
        }

        /// <summary>
        /// Error and information about logging should be reported here.
        /// </summary>
        public IEventLogger EventLogger { get; set; }

        public bool OutFileDisabled { get; }

        public bool ErrFileDisabled { get; }

        public abstract void Log(StreamReader outputReader, StreamReader errorReader);

        /// <summary>
        /// Convenience method to copy stuff from StreamReader to StreamWriter
        /// </summary>
        protected void CopyStream(Stream reader, Stream writer)
        {
            var copy = new StreamCopyOperation(reader, writer);
            while (copy.CopyLine() != 0)
            {
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
        protected string BaseLogFileName { get; private set; }

        protected string OutFilePattern { get; private set; }

        protected string ErrFilePattern { get; private set; }

        protected AbstractFileLogAppender(string logDirectory, string baseName, bool outFileDisabled, bool errFileDisabled, string outFilePattern, string errFilePattern)
            : base(outFileDisabled, errFileDisabled)
        {
            this.BaseLogFileName = Path.Combine(logDirectory, baseName);
            this.OutFilePattern = outFilePattern;
            this.ErrFilePattern = errFilePattern;
        }

        public override void Log(StreamReader outputReader, StreamReader errorReader)
        {
            if (!this.OutFileDisabled)
            {
                this.LogOutput(outputReader);
            }

            if (!this.ErrFileDisabled)
            {
                this.LogError(errorReader);
            }
        }

        protected abstract void LogOutput(StreamReader outputReader);

        protected abstract void LogError(StreamReader errorReader);
    }

    public abstract class SimpleLogAppender : AbstractFileLogAppender
    {
        public FileMode FileMode { get; private set; }

        public string OutputLogFileName { get; private set; }

        public string ErrorLogFileName { get; private set; }

        protected SimpleLogAppender(string logDirectory, string baseName, FileMode fileMode, bool outFileDisabled, bool errFileDisabled, string outFilePattern, string errFilePattern)
            : base(logDirectory, baseName, outFileDisabled, errFileDisabled, outFilePattern, errFilePattern)
        {
            this.FileMode = fileMode;
            this.OutputLogFileName = this.BaseLogFileName + ".out.log";
            this.ErrorLogFileName = this.BaseLogFileName + ".err.log";
        }

        protected override void LogOutput(StreamReader outputReader)
        {
            new Thread(() => this.CopyStream(outputReader.BaseStream, new FileStream(this.OutputLogFileName, this.FileMode))).Start();
        }

        protected override void LogError(StreamReader errorReader)
        {
            new Thread(() => this.CopyStream(errorReader.BaseStream, new FileStream(this.ErrorLogFileName, this.FileMode))).Start();
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
        public IgnoreLogAppender()
            : base(true, true)
        {
        }

        public override void Log(StreamReader outputReader, StreamReader errorReader)
        {
        }
    }

    public class TimeBasedRollingLogAppender : AbstractFileLogAppender
    {
        public string Pattern { get; private set; }

        public int Period { get; private set; }

        public TimeBasedRollingLogAppender(string logDirectory, string baseName, bool outFileDisabled, bool errFileDisabled, string outFilePattern, string errFilePattern, string pattern, int period)
            : base(logDirectory, baseName, outFileDisabled, errFileDisabled, outFilePattern, errFilePattern)
        {
            this.Pattern = pattern;
            this.Period = period;
        }

        protected override void LogOutput(StreamReader outputReader)
        {
            new Thread(() => this.CopyStreamWithDateRotation(outputReader, this.OutFilePattern)).Start();
        }

        protected override void LogError(StreamReader errorReader)
        {
            new Thread(() => this.CopyStreamWithDateRotation(errorReader, this.ErrFilePattern)).Start();
        }

        /// <summary>
        /// Works like the CopyStream method but does a log rotation based on time.
        /// </summary>
        private void CopyStreamWithDateRotation(StreamReader reader, string ext)
        {
            var periodicRollingCalendar = new PeriodicRollingCalendar(this.Pattern, this.Period);
            periodicRollingCalendar.Init();

            var writer = new FileStream(this.BaseLogFileName + "_" + periodicRollingCalendar.Format + ext, FileMode.Append);
            var copy = new StreamCopyOperation(reader.BaseStream, writer);
            while (copy.CopyLine() != 0)
            {
                if (periodicRollingCalendar.ShouldRoll)
                {
                    writer.Dispose();
                    copy.Writer = writer = new FileStream(this.BaseLogFileName + "_" + periodicRollingCalendar.Format + ext, FileMode.Create);
                }
            }

            reader.Dispose();
            writer.Dispose();
        }
    }

    public class SizeBasedRollingLogAppender : AbstractFileLogAppender
    {
        public static int BytesPerKB = 1024;
        public static int BytesPerMB = 1024 * BytesPerKB;
        public static int DefaultSizeThreshold = 10 * BytesPerMB; // roll every 10MB.
        public static int DefaultFilesToKeep = 8;

        public int SizeThreshold { get; private set; }

        public int FilesToKeep { get; private set; }

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

        protected override void LogOutput(StreamReader outputReader)
        {
            new Thread(() => this.CopyStreamWithRotation(outputReader, this.OutFilePattern)).Start();
        }

        protected override void LogError(StreamReader errorReader)
        {
            new Thread(() => this.CopyStreamWithRotation(errorReader, this.ErrFilePattern)).Start();
        }

        /// <summary>
        /// Works like the CopyStream method but does a log rotation.
        /// </summary>
        private void CopyStreamWithRotation(StreamReader reader, string ext)
        {
            var writer = new FileStream(this.BaseLogFileName + ext, FileMode.Append);
            var copy = new StreamCopyOperation(reader.BaseStream, writer);
            long fileLength = new FileInfo(this.BaseLogFileName + ext).Length;

            int written;
            while ((written = copy.CopyLine()) != 0)
            {
                fileLength += written;
                if (fileLength > this.SizeThreshold)
                {
                    writer.Dispose();

                    try
                    {
                        for (int j = this.FilesToKeep; j >= 2; j--)
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
                    copy.Writer = writer = new FileStream(this.BaseLogFileName + ext, FileMode.Create);
                    fileLength = new FileInfo(this.BaseLogFileName + ext).Length;
                }
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
        public static int BytesPerKB = 1024;

        public int SizeThreshold { get; private set; }

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
            this.SizeThreshold = sizeThreshold;
            this.FilePattern = filePattern;
            this.AutoRollAtTime = autoRollAtTime;
            this.ZipOlderThanNumDays = zipolderthannumdays;
            this.ZipDateFormat = zipdateformat;
        }

        protected override void LogOutput(StreamReader outputReader)
        {
            new Thread(() => this.CopyStreamWithRotation(outputReader, this.OutFilePattern)).Start();
        }

        protected override void LogError(StreamReader errorReader)
        {
            new Thread(() => this.CopyStreamWithRotation(errorReader, this.ErrFilePattern)).Start();
        }

        private void CopyStreamWithRotation(StreamReader reader, string extension)
        {
            // lock required as the timer thread and the thread that will write to the stream could try and access the file stream at the same time
            object fileLock = new();

            string baseDirectory = Path.GetDirectoryName(this.BaseLogFileName)!;
            string baseFileName = Path.GetFileName(this.BaseLogFileName);
            string logFile = this.BaseLogFileName + extension;

            var writer = new FileStream(logFile, FileMode.Append);
            var copy = new StreamCopyOperation(reader.BaseStream, writer);
            long fileLength = new FileInfo(logFile).Length;

            // We auto roll at time is configured then we need to create a timer and wait until time is elasped and roll the file over
            if (this.AutoRollAtTime is TimeSpan autoRollAtTime)
            {
                // Run at start
                double tickTime = this.SetupRollTimer(autoRollAtTime);
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
                            int nextFileNumber = this.GetNextFileNumber(extension, baseDirectory, baseFileName, now);
                            string nextFileName = Path.Combine(baseDirectory, string.Format("{0}.{1}.#{2:D4}{3}", baseFileName, now.ToString(this.FilePattern), nextFileNumber, extension));
                            File.Move(logFile, nextFileName);

                            copy.Writer = writer = new FileStream(logFile, FileMode.Create);
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

            int written;
            while ((written = copy.CopyLine()) != 0)
            {
                lock (fileLock)
                {
                    fileLength += written;
                    if (fileLength > this.SizeThreshold)
                    {
                        try
                        {
                            // roll file
                            var now = DateTime.Now;
                            int nextFileNumber = this.GetNextFileNumber(extension, baseDirectory, baseFileName, now);
                            string nextFileName = Path.Combine(
                                baseDirectory,
                                string.Format("{0}.{1}.#{2:D4}{3}", baseFileName, now.ToString(this.FilePattern), nextFileNumber, extension));
                            File.Move(logFile, nextFileName);

                            // even if the log rotation fails, create a new one, or else
                            // we'll infinitely try to roll.
                            copy.Writer = writer = new FileStream(logFile, FileMode.Create);
                            fileLength = new FileInfo(logFile).Length;
                        }
                        catch (Exception e)
                        {
                            this.EventLogger.LogEvent($"Failed to roll size time log: {e.Message}");
                        }
                    }
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
                this.EventLogger.LogEvent($"Failed to Zip the File {sourceFilePath}. Error {e.Message}");
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
                this.EventLogger.LogEvent($"Failed to Zip the File {sourceFilePath}. Error {e.Message}");
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
            {
                scheduledTime = scheduledTime.AddDays(1);
            }

            double tickTime = (scheduledTime - DateTime.Now).TotalMilliseconds;
            return tickTime;
        }

        private int GetNextFileNumber(string ext, string baseDirectory, string baseFileName, DateTime now)
        {
            int nextFileNumber = 0;
            string[] files = Directory.GetFiles(baseDirectory, string.Format("{0}.{1}.#*{2}", baseFileName, now.ToString(this.FilePattern), ext));
            if (files.Length == 0)
            {
                nextFileNumber = 1;
            }
            else
            {
                foreach (string f in files)
                {
                    try
                    {
                        string filenameOnly = Path.GetFileNameWithoutExtension(f);
                        int hashIndex = filenameOnly.IndexOf('#');
                        string lastNumberAsString = filenameOnly.Substring(hashIndex + 1, 4);
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

    internal sealed class StreamCopyOperation
    {
        private const int BufferSize = 1024;

        private readonly byte[] buffer;
        private readonly Stream reader;

        private int startIndex;
        private int endIndex;

        internal Stream Writer;

        internal StreamCopyOperation(Stream reader, Stream writer)
        {
            this.buffer = new byte[BufferSize];
            this.reader = reader;
            this.startIndex = 0;
            this.endIndex = 0;
            this.Writer = writer;
        }

        internal int CopyLine()
        {
            byte[] buffer = this.buffer;
            var source = this.reader;
            int startIndex = this.startIndex;
            int endIndex = this.endIndex;
            var destination = this.Writer;

            int total = 0;
            while (true)
            {
                if (startIndex == 0)
                {
                    if ((endIndex = source.Read(buffer, 0, BufferSize)) == 0)
                    {
                        break;
                    }
                }

                int buffered = endIndex - startIndex;

                int newLineIndex = Array.IndexOf(buffer, (byte)'\n', startIndex, buffered);
                if (newLineIndex >= 0)
                {
                    int count = newLineIndex - startIndex + 1;
                    total += count;
                    destination.Write(buffer, startIndex, count);
                    destination.Flush();
                    startIndex = (newLineIndex + 1) % BufferSize;
                    break;
                }

                total += buffered;
                destination.Write(buffer, startIndex, buffered);
                destination.Flush();
                startIndex = 0;
            }

            this.startIndex = startIndex;
            this.endIndex = endIndex;

            return total;
        }
    }
}
