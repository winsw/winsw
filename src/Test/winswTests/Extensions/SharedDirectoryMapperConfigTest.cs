using winsw;
using winsw.Extensions;
using winsw.Plugins.SharedDirectoryMapper;
using Xunit;

namespace winswTests.Extensions
{
    public class SharedDirectoryMapperConfigTest : ExtensionTestBase
    {
        readonly ServiceDescriptor _testServiceDescriptor;

        readonly string testExtension = GetExtensionClassNameWithAssembly(typeof(SharedDirectoryMapper));

        public SharedDirectoryMapperConfigTest()
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
    <extension enabled=""true"" className=""{testExtension}"" id=""mapNetworDirs"">
      <mapping>
        <map enabled=""false"" label=""N:"" uncpath=""\\UNC""/>
        <map enabled=""false"" label=""M:"" uncpath=""\\UNC2""/>
      </mapping>
    </extension>
    <extension enabled=""true"" className=""{testExtension}"" id=""mapNetworDirs2"">
      <mapping>
        <map enabled=""false"" label=""X:"" uncpath=""\\UNC""/>
        <map enabled=""false"" label=""Y:"" uncpath=""\\UNC2""/>
      </mapping>
    </extension>
  </extensions>
</service>";
            _testServiceDescriptor = ServiceDescriptor.FromXML(seedXml);
        }

        [Fact]
        public void LoadExtensions()
        {
            WinSWExtensionManager manager = new WinSWExtensionManager(_testServiceDescriptor);
            manager.LoadExtensions();
            Assert.Equal(2, manager.Extensions.Count);
        }

        [Fact]
        public void StartStopExtension()
        {
            WinSWExtensionManager manager = new WinSWExtensionManager(_testServiceDescriptor);
            manager.LoadExtensions();
            manager.FireOnWrapperStarted();
            manager.FireBeforeWrapperStopped();
        }
    }
}
