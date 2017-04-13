using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;

namespace winswTests.Util
{
    class ExceptionHelper
    {
        public static void assertFails(String expectedMessagePart, Type expectedExceptionType, ExceptionHelperExecutionBody body)
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
