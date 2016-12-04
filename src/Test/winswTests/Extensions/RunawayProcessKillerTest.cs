using winsw;
using NUnit.Framework;
using winsw.Extensions;
using winsw.Plugins.SharedDirectoryMapper;
using winsw.Plugins.RunawayProcessKiller;

namespace winswTests.extensions
{
    [TestFixture]
    class RunawayProcessKillerExtensionTest
    {
        ServiceDescriptor _testServiceDescriptor;

        [SetUp]
        public void SetUp()
        {
            string testExtension = typeof (RunawayProcessKillerExtension).ToString();
            string seedXml = "<?xml version=\"1.0\" encoding=\"utf-8\" ?>"
                + "<service>                                                                                                        "
                + "  <id>SERVICE_NAME</id>                                                                                          "
                + "  <name>Jenkins Slave</name>                                                                                     "
                + "  <description>This service runs a slave for Jenkins continuous integration system.</description>                "
                + "  <executable>C:\\Program Files\\Java\\jre7\\bin\\java.exe</executable>                                               "
                + "  <arguments>-Xrs  -jar \\\"%BASE%\\slave.jar\\\" -jnlpUrl ...</arguments>                                              "
                + "  <logmode>rotate</logmode>                                                                                      "
                + "  <extensions>                                                                                                   "
                + "    <extension enabled=\"true\" className=\"" + testExtension + "\" id=\"mapNetworDirs\"> "
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
            var extension = manager.Extensions[typeof(RunawayProcessKillerExtension).ToString()] as RunawayProcessKillerExtension;
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
    }
}
