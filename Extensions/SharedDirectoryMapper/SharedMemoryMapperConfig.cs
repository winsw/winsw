using System;
using System.Collections.Generic;
using System.Text;

namespace winsw.Extensions.SharedDirectoryMapper
{
    class SharedMemoryMapperConfig
    {
        public bool EnableMapping { get; set; }
        public String Label { get; set; }
        public String SharedDirPath { get; set; }
    }
}
