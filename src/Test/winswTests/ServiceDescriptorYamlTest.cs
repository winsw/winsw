using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using winsw;

namespace winswTests
{
    class ServiceDescriptorYamlTest
    {

        private string MinimalYaml = @"id: myapp
caption: This is a test
executable: 'C:\Program Files\Java\jdk1.8.0_241\bin\java.exe'
description: This is test winsw";


        [Test]
        public void Simple_yaml_parsing_test()
        {
            var configs = ServiceDescriptorYaml.FromYaml(MinimalYaml).configurations;

            Assert.AreEqual("myapp", configs.Id);
            Assert.AreEqual("This is a test", configs.Caption);
            Assert.AreEqual("C:\\Program Files\\Java\\jdk1.8.0_241\\bin\\java.exe", configs.Executable);
            Assert.AreEqual("This is test winsw", configs.Description);
        }

        [Test]
        public void Must_implemented_value_test()
        {
            string yml = @"caption: This is a test
executable: 'C:\Program Files\Java\jdk1.8.0_241\bin\java.exe'
description: This is test winsw";

            void getId()
            {
                var id = ServiceDescriptorYaml.FromYaml(yml).configurations.Id;
            }

            Assert.That(() => getId(), Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void Default_value_map_test()
        {
            var executablePath = ServiceDescriptorYaml.FromYaml(MinimalYaml).configurations.ExecutablePath;

            Assert.IsNotNull(executablePath);
        }

        [Test]
        public void Simple_download_parsing_test()
        {
            var yml = @"download:
    -
        from: www.sample.com
        to: c://tmp
    -
        from: www.sample2.com
        to: d://tmp
    -
        from: www.sample3.com
        to: d://temp";

            var configs = ServiceDescriptorYaml.FromYaml(yml).configurations;

            Assert.AreEqual(3, configs.Downloads.Count);
        }


        [Test]
        public void Log_defaults_when_log_not_specified()
        {
            var configs = ServiceDescriptorYaml.FromYaml(MinimalYaml).configurations;
            Assert.AreEqual(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName)!, configs.LogDirectory);
        }

        [Test]
        public void Log_defaults_when_log_specified_and_field_not_specified()
        {
            var yaml = @"id: myapp
caption: This is a test
executable: 'C:\Program Files\Java\jdk1.8.0_241\bin\java.exe'
description: This is test winsw
log:
  mode: rotate";

            var configs = ServiceDescriptorYaml.FromYaml(yaml).configurations;
            Assert.AreEqual(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName)!, configs.LogDirectory);
        }

        [Test]
        public void Log_defaults_when_log_and_field_specified()
        {
            var yaml = @"id: myapp
caption: This is a test
executable: 'C:\Program Files\Java\jdk1.8.0_241\bin\java.exe'
description: This is test winsw
log:
  mode: rotate";

            var configs = ServiceDescriptorYaml.FromYaml(yaml).configurations;
            Assert.AreEqual("rotate", configs.LogMode);
        }
    }
}
