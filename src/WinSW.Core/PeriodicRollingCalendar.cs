using System;

namespace WinSW
{
    // This is largely borrowed from the logback Rolling Calendar.
    public class PeriodicRollingCalendar
    {
        private readonly string format;
        private readonly long period;
        private DateTime currentRoll;
        private DateTime nextRoll;

        public PeriodicRollingCalendar(string format, long period)
        {
            this.format = format;
            this.period = period;
            this.currentRoll = DateTime.Now;
        }

        public void Init()
        {
            this.Periodicity = this.DeterminePeriodicityType();
            this.nextRoll = this.NextTriggeringTime(this.currentRoll, this.period);
        }

        public enum PeriodicityType
        {
            ERRONEOUS,
            TOP_OF_MILLISECOND,
            TOP_OF_SECOND,
            TOP_OF_MINUTE,
            TOP_OF_HOUR,
            TOP_OF_DAY
        }

        private static readonly PeriodicityType[] ValidOrderedList =
        {
            PeriodicityType.TOP_OF_MILLISECOND, PeriodicityType.TOP_OF_SECOND, PeriodicityType.TOP_OF_MINUTE, PeriodicityType.TOP_OF_HOUR, PeriodicityType.TOP_OF_DAY
        };

        private PeriodicityType DeterminePeriodicityType()
        {
            var periodicRollingCalendar = new PeriodicRollingCalendar(this.format, this.period);
            var epoch = new DateTime(1970, 1, 1);

            foreach (var i in ValidOrderedList)
            {
                string r0 = epoch.ToString(this.format);
                periodicRollingCalendar.Periodicity = i;

                var next = periodicRollingCalendar.NextTriggeringTime(epoch, 1);
                string r1 = next.ToString(this.format);

                if (r0 != r1)
                {
                    return i;
                }
            }

            return PeriodicityType.ERRONEOUS;
        }

        private DateTime NextTriggeringTime(DateTime input, long increment) => this.Periodicity switch
        {
            PeriodicityType.TOP_OF_MILLISECOND =>
                new DateTime(input.Year, input.Month, input.Day, input.Hour, input.Minute, input.Second, input.Millisecond)
                    .AddMilliseconds(increment),

            PeriodicityType.TOP_OF_SECOND =>
                new DateTime(input.Year, input.Month, input.Day, input.Hour, input.Minute, input.Second)
                    .AddSeconds(increment),

            PeriodicityType.TOP_OF_MINUTE =>
                new DateTime(input.Year, input.Month, input.Day, input.Hour, input.Minute, 0)
                    .AddMinutes(increment),

            PeriodicityType.TOP_OF_HOUR =>
                new DateTime(input.Year, input.Month, input.Day, input.Hour, 0, 0)
                    .AddHours(increment),

            PeriodicityType.TOP_OF_DAY =>
                new DateTime(input.Year, input.Month, input.Day)
                    .AddDays(increment),

            _ => throw new Exception("invalid periodicity type: " + this.Periodicity),
        };

        public PeriodicityType Periodicity { get; set; }

        public bool ShouldRoll
        {
            get
            {
                var now = DateTime.Now;
                if (now > this.nextRoll)
                {
                    this.currentRoll = now;
                    this.nextRoll = this.NextTriggeringTime(now, this.period);
                    return true;
                }

                return false;
            }
        }

        public string Format => this.currentRoll.ToString(this.format);
    }
}
