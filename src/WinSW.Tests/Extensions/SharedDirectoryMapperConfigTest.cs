using NUnit.Framework;
using WinSW;
using WinSW.Configuration;
using WinSW.Extensions;
using WinSW.Plugins;

namespace winswTests.Extensions
{
    [TestFixture]
    class SharedDirectoryMapperConfigTest : ExtensionTestBase
    {
        IServiceConfig _testServiceDescriptor;
        IServiceConfig _testServiceDescriptorYaml;

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
            this._testServiceDescriptor = XmlServiceConfig.FromXML(seedXml);

            string seedYaml = $@"---
id: jenkins
name: Jenkins
description: This service runs Jenkins automation server.
env:
    -
        name: JENKINS_HOME
        value: '%LocalAppData%\Jenkins.jenkins'
executable: java
arguments: >-
    -Xrs -Xmx256m -Dhudson.lifecycle=hudson.lifecycle.WindowsServiceLifecycle
    -jar E:\Winsw Test\yml6\jenkins.war --httpPort=8081
extensions:
    - id: mapNetworDirs
      className: ""{this.testExtension}""
      enabled: true
      settings:
          mapping: 
              - enabled: false
                label: N
                uncPath: \\UNC
              - enabled: false
                label: M
                uncPath: \\UNC2
    - id: mapNetworDirs2
      className: ""{this.testExtension}""
      enabled: true
      settings:
          mapping: 
              - enabled: false
                label: X
                uncPath: \\UNC
              - enabled: false
                label: Y
                uncPath: \\UNC2";

            this._testServiceDescriptorYaml = YamlServiceConfig.FromYaml(seedYaml);
        }

        [Test]
        public void LoadExtensions()
        {
            var manager = new WinSWExtensionManager(this._testServiceDescriptor);
            manager.LoadExtensions();
            Assert.AreEqual(2, manager.Extensions.Count, "Two extensions should be loaded");
        }

        [Test]
        public void LoadExtensionsYaml()
        {
            var manager = new WinSWExtensionManager(this._testServiceDescriptorYaml);
            manager.LoadExtensions();
            Assert.AreEqual(2, manager.Extensions.Count, "Two extensions should be loaded");
        }

        [Test]
        public void StartStopExtension()
        {
            var manager = new WinSWExtensionManager(this._testServiceDescriptor);
            manager.LoadExtensions();
            manager.FireOnWrapperStarted();
            manager.FireBeforeWrapperStopped();
        }
    }
}
