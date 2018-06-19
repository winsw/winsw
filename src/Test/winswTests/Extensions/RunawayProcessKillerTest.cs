using winsw;
using NUnit.Framework;
using winsw.Extensions;
using winsw.Plugins.SharedDirectoryMapper;
using winsw.Plugins.RunawayProcessKiller;
using winswTests.Util;
using System.IO;
using System.Diagnostics;
using winsw.Util;
using System;
using System.Collections.Generic;

namespace winswTests.Extensions
{
    [TestFixture]
    class RunawayProcessKillerExtensionTest : ExtensionTestBase
    {
        ServiceDescriptor _testServiceDescriptor;

        string testExtension = getExtensionClassNameWithAssembly(typeof(RunawayProcessKillerExtension));
            
        [SetUp]
        public void SetUp()
        {
            string seedXml = "<?xml version=\"1.0\" encoding=\"utf-8\" ?>"
                + "<service>                                                                                                        "
                + "  <id>SERVICE_NAME</id>                                                                                          "
                + "  <name>Jenkins Slave</name>                                                                                     "
                + "  <description>This service runs a slave for Jenkins continuous integration system.</description>                "
                + "  <executable>C:\\Program Files\\Java\\jre7\\bin\\java.exe</executable>                                               "
                + "  <arguments>-Xrs  -jar \\\"%BASE%\\slave.jar\\\" -jnlpUrl ...</arguments>                                              "
                + "  <logmode>rotate</logmode>                                                                                      "
                + "  <extensions>                                                                                                   "
                + "    <extension enabled=\"true\" className=\"" + testExtension + "\" id=\"killRunawayProcess\"> "
                + "      <pidfile>foo/bar/pid.txt</pidfile>"
                + "      <stopTimeout>5000</stopTimeout> "
                + "      <stopParentFirst>true</stopParentFirst>"
                + "    </extension>         "
                + "  </extensions>                                                                                                  "
                + "</service>";
            _testServiceDescriptor = ServiceDescriptor.FromXML(seedXml);
        }

        [Test]
        public void LoadExtensions()
        {
            WinSWExtensionManager manager = new WinSWExtensionManager(_testServiceDescriptor);
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
            WinSWExtensionManager manager = new WinSWExtensionManager(_testServiceDescriptor);
            manager.LoadExtensions();
            manager.FireOnWrapperStarted();
            manager.FireBeforeWrapperStopped();
        }

        [Test]
        public void ShouldKillTheSpawnedProcess()
        {
            var winswId = "myAppWithRunaway";
            var extensionId = "runawayProcessKiller";
            var tmpDir = FilesystemTestHelper.CreateTmpDirectory();
            
            // Prepare the env var
            String varName = WinSWSystem.ENVVAR_NAME_SERVICE_ID;
            var env = new Dictionary<string, string>();
            env.Add(varName, winswId);

            // Spawn the test process
            var scriptFile = Path.Combine(tmpDir, "dosleep.bat");
            var envFile = Path.Combine(tmpDir, "env.txt");
            File.WriteAllText(scriptFile, "set > " + envFile + "\nsleep 100500");
            Process proc = new Process();
            var ps = proc.StartInfo;
            ps.FileName = scriptFile;
            ProcessHelper.StartProcessAndCallbackForExit(proc, envVars: env);

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
