using System;
using System.IO;
using Xunit;

namespace WinSW.Tests.Util
{
    internal static class Layout
    {
        private static string repositoryRoot;
        private static string artifactsDirectory;

        internal static string RepositoryRoot
        {
            get
            {
                if (repositoryRoot != null)
                {
                    return repositoryRoot;
                }

                string directory = Environment.CurrentDirectory;
                while (true)
                {
                    if (File.Exists(Path.Combine(directory, ".gitignore")))
                    {
                        break;
                    }

                    directory = Path.GetDirectoryName(directory);
                    Assert.NotNull(directory);
                }

                return repositoryRoot = directory;
            }
        }

        internal static string ArtifactsDirectory => artifactsDirectory ??= Path.Combine(RepositoryRoot, "artifacts");

        internal static string NET461Exe => Path.Combine(ArtifactsDirectory, "publish", "WinSW-net461.exe");

        internal static string WinSWExe =>
#if NET
            Path.ChangeExtension(typeof(Program).Assembly.Location, ".exe");
#else
            typeof(Program).Assembly.Location;
#endif
    }
}
