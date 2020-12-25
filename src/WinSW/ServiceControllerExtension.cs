using System;
using System.ServiceProcess;
using TimeoutException = System.ServiceProcess.TimeoutException;

namespace WinSW
{
    internal static class ServiceControllerExtension
    {
        /// <exception cref="TimeoutException" />
        internal static void WaitForStatus(ServiceController serviceController, ServiceControllerStatus desiredStatus, ServiceControllerStatus pendingStatus)
        {
            var timeout = new TimeSpan(TimeSpan.TicksPerSecond);
            for (; ; )
            {
                try
                {
                    serviceController.WaitForStatus(desiredStatus, timeout);
                    break;
                }
                catch (TimeoutException)
                when (serviceController.Status == desiredStatus || serviceController.Status == pendingStatus)
                {
                }
            }
        }

        internal static bool HasAnyStartedDependentService(ServiceController serviceController)
        {
            return Array.Exists(serviceController.DependentServices, service => service.Status != ServiceControllerStatus.Stopped);
        }
    }
}
