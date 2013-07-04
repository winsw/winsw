using System;
using System.Data;

namespace winsw
{
    /**
     *  This is largely borrowed from the logback Rolling Calendar.
     **/
    public class PeriodicRollingCalendar
    {
        private string _format;
        private long _period;
        private DateTime _currentRoll;
        private DateTime _nextRoll;

        public PeriodicRollingCalendar(string format, long period)
        {
            _format = format;
            _period = period;
            _currentRoll = DateTime.Now;
        }

        public void init()
        {
            Type = determinePeriodicityType();
            _nextRoll = NextTriggeringTime(_currentRoll, _period);
        }

        public enum PeriodicityType
        {
            ERRONEOUS, TOP_OF_MILLISECOND, TOP_OF_SECOND, TOP_OF_MINUTE, TOP_OF_HOUR, TOP_OF_DAY
        }

        private static PeriodicityType[] VALID_ORDERED_LIST = new[] {
            PeriodicityType.TOP_OF_MILLISECOND, PeriodicityType.TOP_OF_SECOND, PeriodicityType.TOP_OF_MINUTE, PeriodicityType.TOP_OF_HOUR, PeriodicityType.TOP_OF_DAY
        };

        private PeriodicityType determinePeriodicityType()
        {
            var periodicRollingCalendar = new PeriodicRollingCalendar(_format, _period);
            var epoch = new DateTime(1970, 1, 1);

            foreach (var i in VALID_ORDERED_LIST)
            {
                var r0 = epoch.ToString(_format);
                periodicRollingCalendar.Type = i;

                var next = periodicRollingCalendar.NextTriggeringTime(epoch, 1);
                var r1 = next.ToString(_format);

                if (!r0.Equals(r1))
                {
                    return i;
                }
            }
            return PeriodicityType.ERRONEOUS;
        }

        private DateTime NextTriggeringTime(DateTime input, long increment)
        {
            DateTime output;
            switch (Type)
            {
                case PeriodicityType.TOP_OF_MILLISECOND:
                    output = new DateTime(input.Year, input.Month, input.Day, input.Hour, input.Minute, input.Second, input.Millisecond);
                    output = output.AddMilliseconds(increment);
                    return output;
                case PeriodicityType.TOP_OF_SECOND:
                    output = new DateTime(input.Year, input.Month, input.Day, input.Hour, input.Minute, input.Second);
                    output = output.AddSeconds(increment);
                    return output;
                case PeriodicityType.TOP_OF_MINUTE:
                    output = new DateTime(input.Year, input.Month, input.Day, input.Hour, input.Minute, 0);
                    output = output.AddMinutes(increment);
                    return output;
                case PeriodicityType.TOP_OF_HOUR:
                    output = new DateTime(input.Year, input.Month, input.Day, input.Hour, 0, 0);
                    output = output.AddHours(increment);
                    return output;
                case PeriodicityType.TOP_OF_DAY:
                    output = new DateTime(input.Year, input.Month, input.Day);
                    output = output.AddDays(increment);
                    return output;
                default:
                    throw new Exception("invalid periodicity type: " + Type);
            }
        }

        public PeriodicityType Type { private get; set; }

        public Boolean ShouldRoll
        {
            get
            {
                var now = DateTime.Now;
                if (now > _nextRoll)
                {
                    _currentRoll = now;
                    _nextRoll = NextTriggeringTime(now, _period);
                    return true;
                }
                return false;
            }
        }

        public string Format
        {
            get
            {
                return _currentRoll.ToString(_format);
            }
        }

    }
}
