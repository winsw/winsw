using System;

namespace WinSW.Tests.Util
{
    internal static class DateTimeExtensions
    {
        internal static DateTime TrimToSeconds(this DateTime dateTime) =>
            dateTime.AddTicks(-(dateTime.Ticks % TimeSpan.TicksPerSecond));
    }
}
