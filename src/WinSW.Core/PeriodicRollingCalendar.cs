using System;

namespace WinSW
{
    // This is largely borrowed from the logback Rolling Calendar.
    public class PeriodicRollingCalendar
    {
        private readonly string format;
        private readonly int period;
        private DateTime currentRoll;
        private DateTime nextRoll;

        public PeriodicRollingCalendar(string format, int period)
        {
            this.format = format;
            this.period = period;
            this.currentRoll = DateTime.Now;
        }

        public void Init()
        {
            this.PeriodicityType = this.DeterminePeriodicityType();
            this.nextRoll = this.NextTriggeringTime(this.currentRoll, this.period);
        }

        public enum Periodicity
        {
            ERRONEOUS,
            TOP_OF_MILLISECOND,
            TOP_OF_SECOND,
            TOP_OF_MINUTE,
            TOP_OF_HOUR,
            TOP_OF_DAY,
            TOP_OF_MONTH,
        }

        private static readonly Periodicity[] ValidOrderedList =
        {
            Periodicity.TOP_OF_MILLISECOND,
            Periodicity.TOP_OF_SECOND,
            Periodicity.TOP_OF_MINUTE,
            Periodicity.TOP_OF_HOUR,
            Periodicity.TOP_OF_DAY,
            Periodicity.TOP_OF_MONTH,
        };

        private Periodicity DeterminePeriodicityType()
        {
            var periodicRollingCalendar = new PeriodicRollingCalendar(this.format, this.period);
            var epoch = new DateTime(1970, 1, 1);

            foreach (var i in ValidOrderedList)
            {
                string r0 = epoch.ToString(this.format);
                periodicRollingCalendar.PeriodicityType = i;

                var next = periodicRollingCalendar.NextTriggeringTime(epoch, 1);
                string r1 = next.ToString(this.format);

                if (r0 != r1)
                {
                    return i;
                }
            }

            return Periodicity.ERRONEOUS;
        }

        private DateTime NextTriggeringTime(DateTime input, int increment) => this.PeriodicityType switch
        {
            Periodicity.TOP_OF_MILLISECOND =>
                new DateTime(input.Year, input.Month, input.Day, input.Hour, input.Minute, input.Second, input.Millisecond)
                    .AddMilliseconds(increment),

            Periodicity.TOP_OF_SECOND =>
                new DateTime(input.Year, input.Month, input.Day, input.Hour, input.Minute, input.Second)
                    .AddSeconds(increment),

            Periodicity.TOP_OF_MINUTE =>
                new DateTime(input.Year, input.Month, input.Day, input.Hour, input.Minute, 0)
                    .AddMinutes(increment),

            Periodicity.TOP_OF_HOUR =>
                new DateTime(input.Year, input.Month, input.Day, input.Hour, 0, 0)
                    .AddHours(increment),

            Periodicity.TOP_OF_DAY =>
                new DateTime(input.Year, input.Month, input.Day)
                    .AddDays(increment),

            Periodicity.TOP_OF_MONTH =>
                new DateTime(input.Year, input.Month, 1)
                    .AddMonths(increment),

            _ => throw new Exception("invalid periodicity type: " + this.PeriodicityType),
        };

        public Periodicity PeriodicityType { get; set; }

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
