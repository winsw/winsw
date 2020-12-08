using System;
using System.IO;

namespace WinSW.Configuration
{
    public abstract class Log
    {
        public abstract string? Mode { get; }

        public abstract string Name { get; }

        public abstract string Directory { get; }

        public abstract int? SizeThreshold { get; }

        public abstract int? KeepFiles { get; }

        public abstract string Pattern { get; }

        public abstract int? Period { get; }

        // Filters
        public abstract bool OutFileDisabled { get; }

        public abstract bool ErrFileDisabled { get; }

        public abstract string OutFilePattern { get; }

        public abstract string ErrFilePattern { get; }

        // Zip options
        public abstract string? AutoRollAtTime { get; }

        public abstract int? ZipOlderThanNumDays { get; }

        public abstract string? ZipDateFormat { get; }

        public LogHandler CreateLogHandler()
        {
            switch (this.Mode)
            {
                case "rotate":
                    return new SizeBasedRollingLogAppender(this.Directory, this.Name, this.OutFileDisabled, this.ErrFileDisabled, this.OutFilePattern, this.ErrFilePattern);

                case "none":
                    return new IgnoreLogAppender();

                case "reset":
                    return new ResetLogAppender(this.Directory, this.Name, this.OutFileDisabled, this.ErrFileDisabled, this.OutFilePattern, this.ErrFilePattern);

                case "roll":
                    return new RollingLogAppender(this.Directory, this.Name, this.OutFileDisabled, this.ErrFileDisabled, this.OutFilePattern, this.ErrFilePattern);

                case "roll-by-time":
                    return new TimeBasedRollingLogAppender(this.Directory, this.Name, this.OutFileDisabled, this.ErrFileDisabled, this.OutFilePattern, this.ErrFilePattern, this.Pattern, this.Period.GetValueOrDefault(1));

                case "roll-by-size":
                    return new SizeBasedRollingLogAppender(
                        this.Directory,
                        this.Name,
                        this.OutFileDisabled,
                        this.ErrFileDisabled,
                        this.OutFilePattern,
                        this.ErrFilePattern,
                        this.SizeThreshold.GetValueOrDefault(10 * 1024) * SizeBasedRollingLogAppender.BytesPerKB,
                        this.KeepFiles.GetValueOrDefault(SizeBasedRollingLogAppender.DefaultFilesToKeep));

                case "append":
                    return new DefaultLogAppender(this.Directory, this.Name, this.OutFileDisabled, this.ErrFileDisabled, this.OutFilePattern, this.ErrFilePattern);

                case "roll-by-size-time":
                    if (this.Pattern is null)
                    {
                        throw new InvalidDataException("Roll-Size-Time Based rolling policy is specified but no pattern can be found in configuration");
                    }

                    TimeSpan? autoRollAtTime = null;
                    if (this.AutoRollAtTime != null)
                    {
                        // validate it
                        if (!TimeSpan.TryParse(this.AutoRollAtTime, out var autoRollAtTimeValue))
                        {
                            throw new InvalidDataException("Roll-Size-Time Based rolling policy is specified but autoRollAtTime does not match the TimeSpan format HH:mm:ss found in configuration XML.");
                        }

                        autoRollAtTime = autoRollAtTimeValue;
                    }

                    return new RollingSizeTimeLogAppender(
                        this.Directory,
                        this.Name,
                        this.OutFileDisabled,
                        this.ErrFileDisabled,
                        this.OutFilePattern,
                        this.ErrFilePattern,
                        this.SizeThreshold.GetValueOrDefault(10 * 1024) * SizeBasedRollingLogAppender.BytesPerKB,
                        this.Pattern,
                        autoRollAtTime,
                        this.ZipOlderThanNumDays,
                        this.ZipDateFormat != null ? this.ZipDateFormat : "yyyyMM");

                default:
                    throw new InvalidDataException("Undefined logging mode: " + this.Mode);
            }
        }
    }
}