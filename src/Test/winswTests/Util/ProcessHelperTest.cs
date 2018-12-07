using System;
using System.Diagnostics;
using NUnit.Framework;
using winsw;
using System.IO;
using winsw.Util;
using System.Collections.Generic;

namespace winswTests.Util
{

    [TestFixture]
    class ProcessHelperTest
    {
        /// <summary>
        /// Also reported as <a href="https://issues.jenkins-ci.org/browse/JENKINS-42744">JENKINS-42744</a>
        /// </summary>
        [Test]
        public void ShouldPropagateVariablesInUppercase()
        {
            Environment.SetEnvironmentVariable("TEST_KEY", "TEST_VALUE");

            var tmpDir = FilesystemTestHelper.CreateTmpDirectory();
            String envFile = Path.Combine(tmpDir, "env.properties");
            String scriptFile = Path.Combine(tmpDir, "printenv.bat");
            File.WriteAllText(scriptFile, "set > " + envFile);


            Process proc = new Process();
            var ps = proc.StartInfo;
            ps.FileName = scriptFile;

            ProcessHelper.StartProcessAndCallbackForExit(proc);
            var exited = proc.WaitForExit(5000);
            if (!exited)
            {
                Assert.Fail("Process " + proc + " didn't exit after 5 seconds");
            }

            // Check several veriables, which are expected to be in Uppercase
            var envVars = FilesystemTestHelper.parseSetOutput(envFile);
            String[] keys = new String[envVars.Count];
            envVars.Keys.CopyTo(keys, 0);
            String availableVars = "[" + String.Join(",", keys) + "]";
            Assert.That(envVars.ContainsKey("TEST_KEY"), "No TEST_KEY in the injected vars: " + availableVars);
            
            // And just ensure that the parsing logic is case-sensitive
            Assert.That(!envVars.ContainsKey("test_key"), "Test error: the environment parsing logic is case-insensitive");

        }

        [Test]
        public void ShouldNotHangWhenWritingLargeStringToStdOut()
        {
            var tmpDir = FilesystemTestHelper.CreateTmpDirectory();
            String scriptFile = Path.Combine(tmpDir, "print_lots_to_stdout.bat");
            var lotsOfStdOut = string.Join("", _Range(1,1000));
            File.WriteAllText(scriptFile, string.Format("echo \"{0}\"", lotsOfStdOut));

            Process proc = new Process();
            var ps = proc.StartInfo;
            ps.FileName = scriptFile;

            ProcessHelper.StartProcessAndCallbackForExit(proc);
            var exited = proc.WaitForExit(5000);
            if (!exited)
            {
                Assert.Fail("Process " + proc + " didn't exit after 5 seconds");
            }
        }

        private string[] _Range(int start, int limit)
        {
            var range = new List<string>();
            for(var i = start; i<limit; i++)
            {
                range.Add(i.ToString());
            }
            return range.ToArray();
        }
    }
}
