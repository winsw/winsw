#if NETFRAMEWORK
using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using WinSW.Tests.Util;
using Xunit;

namespace WinSW.Tests
{
    public sealed class MetadataTests
    {
        [Fact]
        public void Extern()
        {
            using var peReader = new PEReader(File.OpenRead(Layout.NET461Exe));
            var metadataReader = peReader.GetMetadataReader();
            foreach (var handle in metadataReader.AssemblyReferences)
            {
                var assembly = metadataReader.GetAssemblyReference(handle);
                // Note the actual version may vary depending on the .NET Framework version. THis is just a smoke test
                Assert.NotNull(assembly.Version);
            }
        }
    }
}
#endif
