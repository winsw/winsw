using System;
using System.Diagnostics;
using NUnit.Framework;
using winsw;
using System.IO;
using winsw.Util;

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
            Assert.That(envVars.ContainsKey("PROCESSOR_ARCHITECTURE"), "No PROCESSOR_ARCHITECTURE in the injected vars");
            Assert.That(envVars.ContainsKey("COMPUTERNAME"), "No COMPUTERNAME in the injected vars");
            Assert.That(envVars.ContainsKey("PATHEXT"), "No PATHEXT in the injected vars");
            
            // And just ensure that the parsing logic is case-sensitive
            Assert.That(!envVars.ContainsKey("computername"), "Test error: the environment parsing logic is case-insensitive");

        }
    }
}
