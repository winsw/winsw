using System;
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
        public void Download_not_specified_test()
        {
            var yml = @"id: jenkins
name: No Service Account
";

            var configs = ServiceDescriptorYaml.FromYaml(yml).configurations;

            Assert.DoesNotThrow(() =>
            {
                var dowloads = configs.Downloads;
            });
        }

        [Test]
        public void Service_account_not_specified_test()
        {
            var yml = @"id: jenkins
name: No Service Account
";

            var configs = ServiceDescriptorYaml.FromYaml(yml).configurations;

            Assert.DoesNotThrow(() =>
            {
                var serviceAccount = configs.ServiceAccount.AllowServiceAcountLogonRight;
            });
        }

        [Test]
        public void Service_account_specified_but_fields_not_specified()
        {
            var yml = @"id: jenkins
name: No Service Account
serviceaccount:
  user: testuser
";

            var configs = ServiceDescriptorYaml.FromYaml(yml).configurations;

            Assert.DoesNotThrow(() =>
            {
                var serviceAccountName = configs.ServiceAccount.Name;
                Console.WriteLine(serviceAccountName);
            });
        }
    }
}
