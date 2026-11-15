using System;
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

        [Fact]
        public void TimeBasedRollingLogAppender_PurgeOldFiles_RemovesExcessFiles()
        {
            using var data = TestData.Create();

            string outFileExt = ".out.log";
            const int keepFiles = 3;

            for (int i = 0; i < 5; i++)
            {
                string filePath = Path.Combine(data.path, $"{data.name}_{i:D8}{outFileExt}");
                File.WriteAllText(filePath, "old log");
                File.SetLastWriteTime(filePath, DateTime.Now.AddDays(-i));
            }

            var appender = new TimeBasedRollingLogAppender(
                data.path, data.name,
                false, false,
                outFileExt, ".err.log",
                "yyyyMMdd", 1,
                filesToKeep: keepFiles);

            appender.PurgeOldFiles(outFileExt);

            var remaining = Directory.GetFiles(data.path, $"{data.name}_*{outFileExt}");
            Assert.Equal(keepFiles, remaining.Length);
        }

        [Fact]
        public void TimeBasedRollingLogAppender_PurgeOldFiles_NoLimitKeepsAllFiles()
        {
            using var data = TestData.Create();

            string outFileExt = ".out.log";
            const int totalFiles = 5;

            for (int i = 0; i < totalFiles; i++)
            {
                File.WriteAllText(Path.Combine(data.path, $"{data.name}_{i:D8}{outFileExt}"), "log");
            }

            var appender = new TimeBasedRollingLogAppender(
                data.path, data.name,
                false, false,
                outFileExt, ".err.log",
                "yyyyMMdd", 1,
                filesToKeep: -1);

            appender.PurgeOldFiles(outFileExt);

            var remaining = Directory.GetFiles(data.path, $"{data.name}_*{outFileExt}");
            Assert.Equal(totalFiles, remaining.Length);
        }

        private readonly struct TestData : IDisposable
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