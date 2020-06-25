﻿using System;
using NUnit.Framework;
using winsw;

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
        auth: none

    -
        from: www.msd.com
        to: d://docs
        auth: sspi";

            var sd = ServiceDescriptorYaml.FromYaml(yaml);
            
            foreach(Download item in sd.configurations.Downloads)
            {
                Console.WriteLine(item.From);
                Console.WriteLine(item.To);
                Console.WriteLine(item.Auth);
            }
        }
    }
}
