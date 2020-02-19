using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace WinSW.Native
{
    internal static class Throw
    {
        /// <exception cref="System.ComponentModel.Win32Exception" />
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Win32Exception(string message)
        {
            Win32Exception inner = new Win32Exception();
            Debug.Assert(inner.NativeErrorCode != 0);
            Debug.Assert(message.EndsWith("."));
            throw new Win32Exception(inner.NativeErrorCode, message + ' ' + inner.Message);
        }
    }
}
