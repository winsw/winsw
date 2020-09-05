using WinSW.Extensions;
using WinSW.Plugins.SharedDirectoryMapper;
using Xunit;

namespace WinSW.Tests.Extensions
{
    public class SharedDirectoryMapperConfigTest : ExtensionTestBase
    {
        private readonly XmlServiceConfig serviceConfig;

        private readonly string testExtension = GetExtensionClassNameWithAssembly(typeof(SharedDirectoryMapper));

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
            this.serviceConfig = XmlServiceConfig.FromXml(seedXml);
        }

        [Fact]
        public void LoadExtensions()
        {
            var manager = new WinSWExtensionManager(this.serviceConfig);
            manager.LoadExtensions();
            Assert.Equal(2, manager.Extensions.Count);
        }

        [Fact]
        public void StartStopExtension()
        {
            var manager = new WinSWExtensionManager(this.serviceConfig);
            manager.LoadExtensions();
            manager.FireOnWrapperStarted();
            manager.FireBeforeWrapperStopped();
        }
    }
}
