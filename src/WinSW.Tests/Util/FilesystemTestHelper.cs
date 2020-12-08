using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

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

        /// <summary>
        /// Parses output of the "set" command from the file
        /// </summary>
        /// <param name="filePath">File path</param>
        /// <returns>Dictionary of the strings.</returns>
        public static Dictionary<string, string> parseSetOutput(string filePath)
        {
            var res = new Dictionary<string, string>();
            string[] lines = File.ReadAllLines(filePath);
            foreach (string line in lines)
            {
                string[] parsed = line.Split("=".ToCharArray(), 2);
                if (parsed.Length == 2)
                {
                    res.Add(parsed[0], parsed[1]);
                }
                else
                {
                    Assert.Fail("Wrong line in the parsed Set output file: " + line);
                }
            }

            return res;
        }
    }
}
