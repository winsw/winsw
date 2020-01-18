using System;
using NUnit.Framework;

namespace winswTests.Util
{
    class ExceptionHelper
    {
        public static void assertFails(string expectedMessagePart, Type expectedExceptionType, ExceptionHelperExecutionBody body)
        {
            try
            {
                body();
            }
            catch (Exception ex)
            {
                Console.Out.WriteLine("Caught exception: " + ex);
                Assert.That(ex, Is.InstanceOf(expectedExceptionType ?? typeof(Exception)), "Wrong exception type");
                if (expectedMessagePart != null)
                {
                    Assert.That(ex.Message, Is.StringContaining(expectedMessagePart), "Wrong error message");
                }

                // Else the exception is fine
                return;
            }

            Assert.Fail("Expected exception " + expectedExceptionType + " to be thrown by the operation");
        }
    }

    public delegate void ExceptionHelperExecutionBody();
}
