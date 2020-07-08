﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using WinSW;
using WinSW.Extensions;
using WinSW.Plugins.RunawayProcessKiller;
using WinSW.Util;
using winswTests.Util;

namespace winswTests.Extensions
{
    [TestFixture]
    class RunawayProcessKillerExtensionTest : ExtensionTestBase
    {
        ServiceDescriptor _testServiceDescriptor;

        readonly string testExtension = GetExtensionClassNameWithAssembly(typeof(RunawayProcessKillerExtension));

        [SetUp]
        public void SetUp()
        {
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
      <stopParentFirst>true</stopParentFirst>
    </extension>
  </extensions>
</service>";
            this._testServiceDescriptor = ServiceDescriptor.FromXML(seedXml);
        }

        [Test]
        public void LoadExtensions()
        {
            WinSWExtensionManager manager = new WinSWExtensionManager(this._testServiceDescriptor);
            manager.LoadExtensions();
            Assert.AreEqual(1, manager.Extensions.Count, "One extension should be loaded");

            // Check the file is correct
            var extension = manager.Extensions["killRunawayProcess"] as RunawayProcessKillerExtension;
            Assert.IsNotNull(extension, "RunawayProcessKillerExtension should be loaded");
            Assert.AreEqual("foo/bar/pid.txt", extension.Pidfile, "Loaded PID file path is not equal to the expected one");
            Assert.AreEqual(5000, extension.StopTimeout.TotalMilliseconds, "Loaded Stop Timeout is not equal to the expected one");
            Assert.AreEqual(true, extension.StopParentProcessFirst, "Loaded StopParentFirst is not equal to the expected one");
        }

        [Test]
        public void StartStopExtension()
        {
            WinSWExtensionManager manager = new WinSWExtensionManager(this._testServiceDescriptor);
            manager.LoadExtensions();
            manager.FireOnWrapperStarted();
            manager.FireBeforeWrapperStopped();
        }

        [Test]
        public void ShouldKillTheSpawnedProcess()
        {
            Assert.Ignore();

            var winswId = "myAppWithRunaway";
            var extensionId = "runawayProcessKiller";
            var tmpDir = FilesystemTestHelper.CreateTmpDirectory();

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
                var sd = ConfigXmlBuilder.create(id: winswId)
                    .WithRunawayProcessKiller(new RunawayProcessKillerExtension(pidfile), extensionId)
                    .ToServiceDescriptor();
                WinSWExtensionManager manager = new WinSWExtensionManager(sd);
                manager.LoadExtensions();
                var extension = manager.Extensions[extensionId] as RunawayProcessKillerExtension;
                Assert.IsNotNull(extension, "RunawayProcessKillerExtension should be loaded");
                Assert.AreEqual(pidfile, extension.Pidfile, "PidFile should have been retained during the config roundtrip");

                // Inject PID
                File.WriteAllText(pidfile, proc.Id.ToString());

                // Try to terminate
                Assert.That(!proc.HasExited, "Process " + proc + " has exited before the RunawayProcessKiller extension invocation");
                _ = proc.StandardOutput.Read();
                extension.OnWrapperStarted();
                Assert.That(proc.HasExited, "Process " + proc + " should have been terminated by RunawayProcessKiller");
            }
            finally
            {
                if (!proc.HasExited)
                {
                    Console.Error.WriteLine("Test: Killing runaway process with ID=" + proc.Id);
                    ProcessHelper.StopProcessAndChildren(proc.Id, TimeSpan.FromMilliseconds(100), false);
                    if (!proc.HasExited)
                    {
                        // The test is failed here anyway, but we add additional diagnostics info
                        Console.Error.WriteLine("Test: ProcessHelper failed to properly terminate process with ID=" + proc.Id);
                    }
                }
            }
        }
    }
}
