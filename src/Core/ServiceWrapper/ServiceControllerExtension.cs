using System;
using System.ServiceProcess;
using TimeoutException = System.ServiceProcess.TimeoutException;

namespace WinSW
{
    internal static class ServiceControllerExtension
    {
        internal static bool TryWaitForStatus(/*this*/ ServiceController serviceController, ServiceControllerStatus desiredStatus, TimeSpan timeout)
        {
            try
            {
                serviceController.WaitForStatus(desiredStatus, timeout);
                return true;
            }
            catch (TimeoutException)
            {
                return false;
            }
        }
    }
}
