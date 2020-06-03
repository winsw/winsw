using System;
using NUnit.Framework;
using winsw;
using winsw.Configuration;

namespace winswTests
{
    class ServiceDescriptorYamlTest
    {
        [Test]
        public void YamlDeserializingTest()
        {
            string yaml = @"id: myapp
name: myappname
description: appdescription
serviceaccount:
    name: buddhika
    user: hackerbuddy
log:
    sizeThreshold: 45
download:
    -
        from: www.github.com
        to: c://documents
    -
        from: www.msd.com
        to: d://docs";

            var sd = ServiceDescriptorYaml.FromYaml(yaml);
            
            foreach(Downloading item in sd.configurations.download)
            {
                Console.WriteLine(item.from);
                Console.WriteLine(item.to);
            }
        }
    }
}
