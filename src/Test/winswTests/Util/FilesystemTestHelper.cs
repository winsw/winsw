using System;
using System.IO;

namespace winswTests.Util
{
    class FilesystemTestHelper
    {
        /// <summary>
        /// Creates a temporary directory for testing.
        /// </summary>
        /// <returns>tmp Dir</returns>
        public static string CreateTmpDirectory(string testName = null)
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), "winswTests_" + (testName ?? string.Empty) + Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);
            Console.Out.WriteLine("Created the temporary directory: {0}", tempDirectory);
            return tempDirectory;
        }
    }
}
