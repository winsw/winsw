using System;
using NUnit.Framework;
using WinSW;
using WinSW.Configuration;
using WinSW.Native;

namespace winswTests
{
    class ServiceDescriptorYamlTest
    {

        private readonly string MinimalYaml = @"id: myapp
name: This is a test
executable: 'C:\Program Files\Java\jdk1.8.0_241\bin\java.exe'
description: This is test winsw";

        private readonly DefaultWinSWSettings Defaults = new DefaultWinSWSettings();

        [Test]
        public void Parse_must_implemented_value_test()
        {
            var yml = @"name: This is a test
executable: 'C:\Program Files\Java\jdk1.8.0_241\bin\java.exe'
description: This is test winsw";

            Assert.That(() =>
            {
                _ = ServiceDescriptorYaml.FromYaml(yml).Configurations.Id;
            }, Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void Default_value_map_test()
        {
            var configs = ServiceDescriptorYaml.FromYaml(MinimalYaml).Configurations;

            Assert.IsNotNull(configs.ExecutablePath);
            Assert.IsNotNull(configs.BaseName);
            Assert.IsNotNull(configs.BasePath);
        }

        [Test]
        public void Parse_downloads()
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

            var configs = ServiceDescriptorYaml.FromYaml(yml).Configurations;

            Assert.AreEqual(3, configs.Downloads.Count);
            Assert.AreEqual("www.sample.com", configs.Downloads[0].From);
            Assert.AreEqual("c://tmp", configs.Downloads[0].To);
        }

        [Test]
        public void Parse_serviceaccount()
        {
            var yml = @"id: myapp
name: winsw
description: yaml test
executable: java
serviceaccount:
    user: testuser
    domain: mydomain
    password: pa55w0rd
    allowservicelogon: yes";

            var serviceAccount = ServiceDescriptorYaml.FromYaml(yml).Configurations.ServiceAccount;

            Assert.AreEqual("mydomain\\testuser", serviceAccount.ServiceAccountUser);
            Assert.AreEqual(true, serviceAccount.AllowServiceAcountLogonRight);
            Assert.AreEqual("pa55w0rd", serviceAccount.ServiceAccountPassword);
            Assert.AreEqual(true, serviceAccount.HasServiceAccount());
        }

        [Test]
        public void Parse_environment_variables()
        {
            var yml = @"id: myapp
name: WinSW
executable: java
description: env test
env:
    -
        name: MY_TOOL_HOME
        value: 'C:\etc\tools\myTool'
    -
        name: LM_LICENSE_FILE
        value: host1;host2";

            var envs = ServiceDescriptorYaml.FromYaml(yml).Configurations.EnvironmentVariables;

            Assert.That(@"C:\etc\tools\myTool", Is.EqualTo(envs["MY_TOOL_HOME"]));
            Assert.That("host1;host2", Is.EqualTo(envs["LM_LICENSE_FILE"]));
        }

        [Test]
        public void Parse_log()
        {
            var yml = @"id: myapp
name: winsw
description: yaml test
executable: java
log:
    mode: roll
    logpath: 'D://winsw/logs'";

            var config = ServiceDescriptorYaml.FromYaml(yml).Configurations;

            Assert.AreEqual("roll", config.LogMode);
            Assert.AreEqual("D://winsw/logs", config.LogDirectory);
        }

        [Test]
        public void Parse_onfailure_actions()
        {
            var yml = @"id: myapp
name: winsw
description: yaml test
executable: java
onFailure:
    -
        action: restart
        delay: 5 sec
    - 
        action: reboot
        delay: 10 min";

            var onFailure = ServiceDescriptorYaml.FromYaml(yml).Configurations.FailureActions;

            Assert.That(onFailure[0].Type, Is.EqualTo(SC_ACTION_TYPE.SC_ACTION_RESTART));

            Assert.That(onFailure[1].Type, Is.EqualTo(SC_ACTION_TYPE.SC_ACTION_REBOOT));

            Assert.That(TimeSpan.FromMilliseconds(onFailure[0].Delay), Is.EqualTo(TimeSpan.FromSeconds(5)));

            Assert.That(TimeSpan.FromMilliseconds(onFailure[1].Delay), Is.EqualTo(TimeSpan.FromMinutes(10)));

        }
    }
}
