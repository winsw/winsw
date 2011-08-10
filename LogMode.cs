using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace winsw
{
    public enum LogBehavior
    {
        Append,
        Rotate,
        Roll,
        Reset,
    }



    /// <summary>
    /// Specify the log file handling mode and any attributes associate with the log mode.
    /// </summary>
    public class LogMode
    {
        private static string DefaultLogCount = "5";
        private static string DefaultLogSize = "10M";

        private static int MaximumLogCount = 14;
        private static int MinimumLogSize = 1024 * 1024;
        private static int MaximumLogSize = 100 * 1024 * 1024;

        /// <summary>
        /// Mode for handling creation of new logfiles (Append, Rotate, Roll, or Reset)
        /// Default: Append
        /// </summary>
        public readonly LogBehavior mode;

        /// <summary>
        /// The number of logfiles to maintain
        /// Applicable for rolling and rotating logfile modes.
        /// Default: 5
        /// </summary>
        public readonly int numLogs;

        /// <summary>
        /// The maximum size of a lofile that forces the advance to the new file.
        /// Applicable for rotating logfile mode.
        /// Default: 10MB
        /// </summary>
        public readonly int logSize;

        internal LogMode(string modeString, string countString, string sizeLimitString)
        {
            if (modeString == null)
                modeString = "append";

            if (countString == null)
                countString = DefaultLogCount;

            if (sizeLimitString == null)
                sizeLimitString = DefaultLogSize;

            switch (modeString.ToLower())
            {
                case "rotate":
                case "limitsize":
                case "limit-size":
                    mode = LogBehavior.Rotate;
                    break;
                case "age":
                case "roll":
                case "roll-over":
                case "rollover":
                    mode = LogBehavior.Roll;
                    break;
                case "reset":
                case "create":
                case "overwrite":
                    mode = LogBehavior.Reset;
                    break;
                case "append":
                    mode = LogBehavior.Append;
                    break;
                default:
                    throw new ArgumentException("Invalid Logfile handling mode, " + modeString + ".");
            }

            bool canConvert = int.TryParse(countString, out numLogs);

            if (!canConvert)
            {
                throw new ArgumentException("Logfile count must be an integer.");
            }

            if ((numLogs <= 0) || (numLogs >= MaximumLogCount))
                throw new ArgumentOutOfRangeException("count", "Logfile count must be between 1 and " + MaximumLogCount);

            int scale = 1;
            if (sizeLimitString.EndsWith("M"))
            {
                scale = 1024 * 1024;
                sizeLimitString = sizeLimitString.Substring(0, sizeLimitString.Length - 1);
            }
            else if (sizeLimitString.EndsWith("K"))
            {
                scale = 1024;
                sizeLimitString = sizeLimitString.Substring(0, sizeLimitString.Length - 1);
            }

            canConvert = int.TryParse(sizeLimitString, out logSize);
            if (!canConvert)
            {
                throw new ArgumentException("Logfile size must be an integer optionally followed by \"K\" or \"M\"");
            }

            logSize *= scale;

            if ((logSize < MinimumLogSize) || (logSize > MaximumLogSize))
                throw new ArgumentOutOfRangeException("size", "Logfile size must be between " + MinimumLogSize + "M and " + MaximumLogSize + "M");

        }
    }
}
