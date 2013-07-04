using System.IO;
using System.Diagnostics;
using System.Threading;

namespace winsw
{
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
        private readonly string baseLogFileName;

        public LogHandler(string logDirectory, string baseName)
        {
            baseLogFileName = Path.Combine(logDirectory, baseName);
        }

        public abstract void log(Stream outputStream, Stream errorStream);

        public EventLogger EventLogger { get; set; }

        public string BaseLogFileName
        {
            get
            {
                return baseLogFileName;
            }
        }

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

    public abstract class SimpleLogAppender : LogHandler
    {

        private FileMode fileMode;

        public SimpleLogAppender(string logDirectory, string baseName, FileMode fileMode)
            : base(logDirectory, baseName)
        {
            this.fileMode = fileMode;
            OutputLogFileName = BaseLogFileName + ".out.log";
            ErrorLogFileName = BaseLogFileName + ".err.log";
        }

        public string OutputLogFileName { get; private set; }

        public string ErrorLogFileName { get; private set; }

        public override void log(Stream outputStream, Stream errorStream)
        {
            new Thread(() => CopyStream(outputStream, new FileStream(OutputLogFileName, fileMode))).Start();
            new Thread(() => CopyStream(errorStream, new FileStream(ErrorLogFileName, fileMode))).Start();
        }
    }

    public class DefaultLogAppender : SimpleLogAppender
    {
        public DefaultLogAppender(string logDirectory, string baseName)
            : base(logDirectory, baseName, FileMode.Append)
        {
        }
    }

    public class ResetLogAppender : SimpleLogAppender
    {
        public ResetLogAppender(string logDirectory, string baseName)
            : base(logDirectory, baseName, FileMode.Create)
        {
        }

    }

    public class TimeBasedRollingLogAppender : LogHandler
    {

        private string pattern;
        private int period;

        public TimeBasedRollingLogAppender(string logDirectory, string baseName, string pattern, int period)
            : base(logDirectory, baseName)
        {
            this.pattern = pattern;
            this.period = period;
        }

        public override void log(Stream outputStream, Stream errorStream)
        {
            new Thread(() => CopyStreamWithDateRotation(outputStream, ".out.log")).Start();
            new Thread(() => CopyStreamWithDateRotation(errorStream, ".err.log")).Start();
        }

        /// <summary>
        /// Works like the CopyStream method but does a log rotation based on time.
        /// </summary>
        private void CopyStreamWithDateRotation(Stream data, string ext)
        {
            var periodicRollingCalendar = new PeriodicRollingCalendar(pattern, period);
            periodicRollingCalendar.init();

            var buf = new byte[1024];
            var w = new FileStream(BaseLogFileName + "_" + periodicRollingCalendar.Format + ext, FileMode.Create);
            while (true)
            {
                var len = data.Read(buf, 0, buf.Length);
                if (len == 0) break;    // EOF

                if (periodicRollingCalendar.ShouldRoll)
                {// rotate at the line boundary
                    var offset = 0;
                    var rolled = false;
                    for (var i = 0; i < len; i++)
                    {
                        if (buf[i] != 0x0A)
                        {
                            continue;
                        } // at the line boundary.
                        // time to rotate.
                        w.Write(buf, offset, i + 1);
                        w.Close();
                        offset = i + 1;

                        // create a new file.
                        w = new FileStream(BaseLogFileName + "_" + periodicRollingCalendar.Format + ext, FileMode.Create);
                        rolled = true;
                    }

                    if (!rolled)
                    {// we didn't roll - most likely as we didnt find a line boundary, so we should log what we read and roll anyway.
                        w.Write(buf, 0, len);
                        w.Close();
                        w = new FileStream(BaseLogFileName + "_" + periodicRollingCalendar.Format + ext, FileMode.Create);
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

    public class SizeBasedRollingLogAppender : LogHandler
    {
        public static int BYTES_PER_KB = 1024;
        public static int BYTES_PER_MB = 1024 * BYTES_PER_KB;
        public static int DEFAULT_SIZE_THRESHOLD = 10 * BYTES_PER_MB; // rotate every 10MB.
        public static int DEFAULT_FILES_TO_KEEP = 8;

        private readonly int sizeThreshold;
        private readonly int filesToKeep;

        public SizeBasedRollingLogAppender(string logDirectory, string baseName, int sizeThreshold, int filesToKeep)
            : base(logDirectory, baseName)
        {
            this.sizeThreshold = sizeThreshold;
            this.filesToKeep = filesToKeep;
        }

        public SizeBasedRollingLogAppender(string logDirectory, string baseName)
            : this(logDirectory, baseName, DEFAULT_SIZE_THRESHOLD, DEFAULT_FILES_TO_KEEP) { }

        public override void log(Stream outputStream, Stream errorStream)
        {
            new Thread(() => CopyStreamWithRotation(outputStream, ".out.log")).Start();
            new Thread(() => CopyStreamWithRotation(errorStream, ".err.log")).Start();
        }

        /// <summary>
        /// Works like the CopyStream method but does a log rotation.
        /// </summary>
        private void CopyStreamWithRotation(Stream data, string ext)
        {
            var buf = new byte[1024];
            var w = new FileStream(BaseLogFileName + ext, FileMode.Append);
            var sz = new FileInfo(BaseLogFileName + ext).Length;

            while (true)
            {
                var len = data.Read(buf, 0, buf.Length);
                if (len == 0) break;    // EOF
                if (sz + len < sizeThreshold)
                {// typical case. write the whole thing into the current file
                    w.Write(buf, 0, len);
                    sz += len;
                }
                else
                {
                    // rotate at the line boundary
                    var s = 0;
                    for (var i = 0; i < len; i++)
                    {
                        if (buf[i] != 0x0A) continue;
                        if (sz + i < sizeThreshold) continue;

                        // at the line boundary and exceeded the rotation unit.
                        // time to rotate.
                        w.Write(buf, s, i + 1);
                        w.Close();
                        s = i + 1;

                        try
                        {
                            for (var j = filesToKeep; j >= 1; j--)
                            {
                                var dst = BaseLogFileName + "." + (j - 1) + ext;
                                var src = BaseLogFileName + "." + (j - 2) + ext;
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
        public RollingLogAppender(string logDirectory, string baseName)
            : base(logDirectory, baseName, FileMode.Append)
        {
        }

        public override void log(Stream outputStream, Stream errorStream)
        {
            CopyFile(OutputLogFileName, OutputLogFileName + ".old");
            CopyFile(ErrorLogFileName, ErrorLogFileName + ".old");
            base.log(outputStream, errorStream);
        }
    }
}