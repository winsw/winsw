using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;
using static System.IO.File;

namespace WinSW.Tests
{
    public class LogAppenderTests
    {
        private const byte CR = 0x0d;
        private const byte LF = 0x0a;

        [Fact]
        public void DefaultLogAppender()
        {
            byte[] stdout = { 0x4e, 0x65, 0x78, 0x74 };
            byte[] stderr = { 0x54, 0x75, 0x72, 0x6e };

            using var data = TestData.Create();

            string baseName = data.name;
            string outFileExt = ".out.log";
            string errFileExt = ".err.log";
            string outFileName = baseName + outFileExt;
            string errFileName = baseName + errFileExt;
            string outFilePath = Path.Combine(data.path, outFileName);
            string errFilePath = Path.Combine(data.path, errFileName);

            WriteAllBytes(outFilePath, stdout);
            WriteAllBytes(errFilePath, stderr);

            var appender = new DefaultLogAppender(data.path, data.name, false, false, outFileExt, errFileExt);
            appender.Log(new(new MemoryStream(stdout)), new(new MemoryStream(stderr)));

            Assert.True(Exists(outFilePath));
            Assert.True(Exists(errFilePath));

            Assert.Equal(stdout.Concat(stdout), ReadAllBytes(outFilePath));
            Assert.Equal(stderr.Concat(stderr), ReadAllBytes(errFilePath));
        }

        [Fact]
        public void ResetLogAppender()
        {
            byte[] stdout = { 0x4e, 0x65, 0x78, 0x74 };
            byte[] stderr = { 0x54, 0x75, 0x72, 0x6e };

            using var data = TestData.Create();

            string baseName = data.name;
            string outFileExt = ".out.log";
            string errFileExt = ".err.log";
            string outFileName = baseName + outFileExt;
            string errFileName = baseName + errFileExt;
            string outFilePath = Path.Combine(data.path, outFileName);
            string errFilePath = Path.Combine(data.path, errFileName);

            WriteAllBytes(outFilePath, stderr);
            WriteAllBytes(errFilePath, stdout);

            var appender = new ResetLogAppender(data.path, data.name, false, false, outFileExt, errFileExt);
            appender.Log(new(new MemoryStream(stdout)), new(new MemoryStream(stderr)));

            Assert.True(Exists(outFilePath));
            Assert.True(Exists(errFilePath));

            Assert.Equal(stdout, ReadAllBytes(outFilePath));
            Assert.Equal(stderr, ReadAllBytes(errFilePath));
        }

        [Fact]
        public void IgnoreLogAppender()
        {
            byte[] stdout = { 0x4e, 0x65, 0x78, 0x74 };
            byte[] stderr = { 0x54, 0x75, 0x72, 0x6e };

            using var data = TestData.Create();

            string baseName = data.name;
            string outFileExt = ".out.log";
            string errFileExt = ".err.log";
            string outFileName = baseName + outFileExt;
            string errFileName = baseName + errFileExt;
            string outFilePath = Path.Combine(data.path, outFileName);
            string errFilePath = Path.Combine(data.path, errFileName);

            var appender = new IgnoreLogAppender();
            appender.Log(new(new MemoryStream(stdout)), new(new MemoryStream(stderr)));

            Assert.False(Exists(outFilePath));
            Assert.False(Exists(errFilePath));
        }

        [Fact]
        public void SizeBasedRollingLogAppender()
        {
            byte[] stdout = { 0x4e, 0x65, CR, LF, 0x78, 0x74 };
            byte[] stderr = { 0x54, 0x75, CR, LF, 0x72, 0x6e };

            using var data = TestData.Create();

            string baseName = data.name;
            string outFileExt = ".out.log";
            string errFileExt = ".err.log";

            var appender = new SizeBasedRollingLogAppender(data.path, data.name, false, false, outFileExt, errFileExt, 3, 2);
            appender.Log(new(new MemoryStream(stdout)), new(new MemoryStream(stderr)));

            Assert.Equal(stdout.Take(4), ReadAllBytes(Path.Combine(data.path, baseName + ".0" + outFileExt)));
            Assert.Equal(stdout.Skip(4), ReadAllBytes(Path.Combine(data.path, baseName + outFileExt)));
            Assert.Equal(stderr.Take(4), ReadAllBytes(Path.Combine(data.path, baseName + ".0" + errFileExt)));
            Assert.Equal(stderr.Skip(4), ReadAllBytes(Path.Combine(data.path, baseName + errFileExt)));
        }

        private readonly ref struct TestData
        {
            internal readonly string name;
            internal readonly string path;

            private TestData(string name, string path)
            {
                this.name = name;
                this.path = path;
            }

            internal static TestData Create([CallerMemberName] string name = null)
            {
                string path = Path.Combine(Path.GetTempPath(), name);
                _ = Directory.CreateDirectory(path);

                return new(name, path);
            }

            public void Dispose()
            {
                Directory.Delete(this.path, true);
            }
        }
    }
}
