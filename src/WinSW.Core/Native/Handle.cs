using System;
using System.Runtime.InteropServices;
using static WinSW.Native.HandleApis;

namespace WinSW.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal readonly ref struct Handle
    {
        private readonly IntPtr handle;

        internal Handle(IntPtr handle) => this.handle = handle;

        public void Dispose() => CloseHandle(this.handle);

        public static implicit operator IntPtr(Handle value) => value.handle;
    }
}
