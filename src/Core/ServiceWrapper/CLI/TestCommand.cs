﻿using CommandLine;
using System.Threading;
using WMI;

namespace winsw.CLI
{
    [Verb("test", HelpText = "check if the service can be started and then stopped")]
    public class TestCommand : CLICommand
    {
        public override void Run(ServiceDescriptor descriptor, Win32Services svcs, Win32Service? svc)
        {
            if (!Program.elevated)
            {
                Elevate();
                return;
            }

            WrapperService wsvc = new WrapperService(descriptor);
            wsvc.RaiseOnStart(new string[0]);
            Thread.Sleep(1000);
            wsvc.RaiseOnStop();
        }
    }
}