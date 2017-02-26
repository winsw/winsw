using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using winsw;
using winswTests.Util;

namespace winswTests
{
    [TestFixture]
    class DownloadTest
    {
        private const string From = "http://www.nosuchhostexists.foo.myorg/foo.xml";
        private const string To = "%BASE%\\foo.xml";

        /// <summary>
        /// Ensures that the fail-on-error field is being processed correctly.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void Download_FailOnError(bool failOnError)
        {
            Download d = new Download(From, To, failOnError);

            var sd = ConfigXmlBuilder.create()
                .WithDownload(d)
                .ToServiceDescriptor(true);

            var loaded = getSingleEntry(sd);
            Assert.That(loaded.From, Is.EqualTo(From));
            Assert.That(loaded.To, Is.EqualTo(To));
            Assert.That(loaded.FailOnError, Is.EqualTo(failOnError), "Unexpected FailOnError value");
        }

        /// <summary>
        /// Ensures that the fail-on-error field is being processed correctly.
        /// </summary>
        [Test]
        public void Download_FailOnError_Undefined()
        {
            var sd = ConfigXmlBuilder.create()
                .WithRawEntry("<download from=\"http://www.nosuchhostexists.foo.myorg/foo.xml\" to=\"%BASE%\\foo.xml\"/>")
                .ToServiceDescriptor(true);

            var loaded = getSingleEntry(sd);
            Assert.That(loaded.FailOnError, Is.False);
        }

        private Download getSingleEntry(ServiceDescriptor sd)
        {
            var downloads = sd.Downloads.ToArray();
            Assert.That(downloads.Length, Is.EqualTo(1), "Service Descriptor is expected to have only one entry");
            return downloads[0];
        }
    }
}
