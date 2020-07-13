using System;
using System.IO;

namespace winsw.Configuration
{
    public abstract class Log
    {
        public abstract string? Mode { get; }

        public abstract string? Name { get; }

        public abstract string? Directory { get; }

        public abstract int? SizeThreshold { get; }

        public abstract int? KeepFiles { get; }

        public abstract string? Pattern { get; }


        public abstract int? Period { get; }


        // Filters
        public abstract bool OutFileDisabled { get;}

        public abstract bool ErrFileDisabled { get;}

        public abstract string OutFilePattern { get; }

        public abstract string ErrFilePattern { get; }


        // Zip options
        public abstract string? AutoRollAtTime { get; }

        public abstract int? ZipOlderThanNumDays { get; }

        public abstract string? ZipDateFormat { get;}

        public LogHandler createLogHandler()
        {
 
            switch (Mode)
            {
                case "rotate":
                    return new SizeBasedRollingLogAppender(Directory, Name, OutFileDisabled, ErrFileDisabled, OutFilePattern, ErrFilePattern);

                case "none":
                    return new IgnoreLogAppender();

                case "reset":
                    return new ResetLogAppender(Directory, Name, OutFileDisabled, ErrFileDisabled, OutFilePattern, ErrFilePattern);

                case "roll":
                    return new RollingLogAppender(Directory, Name, OutFileDisabled, ErrFileDisabled, OutFilePattern, ErrFilePattern);

                case "roll-by-time":
                    return new TimeBasedRollingLogAppender(Directory, Name, OutFileDisabled, ErrFileDisabled, OutFilePattern, ErrFilePattern, Pattern, Period.GetValueOrDefault(1));

                case "roll-by-size":
                    return new SizeBasedRollingLogAppender(Directory, Name, OutFileDisabled, ErrFileDisabled, OutFilePattern, ErrFilePattern,
                        SizeThreshold.GetValueOrDefault(10 * 1024) * SizeBasedRollingLogAppender.BYTES_PER_KB,
                        KeepFiles.GetValueOrDefault(SizeBasedRollingLogAppender.DEFAULT_FILES_TO_KEEP));

                case "append":
                    return new DefaultLogAppender(Directory, Name, OutFileDisabled, ErrFileDisabled, OutFilePattern, ErrFilePattern);

                case "roll-by-size-time":
                    if (Pattern is null)
                    {
                        throw new InvalidDataException("Roll-Size-Time Based rolling policy is specified but no pattern can be found in configuration");
                    }

                    TimeSpan? autoRollAtTime = null;
                    if (AutoRollAtTime != null)
                    {
                        // validate it
                        if (!TimeSpan.TryParse(AutoRollAtTime, out TimeSpan autoRollAtTimeValue))
                            throw new InvalidDataException("Roll-Size-Time Based rolling policy is specified but autoRollAtTime does not match the TimeSpan format HH:mm:ss found in configuration XML.");

                        autoRollAtTime = autoRollAtTimeValue;
                    }

                    return new RollingSizeTimeLogAppender(Directory, Name, OutFileDisabled, ErrFileDisabled, OutFilePattern, ErrFilePattern,
                        SizeThreshold.GetValueOrDefault(10 * 1024) * SizeBasedRollingLogAppender.BYTES_PER_KB,
                        Pattern, autoRollAtTime,
                        ZipOlderThanNumDays,
                        ZipDateFormat != null ? ZipDateFormat : "yyyyMM");

                default:
                    throw new InvalidDataException("Undefined logging mode: " + Mode);
            }
        }
    }
}