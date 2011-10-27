using System;
using System.Data;

namespace winsw
{
    /**
     *  This is largely borrowed from the logback Rolling Calendar.
     **/
    public class PeriodicRollingCalendar
    {
        private PeriodicityType _periodicityType;
        private string _format;
        private long _period;
        private DateTime _currentRoll;
        private DateTime _nextRoll;

        public PeriodicRollingCalendar(string format, long period)
        {
            this._format = format;
            this._period = period;
            this._currentRoll = DateTime.Now;
        }

        public void init()
        {
            this._periodicityType = determinePeriodicityType();
            this._nextRoll = nextTriggeringTime(this._currentRoll, this._period);
        }

        public enum PeriodicityType
        {
            ERRONEOUS, TOP_OF_MILLISECOND, TOP_OF_SECOND, TOP_OF_MINUTE, TOP_OF_HOUR, TOP_OF_DAY
        }

        private static PeriodicityType[] VALID_ORDERED_LIST = new PeriodicityType[] {
            PeriodicityType.TOP_OF_MILLISECOND, PeriodicityType.TOP_OF_SECOND, PeriodicityType.TOP_OF_MINUTE, PeriodicityType.TOP_OF_HOUR, PeriodicityType.TOP_OF_DAY
        };

        private PeriodicityType determinePeriodicityType()
        {
            PeriodicRollingCalendar periodicRollingCalendar = new PeriodicRollingCalendar(_format, _period);
            DateTime epoch = new DateTime(1970, 1, 1);

            foreach (PeriodicityType i in VALID_ORDERED_LIST)
            {
                string r0 = epoch.ToString(_format);
                periodicRollingCalendar.periodicityType = i;

                DateTime next = periodicRollingCalendar.nextTriggeringTime(epoch, 1);
                string r1 = next.ToString(_format);

                if (r0 != null && r1 != null && !r0.Equals(r1))
                {
                    return i;
                }
            }
            return PeriodicityType.ERRONEOUS;
        }

        private DateTime nextTriggeringTime(DateTime input, long increment)
        {
            DateTime output;
            switch (_periodicityType)
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
                    throw new Exception("invalid periodicity type: " + _periodicityType);
            }
        }

        public PeriodicityType periodicityType
        {
            set
            {
                this._periodicityType = value;
            }
        }

        public Boolean shouldRoll
        {
            get
            {
                DateTime now = DateTime.Now;
                if (now > this._nextRoll)
                {
                    this._currentRoll = now;
                    this._nextRoll = nextTriggeringTime(now, this._period);
                    return true;
                }
                return false;
            }
        }

        public string format
        {
            get
            {
                return this._currentRoll.ToString(this._format);
            }
        }

    }
}
