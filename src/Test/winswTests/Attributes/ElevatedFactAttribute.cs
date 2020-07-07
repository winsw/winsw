using System;
using Xunit;

namespace WinSW.Tests
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    internal sealed class ElevatedFactAttribute : FactAttribute
    {
        internal ElevatedFactAttribute()
        {
            if (!Program.IsProcessElevated())
            {
                this.Skip = "Access is denied";
            }
        }
    }
}
