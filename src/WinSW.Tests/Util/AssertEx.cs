using Xunit;

namespace WinSW.Tests.Util
{
    internal static class AssertEx
    {
        internal static void Succeeded(int hr) => Assert.InRange(hr, 0, int.MaxValue);
    }
}
