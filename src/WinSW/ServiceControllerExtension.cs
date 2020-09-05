using System;
using System.ServiceProcess;
using System.Threading;
using TimeoutException = System.ServiceProcess.TimeoutException;

namespace WinSW
{
    internal static class ServiceControllerExtension
    {
        /// <exception cref="OperationCanceledException" />
        /// <exception cref="TimeoutException" />
        internal static void WaitForStatus(this ServiceController serviceController, ServiceControllerStatus desiredStatus, ServiceControllerStatus pendingStatus, CancellationToken ct)
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
                    ct.ThrowIfCancellationRequested();
                }
            }
        }

        internal static bool HasAnyStartedDependentService(this ServiceController serviceController)
        {
            return Array.Exists(serviceController.DependentServices, service => service.Status != ServiceControllerStatus.Stopped);
        }
    }
}
