using System;
using System.IO;
using System.Net;
using System.Xml;

namespace winsw
{
    /// <summary>
    /// Specify the download activities prior to the launch.
    /// This enables self-updating services.
    /// </summary>
    public class Download
    {
        public readonly string From;
        public readonly string To;
        public readonly bool FailOnError;

        public Download(string from, string to, bool failOnError = false)
        {
            From = from;
            To = to;
            FailOnError = failOnError;
        }

        internal Download(XmlNode n)
        {
            From = Environment.ExpandEnvironmentVariables(n.Attributes["from"].Value);
            To = Environment.ExpandEnvironmentVariables(n.Attributes["to"].Value);
            
            var failOnErrorNode = n.Attributes["failOnError"];
            FailOnError = failOnErrorNode != null ? Boolean.Parse(failOnErrorNode.Value) : false;
        }

        /// <summary>
        ///     Downloads the requested file and puts it to the specified target.
        /// </summary>
        /// <exception cref="System.Net.WebException">
        ///     Download failure. FailOnError flag should be processed outside.
        /// </exception>
        public void Perform()
        {
            WebRequest req = WebRequest.Create(From);
            WebResponse rsp = req.GetResponse();
            FileStream tmpstream = new FileStream(To + ".tmp", FileMode.Create);
            CopyStream(rsp.GetResponseStream(), tmpstream);
            // only after we successfully downloaded a file, overwrite the existing one
            if (File.Exists(To))
                File.Delete(To);
            File.Move(To + ".tmp", To);
        }

        /// <summary>
        /// Produces the XML configuuration entry.
        /// </summary>
        /// <returns>XML String for the configuration file</returns>
        public String toXMLConfig()
        {
            return "<download from=\"" + From + "\" to=\"" + To + "\" failOnError=\"" + FailOnError + "\"/>";
        }

        private static void CopyStream(Stream i, Stream o)
        {
            byte[] buf = new byte[8192];
            while (true)
            {
                int len = i.Read(buf, 0, buf.Length);
                if (len <= 0) break;
                o.Write(buf, 0, len);
            }
            i.Close();
            o.Close();
        }
    }
}
