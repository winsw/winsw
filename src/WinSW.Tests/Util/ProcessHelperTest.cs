using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WinSW.Util;

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
            Assert.Ignore();

            Environment.SetEnvironmentVariable("TEST_KEY", "TEST_VALUE");

            string tmpDir = FilesystemTestHelper.CreateTmpDirectory();
            string envFile = Path.Combine(tmpDir, "env.properties");
            string scriptFile = Path.Combine(tmpDir, "printenv.bat");
            File.WriteAllText(scriptFile, "set > " + envFile);

            var proc = new Process();
            var ps = proc.StartInfo;
            ps.FileName = scriptFile;

            ProcessHelper.StartProcessAndCallbackForExit(proc);
            bool exited = proc.WaitForExit(5000);
            if (!exited)
            {
                Assert.Fail("Process " + proc + " didn't exit after 5 seconds");
            }

            // Check several veriables, which are expected to be in Uppercase
            var envVars = FilesystemTestHelper.parseSetOutput(envFile);
            string[] keys = new string[envVars.Count];
            envVars.Keys.CopyTo(keys, 0);
            string availableVars = "[" + string.Join(",", keys) + "]";
            Assert.That(envVars.ContainsKey("TEST_KEY"), "No TEST_KEY in the injected vars: " + availableVars);

            // And just ensure that the parsing logic is case-sensitive
            Assert.That(!envVars.ContainsKey("test_key"), "Test error: the environment parsing logic is case-insensitive");
        }

        [Test]
        public void ShouldNotHangWhenWritingLargeStringToStdOut()
        {
            string tmpDir = FilesystemTestHelper.CreateTmpDirectory();
            string scriptFile = Path.Combine(tmpDir, "print_lots_to_stdout.bat");
            string lotsOfStdOut = string.Join(string.Empty, Enumerable.Range(1, 1000));
            File.WriteAllText(scriptFile, $"echo \"{lotsOfStdOut}\"");

            var proc = new Process();
            var ps = proc.StartInfo;
            ps.FileName = scriptFile;

            ProcessHelper.StartProcessAndCallbackForExit(proc);
            bool exited = proc.WaitForExit(5000);
            if (!exited)
            {
                Assert.Fail("Process " + proc + " didn't exit after 5 seconds");
            }
        }
    }
}
