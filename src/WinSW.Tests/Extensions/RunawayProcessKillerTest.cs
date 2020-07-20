﻿using System;
using System.Diagnostics;
using System.IO;
using WinSW.Extensions;
using WinSW.Plugins.RunawayProcessKiller;
using WinSW.Tests.Util;
using WinSW.Util;
using Xunit;
using Xunit.Abstractions;

namespace WinSW.Tests.Extensions
{
    public class RunawayProcessKillerExtensionTest : ExtensionTestBase
    {
        private readonly ServiceDescriptor testServiceDescriptor;

        private readonly string testExtension = GetExtensionClassNameWithAssembly(typeof(RunawayProcessKillerExtension));

        private readonly ITestOutputHelper output;

        public RunawayProcessKillerExtensionTest(ITestOutputHelper output)
        {
            this.output = output;

            string seedXml =
$@"<service>
  <id>SERVICE_NAME</id>
  <name>Jenkins Slave</name>
  <description>This service runs a slave for Jenkins continuous integration system.</description>
  <executable>C:\Program Files\Java\jre7\bin\java.exe</executable>
  <arguments>-Xrs  -jar \""%BASE%\slave.jar\"" -jnlpUrl ...</arguments>
  <log mode=""roll""></log>
  <extensions>
    <extension enabled=""true"" className=""{this.testExtension}"" id=""killRunawayProcess"">
      <pidfile>foo/bar/pid.txt</pidfile>
      <stopTimeout>5000</stopTimeout>
    </extension>
  </extensions>
</service>";
            this.testServiceDescriptor = ServiceDescriptor.FromXml(seedXml);
        }

        [Fact]
        public void LoadExtensions()
        {
            WinSWExtensionManager manager = new WinSWExtensionManager(this.testServiceDescriptor);
            manager.LoadExtensions();
            _ = Assert.Single(manager.Extensions);

            // Check the file is correct
            var extension = manager.Extensions["killRunawayProcess"] as RunawayProcessKillerExtension;
            Assert.NotNull(extension);
            Assert.Equal("foo/bar/pid.txt", extension.Pidfile);
            Assert.Equal(5000, extension.StopTimeout.TotalMilliseconds);
        }

        [Fact]
        public void StartStopExtension()
        {
            WinSWExtensionManager manager = new WinSWExtensionManager(this.testServiceDescriptor);
            manager.LoadExtensions();
            manager.FireOnWrapperStarted();
            manager.FireBeforeWrapperStopped();
        }

        internal void ShouldKillTheSpawnedProcess()
        {
            var winswId = "myAppWithRunaway";
            var extensionId = "runaway-process-killer";
            var tmpDir = FilesystemTestHelper.CreateTmpDirectory();
            try
            {
                // Spawn the test process
                Process proc = new Process();
                var ps = proc.StartInfo;
                ps.FileName = "cmd.exe";
                ps.Arguments = "/c pause";
                ps.UseShellExecute = false;
                ps.RedirectStandardOutput = true;
                ps.EnvironmentVariables[WinSWSystem.EnvVarNameServiceId] = winswId;
                proc.Start();

                try
                {
                    // Generate extension and ensure that the roundtrip is correct
                    var pidfile = Path.Combine(tmpDir, "process.pid");
                    var sd = ConfigXmlBuilder.Create(this.output, id: winswId)
                        .WithRunawayProcessKiller(new RunawayProcessKillerExtension(pidfile), extensionId)
                        .ToServiceDescriptor();
                    WinSWExtensionManager manager = new WinSWExtensionManager(sd);
                    manager.LoadExtensions();
                    var extension = manager.Extensions[extensionId] as RunawayProcessKillerExtension;
                    Assert.NotNull(extension);
                    Assert.Equal(pidfile, extension.Pidfile);

                    // Inject PID
                    File.WriteAllText(pidfile, proc.Id.ToString());

                    // Try to terminate
                    Assert.False(proc.HasExited, "Process " + proc + " has exited before the RunawayProcessKiller extension invocation");
                    _ = proc.StandardOutput.Read();
                    extension.OnWrapperStarted();
                    Assert.True(proc.HasExited, "Process " + proc + " should have been terminated by RunawayProcessKiller");
                }
                finally
                {
                    if (!proc.HasExited)
                    {
                        Console.Error.WriteLine("Test: Killing runaway process with ID=" + proc.Id);
                        ProcessHelper.StopProcessTree(proc, TimeSpan.FromMilliseconds(100));
                        if (!proc.HasExited)
                        {
                            // The test is failed here anyway, but we add additional diagnostics info
                            Console.Error.WriteLine("Test: ProcessHelper failed to properly terminate process with ID=" + proc.Id);
                        }
                    }
                }
            }
            finally
            {
                Directory.Delete(tmpDir, true);
            }
        }
    }
}
