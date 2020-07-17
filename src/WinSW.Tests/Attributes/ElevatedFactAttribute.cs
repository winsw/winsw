using System;
using Xunit;

namespace WinSW.Tests
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ElevatedFactAttribute : FactAttribute
    {
        public ElevatedFactAttribute()
        {
            if (!Program.IsProcessElevated())
            {
                this.Skip = "Access is denied";
            }
        }
    }
}
