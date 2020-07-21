using System;
using System.Diagnostics;

namespace WinSW
{
    internal static class FormatExtensions
    {
        internal static string Format(this Process process)
        {
            try
            {
                return $"{process.ProcessName} ({process.Id})";
            }
            catch (InvalidOperationException)
            {
                return $"({process.Id})";
            }
        }
    }
}
