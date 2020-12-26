using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace WinSW.Native
{
    internal static class Throw
    {
        internal static class Command
        {
            [DoesNotReturn]
            [MethodImpl(MethodImplOptions.NoInlining)]
            internal static void Exception(Exception inner)
            {
                throw new CommandException(inner);
            }

            [DoesNotReturn]
            [MethodImpl(MethodImplOptions.NoInlining)]
            internal static void Exception(string message)
            {
                Debug.Assert(message.EndsWith("."));
                throw new CommandException(message);
            }

            [DoesNotReturn]
            [MethodImpl(MethodImplOptions.NoInlining)]
            internal static void Exception(string message, Exception inner)
            {
                Debug.Assert(message.EndsWith("."));
                throw new CommandException(message + ' ' + inner.Message, inner);
            }

            [DoesNotReturn]
            [MethodImpl(MethodImplOptions.NoInlining)]
            internal static void Win32Exception(int error)
            {
                Debug.Assert(error != 0);
                throw new CommandException(new Win32Exception(error));
            }

            [DoesNotReturn]
            [MethodImpl(MethodImplOptions.NoInlining)]
            internal static void Win32Exception(int error, string message)
            {
                Debug.Assert(error != 0);
                var inner = new Win32Exception(error);
                Debug.Assert(message.EndsWith("."));
                throw new CommandException(message + ' ' + inner.Message, inner);
            }

            [DoesNotReturn]
            [MethodImpl(MethodImplOptions.NoInlining)]
            internal static void Win32Exception()
            {
                var inner = new Win32Exception();
                Debug.Assert(inner.NativeErrorCode != 0);
                throw new CommandException(inner);
            }

            [DoesNotReturn]
            [MethodImpl(MethodImplOptions.NoInlining)]
            internal static void Win32Exception(string message)
            {
                var inner = new Win32Exception();
                Debug.Assert(inner.NativeErrorCode != 0);
                Debug.Assert(message.EndsWith("."));
                throw new CommandException(message + ' ' + inner.Message, inner);
            }
        }
    }
}
