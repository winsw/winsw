#if NET461
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
            var version = new Version(4, 0, 0, 0);

            using var file = File.OpenRead(Path.Combine(Layout.ArtifactsDirectory, "WinSW.NET461.exe"));
            using var peReader = new PEReader(file);
            var metadataReader = peReader.GetMetadataReader();
            foreach (var handle in metadataReader.AssemblyReferences)
            {
                var assembly = metadataReader.GetAssemblyReference(handle);
                if (metadataReader.GetString(assembly.Name) != "System.IO.Compression")
                {
                    Assert.Equal(version, assembly.Version);
                }
            }
        }
    }
}
#endif
