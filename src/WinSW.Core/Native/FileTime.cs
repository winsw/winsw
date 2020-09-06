using System;

namespace WinSW.Native
{
    internal struct FileTime
    {
        internal int LowDateTime;
        internal int HighDateTime;

        public DateTime ToDateTime() => DateTime.FromFileTime(((long)this.HighDateTime << 32) + this.LowDateTime);
    }
}
