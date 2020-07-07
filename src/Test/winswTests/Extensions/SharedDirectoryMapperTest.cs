using NUnit.Framework;
using WinSW;
using WinSW.Extensions;
using WinSW.Plugins.SharedDirectoryMapper;

namespace winswTests.Extensions
{
    [TestFixture]
    class SharedDirectoryMapperTest : ExtensionTestBase
    {
        ServiceDescriptor _testServiceDescriptor;

        readonly string testExtension = GetExtensionClassNameWithAssembly(typeof(SharedDirectoryMapper));

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
    <extension enabled=""true"" className=""{this.testExtension}"" id=""mapNetworDirs"">
      <mapping>
        <map enabled=""false"" label=""N:"" uncpath=""\\UNC""/>
        <map enabled=""false"" label=""M:"" uncpath=""\\UNC2""/>
      </mapping>
    </extension>
    <extension enabled=""true"" className=""{this.testExtension}"" id=""mapNetworDirs2"">
      <mapping>
        <map enabled=""false"" label=""X:"" uncpath=""\\UNC""/>
        <map enabled=""false"" label=""Y:"" uncpath=""\\UNC2""/>
      </mapping>
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
            Assert.AreEqual(2, manager.Extensions.Count, "Two extensions should be loaded");
        }

        [Test]
        public void StartStopExtension()
        {
            WinSWExtensionManager manager = new WinSWExtensionManager(this._testServiceDescriptor);
            manager.LoadExtensions();
            manager.FireOnWrapperStarted();
            manager.FireBeforeWrapperStopped();
        }
    }
}
