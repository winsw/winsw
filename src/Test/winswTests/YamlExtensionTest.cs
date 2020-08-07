using NUnit.Framework;
using WinSW;
using WinSW.Configuration;
using WinSW.Extensions;
using WinSW.Plugins.RunawayProcessKiller;
using WinSW.Util;
using winswTests.Extensions;

namespace winswTests
{
    public class YamlExtensionTest : ExtensionTestBase
    {
        private IWinSWConfiguration _testServiceDescriptor { get; set; }

        readonly string testExtension = GetExtensionClassNameWithAssembly(typeof(RunawayProcessKillerExtension));

        [SetUp]
        public void SetUp()
        {

            string yamlDoc = $@"id: SERVICE_NAME
name: Jenkins Slave
description: This service runs Jenkins automation server.
executable: java
arguments: -Xrs  -jar \""%BASE%\slave.jar\"" -jnlpUrl ...
extensions:
    - id: killOnStartup
      enabled: yes
      classname: ""{this.testExtension}""
      settings:
            pidfile: pid.txt
            stopTimeOut: 5000
            StopParentFirst: true";

            this._testServiceDescriptor = ServiceDescriptorYaml.FromYaml(yamlDoc).Configurations;
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
        public void Sample_test()
        {
            ExtensionConfigurationProvider provider = new ExtensionConfigurationProvider(this._testServiceDescriptor);
            var config = provider.GetExtenstionConfiguration("killOnStartup");

            var pid = config.Settings.On("pidfile").AsString();
            var stopTimeOut = config.Settings.On("stopTimeOut").AsString();
            var StopParentFirst = config.Settings.On("StopParentFirst").AsString();

            System.Console.WriteLine(pid);
            System.Console.WriteLine(stopTimeOut);
            System.Console.WriteLine(StopParentFirst);
        }
    }
}
