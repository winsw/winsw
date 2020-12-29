#if NET
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using WinSW.Plugins;

namespace winswTests.Extensions
{
    // TODO: Throws.TypeOf<ExtensionException>()
    [TestFixture]
    public class SharedDirectoryMapperTests
    {
        [Test]
        public void TestMap()
        {
            using var data = TestData.Create();

            const string label = "W:";
            var mapper = new SharedDirectoryMapper(true, $@"\\{Environment.MachineName}\{data.name}", label);

            mapper.OnWrapperStarted();
            Assert.That($@"{label}\", Does.Exist);
            mapper.BeforeWrapperStopped();
            Assert.That($@"{label}\", Does.Not.Exist);
        }

        [Test]
        public void TestDisableMapping()
        {
            using var data = TestData.Create();

            const string label = "W:";
            var mapper = new SharedDirectoryMapper(enableMapping: false, $@"\\{Environment.MachineName}\{data.name}", label);

            mapper.OnWrapperStarted();
            Assert.That($@"{label}\", Does.Not.Exist);
            mapper.BeforeWrapperStopped();
        }

        [Test]
        public void TestMap_PathEndsWithSlash_Throws()
        {
            using var data = TestData.Create();

            const string label = "W:";
            var mapper = new SharedDirectoryMapper(true, $@"\\{Environment.MachineName}\{data.name}\", label);

            Assert.That(() => mapper.OnWrapperStarted(), Throws.Exception);
            Assert.That($@"{label}\", Does.Not.Exist);
            Assert.That(() => mapper.BeforeWrapperStopped(), Throws.Exception);
        }

        [Test]
        public void TestMap_LabelDoesNotEndWithColon_Throws()
        {
            using var data = TestData.Create();

            const string label = "W";
            var mapper = new SharedDirectoryMapper(true, $@"\\{Environment.MachineName}\{data.name}", label);

            Assert.That(() => mapper.OnWrapperStarted(), Throws.Exception);
            Assert.That($@"{label}\", Does.Not.Exist);
            Assert.That(() => mapper.BeforeWrapperStopped(), Throws.Exception);
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

                try
                {
                    var shareInfo = new NativeMethods.SHARE_INFO_2
                    {
                        netname = name,
                        type = NativeMethods.STYPE_DISKTREE | NativeMethods.STYPE_TEMPORARY,
                        max_uses = unchecked((uint)-1),
                        path = path,
                    };

                    uint error = NativeMethods.NetShareAdd(null, 2, shareInfo, out _);
                    Assert.That(error, Is.Zero);

                    return new TestData(name, path);
                }
                catch
                {
                    Directory.Delete(path);
                    throw;
                }
            }

            public void Dispose()
            {
                try
                {
                    uint error = NativeMethods.NetShareDel(null, this.name);
                    Assert.That(error, Is.Zero);
                }
                finally
                {
                    Directory.Delete(this.path);
                }
            }
        }

        private static class NativeMethods
        {
            internal const uint STYPE_DISKTREE = 0;
            internal const uint STYPE_TEMPORARY = 0x40000000;

            private const string Netapi32LibraryName = "netapi32.dll";

            [DllImport(Netapi32LibraryName, CharSet = CharSet.Unicode)]
            internal static extern uint NetShareAdd(string servername, uint level, in SHARE_INFO_2 buf, out uint parm_err);

            [DllImport(Netapi32LibraryName, CharSet = CharSet.Unicode)]
            internal static extern uint NetShareDel(string servername, string netname, uint reserved = 0);

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            internal struct SHARE_INFO_2
            {
                public string netname;
                public uint type;
                public string remark;
                public uint permissions;
                public uint max_uses;
                public uint current_uses;
                public string path;
                public string passwd;
            }
        }
    }
}
#endif
