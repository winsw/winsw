using System;
using NUnit.Framework;

namespace winswTests.Util
{
    class ExceptionHelper
    {
        public static void AssertFails(string expectedMessagePart, Type expectedExceptionType, TestDelegate body)
        {
            Exception exception = Assert.Throws(expectedExceptionType ?? typeof(Exception), body);
            StringAssert.Contains(expectedMessagePart, exception.Message);
        }
    }
}
