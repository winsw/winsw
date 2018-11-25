using System.IO;

namespace WinSW.Tests.Util
{
    internal class FilesystemTestHelper
    {
        /// <summary>
        /// Creates a temporary directory for testing.
        /// </summary>
        /// <returns>tmp Dir</returns>
        public static string CreateTmpDirectory(string testName = null)
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), "winswTests_" + (testName ?? string.Empty) + Path.GetRandomFileName());
            _ = Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }
    }
}
