using System;
using System.Security.AccessControl;
using static WinSW.Native.ServiceApis;

namespace WinSW.Native
{
    public enum SC_ACTION_TYPE
    {
        /// <summary>
        /// No action.
        /// </summary>
        SC_ACTION_NONE = 0,

        /// <summary>
        /// Restart the service.
        /// </summary>
        SC_ACTION_RESTART = 1,

        /// <summary>
        /// Reboot the computer.
        /// </summary>
        SC_ACTION_REBOOT = 2,

        /// <summary>
        /// Run a command.
        /// </summary>
        SC_ACTION_RUN_COMMAND = 3,
    }

    public struct SC_ACTION
    {
        /// <summary>
        /// The action to be performed.
        /// </summary>
        public SC_ACTION_TYPE Type;

        /// <summary>
        /// The time to wait before performing the specified action, in milliseconds.
        /// </summary>
        public int Delay;

        public SC_ACTION(SC_ACTION_TYPE type, TimeSpan delay)
        {
            this.Type = type;
            this.Delay = (int)delay.TotalMilliseconds;
        }
    }

    internal ref struct ServiceManager
    {
        private IntPtr handle;

        private ServiceManager(IntPtr handle) => this.handle = handle;

        internal static ServiceManager Open()
        {
            IntPtr handle = OpenSCManager(null, null, ServiceManagerAccess.ALL_ACCESS);
            if (handle == IntPtr.Zero)
            {
                Throw.Win32Exception("Failed to open the service control manager database.");
            }

            return new ServiceManager(handle);
        }

        internal Service OpenService(string serviceName)
        {
            IntPtr serviceHandle = ServiceApis.OpenService(this.handle, serviceName, ServiceAccess.ALL_ACCESS);
            if (serviceHandle == IntPtr.Zero)
            {
                Throw.Win32Exception("Failed to open the service.");
            }

            return new Service(serviceHandle);
        }

        public void Dispose()
        {
            if (this.handle != IntPtr.Zero)
            {
                _ = CloseServiceHandle(this.handle);
            }

            this.handle = IntPtr.Zero;
        }
    }

    internal ref struct Service
    {
        private IntPtr handle;

        internal Service(IntPtr handle) => this.handle = handle;

        internal void SetDescription(string description)
        {
            if (!ChangeServiceConfig2(
                this.handle,
                ServiceConfigInfoLevels.DESCRIPTION,
                new SERVICE_DESCRIPTION { Description = description }))
            {
                Throw.Win32Exception("Failed to configure the description.");
            }
        }

        internal unsafe void SetFailureActions(TimeSpan failureResetPeriod, SC_ACTION[] actions)
        {
            fixed (SC_ACTION* actionsPtr = actions)
            {
                if (!ChangeServiceConfig2(
                    this.handle,
                    ServiceConfigInfoLevels.FAILURE_ACTIONS,
                    new SERVICE_FAILURE_ACTIONS
                    {
                        ResetPeriod = (int)failureResetPeriod.TotalSeconds,
                        RebootMessage = string.Empty, // TODO
                        Command = string.Empty, // TODO
                        ActionsCount = actions.Length,
                        Actions = actionsPtr,
                    }))
                {
                    Throw.Win32Exception("Failed to configure the failure actions.");
                }
            }
        }

        internal void SetDelayedAutoStart(bool enabled)
        {
            if (!ChangeServiceConfig2(
                this.handle,
                ServiceConfigInfoLevels.DELAYED_AUTO_START_INFO,
                new SERVICE_DELAYED_AUTO_START_INFO { DelayedAutostart = enabled }))
            {
                Throw.Win32Exception("Failed to configure the delayed auto-start setting.");
            }
        }

        internal void SetSecurityDescriptor(RawSecurityDescriptor securityDescriptor)
        {
            byte[] securityDescriptorBytes = new byte[securityDescriptor.BinaryLength];
            securityDescriptor.GetBinaryForm(securityDescriptorBytes, 0);
            if (!SetServiceObjectSecurity(this.handle, SecurityInfos.DiscretionaryAcl, securityDescriptorBytes))
            {
                Throw.Win32Exception("Failed to configure the security descriptor.");
            }
        }

        public void Dispose()
        {
            if (this.handle != IntPtr.Zero)
            {
                _ = CloseServiceHandle(this.handle);
            }

            this.handle = IntPtr.Zero;
        }
    }
}
