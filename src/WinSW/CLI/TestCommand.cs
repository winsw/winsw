using CommandLine;
using System;
using System.Threading;

namespace WinSW.CLI
{
    [Verb("test", HelpText = "check if the service can be started and then stopped")]
    public class TestCommand : CliCommand
    {
        [Option("wait", HelpText = "Test Wait", Default = false)]
        public bool Wait { get; set; }

        public override void Run(ServiceDescriptor descriptor)
        {
            if (!Program.elevated)
            {
                Elevate();
                return;
            }

            if (this.Wait)
            {
                this.TestWait(descriptor);
            }
            else
            {
                this.Test(descriptor);
            }
        }

        private void Test(ServiceDescriptor descriptor)
        {
            WrapperService wsvc = new WrapperService(descriptor);
            wsvc.RaiseOnStart(new string[0]);
            Thread.Sleep(1000);
            wsvc.RaiseOnStop();
        }

        private void TestWait(ServiceDescriptor descriptor)
        {
            WrapperService wsvc = new WrapperService(descriptor);
            wsvc.RaiseOnStart(new string[0]);
            Console.WriteLine("Press any key to stop the service...");
            _ = Console.Read();
            wsvc.RaiseOnStop();
        }
    }
}
