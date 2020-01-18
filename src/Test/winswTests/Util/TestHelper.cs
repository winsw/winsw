using NUnit.Framework;
using winsw;

namespace winswTests.Util
{
    internal static class TestHelper
    {
        internal static void RequireProcessElevated()
        {
            if (!Program.IsProcessElevated())
            {
                Assert.Ignore();
            }
        }
    }
}
