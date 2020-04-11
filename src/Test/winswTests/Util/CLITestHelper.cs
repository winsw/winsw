﻿using System;
using System.IO;
using NUnit.Framework;
using winsw;

namespace winswTests.Util
{
    /// <summary>
    /// Helper for WinSW CLI testing
    /// </summary>
    public static class CLITestHelper
    {
        public const string Id = "WinSW.Tests";
        public const string Name = "WinSW Test Service";

        private static readonly string SeedXml =
$@"<service>
  <id>{Id}</id>
  <name>{Name}</name>
  <description>The service.</description>
  <executable>node.exe</executable>
  <arguments>My Arguments</arguments>
  <logmode>rotate</logmode>
  <workingdirectory>C:\winsw\workdir</workingdirectory>
  <logpath>C:\winsw\logs</logpath>
</service>";

        public static readonly ServiceDescriptor DefaultServiceDescriptor = ServiceDescriptor.FromXML(SeedXml);

        /// <summary>
        /// Runs a simle test, which returns the output CLI
        /// </summary>
        /// <param name="arguments">CLI arguments to be passed</param>
        /// <param name="descriptor">Optional Service descriptor (will be used for initializationpurposes)</param>
        /// <returns>STDOUT if there's no exceptions</returns>
        /// <exception cref="Exception">Command failure</exception>
        public static string CLITest(string[] arguments, ServiceDescriptor descriptor = null)
        {
            TextWriter tmpOut = Console.Out;
            TextWriter tmpErr = Console.Error;

            using StringWriter swOut = new StringWriter();
            using StringWriter swErr = new StringWriter();

            Console.SetOut(swOut);
            Console.SetError(swErr);
            try
            {
                WrapperService.Run(arguments, descriptor ?? DefaultServiceDescriptor);
            }
            finally
            {
                Console.SetOut(tmpOut);
                Console.SetError(tmpErr);
            }

            Assert.That(swErr.GetStringBuilder().Length, Is.Zero);
            Console.Write(swOut.ToString());
            return swOut.ToString();
        }

        /// <summary>
        /// Runs a simle test, which returns the output CLI
        /// </summary>
        /// <param name="arguments">CLI arguments to be passed</param>
        /// <param name="descriptor">Optional Service descriptor (will be used for initializationpurposes)</param>
        /// <returns>Test results</returns>
        public static CLITestResult CLIErrorTest(string[] arguments, ServiceDescriptor descriptor = null)
        {
            Exception testEx = null;
            TextWriter tmpOut = Console.Out;
            TextWriter tmpErr = Console.Error;

            using StringWriter swOut = new StringWriter();
            using StringWriter swErr = new StringWriter();

            Console.SetOut(swOut);
            Console.SetError(swErr);
            try
            {
                WrapperService.Run(arguments, descriptor ?? DefaultServiceDescriptor);
            }
            catch (Exception ex)
            {
                testEx = ex;
            }
            finally
            {
                Console.SetOut(tmpOut);
                Console.SetError(tmpErr);
            }

            Console.WriteLine("\n>>> Output: ");
            Console.Write(swOut.ToString());
            Console.WriteLine("\n>>> Error: ");
            Console.Write(swErr.ToString());
            if (testEx != null)
            {
                Console.WriteLine("\n>>> Exception: ");
                Console.WriteLine(testEx);
            }

            return new CLITestResult(swOut.ToString(), swErr.ToString(), testEx);
        }
    }

    /// <summary>
    /// Aggregated test report
    /// </summary>
    public class CLITestResult
    {
        public string Out { get; }

        public string Error { get; }

        public Exception Exception { get; }

        public bool HasException => Exception != null;

        public CLITestResult(string output, string error, Exception exception = null)
        {
            Out = output;
            Error = error;
            Exception = exception;
        }
    }
}
