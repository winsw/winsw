using System;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace WinSW
{
    internal static class CommandExtensions
    {
        internal static void SetHandler<T>(this Command command, Action<T, InvocationContext> handle, Argument<T> symbol)
        {
            command.SetHandler(context =>
            {
                var value = context.ParseResult.GetValueForArgument(symbol);
                handle(value!, context);
            });
        }

        internal static void SetHandler<T1, T2, T3>(this Command command, Action<T1, T2, T3, InvocationContext> handle, Argument<T1> symbol1, Option<T2> symbol2, Option<T3> symbol3)
        {
            command.SetHandler(context =>
            {
                var value1 = context.ParseResult.GetValueForArgument(symbol1);
                var value2 = context.ParseResult.GetValueForOption(symbol2);
                var value3 = context.ParseResult.GetValueForOption(symbol3);
                handle(value1!, value2!, value3!, context);
            });
        }

        internal static void SetHandler<T1, T2, T3, T4>(this Command command, Action<T1, T2, T3, T4, InvocationContext> handle, Argument<T1> symbol1, Option<T2> symbol2, Option<T3> symbol3, Option<T4> symbol4)
        {
            command.SetHandler(context =>
            {
                var value1 = context.ParseResult.GetValueForArgument(symbol1);
                var value2 = context.ParseResult.GetValueForOption(symbol2);
                var value3 = context.ParseResult.GetValueForOption(symbol3);
                var value4 = context.ParseResult.GetValueForOption(symbol4);
                handle(value1!, value2!, value3!, value4!, context);
            });
        }
    }
}
