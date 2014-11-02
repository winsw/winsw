using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using winsw.Extensions.SharedDirectoryMapper;

namespace winswMapDirTest
{
    [TestClass]
    public class UnitTest1
    {
        SharedDirectoryMapperExtension ext = new SharedDirectoryMapperExtension("test",true, "M:", "\\\\ru20filer01\\ru20ipta\\INSTALL");

        [TestMethod]
        public void MapUnmap()
        {
           // ext.Init();
            ext.OnStart(TestLogger.Instance);
        }
    }
}
