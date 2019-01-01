using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using winsw.Util;

namespace winswTests.Util
{
    [TestFixture]
    class ProcessHelperTest
    {
        [Test]
        public void ShouldNotHangWhenWritingLargeStringToStdOut()
        {
            var tmpDir = FilesystemTestHelper.CreateTmpDirectory();
            string scriptFile = Path.Combine(tmpDir, "print_lots_to_stdout.bat");
            var lotsOfStdOut = string.Join(string.Empty, Enumerable.Range(1, 1000));
            File.WriteAllText(scriptFile, $"echo \"{lotsOfStdOut}\"");

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
    }
}
