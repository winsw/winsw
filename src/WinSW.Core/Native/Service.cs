using System;
using System.Security.AccessControl;
using System.ServiceProcess;
using System.Text;
using static WinSW.Native.ServiceApis;

namespace WinSW.Native
{
    public enum SC_ACTION_TYPE
    {
        /// <summary>
        /// No action.
        /// </summary>
        NONE = 0,

        /// <summary>
        /// Restart the service.
        /// </summary>
        RESTART = 1,

        /// <summary>
        /// Reboot the computer.
        /// </summary>
        REBOOT = 2,

        /// <summary>
        /// Run a command.
        /// </summary>
        RUN_COMMAND = 3,
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

        internal static ServiceManager Open(ServiceManagerAccess access = ServiceManagerAccess.All)
        {
            var handle = OpenSCManager(null, null, access);
            if (handle == IntPtr.Zero)
            {
                Throw.Command.Win32Exception("Failed to open the service control manager database.");
            }

            return new ServiceManager(handle);
        }

        internal Service CreateService(
            string serviceName,
            string displayName,
            bool interactive,
            ServiceStartMode startMode,
            string executablePath,
            string[] dependencies,
            string? username,
            string? password)
        {
            var handle = ServiceApis.CreateService(
                this.handle,
                serviceName,
                displayName,
                ServiceAccess.All,
                ServiceType.Win32OwnProcess | (interactive ? ServiceType.InteractiveProcess : default),
                startMode,
                ServiceErrorControl.Normal,
                executablePath,
                default,
                default,
                Service.GetNativeDependencies(dependencies),
                username,
                password);
            if (handle == IntPtr.Zero)
            {
                Throw.Command.Win32Exception("Failed to create service.");
            }

            return new Service(handle);
        }

        internal Service OpenService(string serviceName, ServiceAccess access = ServiceAccess.All)
        {
            var serviceHandle = ServiceApis.OpenService(this.handle, serviceName, access);
            if (serviceHandle == IntPtr.Zero)
            {
                Throw.Command.Win32Exception("Failed to open the service.");
            }

            return new Service(serviceHandle);
        }

        internal bool ServiceExists(string serviceName)
        {
            var serviceHandle = ServiceApis.OpenService(this.handle, serviceName, ServiceAccess.All);
            if (serviceHandle == IntPtr.Zero)
            {
                return false;
            }

            _ = CloseServiceHandle(this.handle);
            return true;
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

        internal ServiceControllerStatus Status
        {
            get
            {
                if (!QueryServiceStatus(this.handle, out var status))
                {
                    Throw.Command.Win32Exception("Failed to query service status.");
                }

                return status.CurrentState;
            }
        }

        internal static StringBuilder? GetNativeDependencies(string[] dependencies)
        {
            int arrayLength = 1;
            for (int i = 0; i < dependencies.Length; i++)
            {
                arrayLength += dependencies[i].Length + 1;
            }

            StringBuilder? array = null;
            if (dependencies.Length != 0)
            {
                array = new StringBuilder(arrayLength);
                for (int i = 0; i < dependencies.Length; i++)
                {
                    _ = array.Append(dependencies[i]).Append('\0');
                }

                _ = array.Append('\0');
            }

            return array;
        }

        internal void SetStatus(IntPtr statusHandle, ServiceControllerStatus state)
        {
            if (!QueryServiceStatus(this.handle, out var status))
            {
                Throw.Command.Win32Exception("Failed to query service status.");
            }

            status.CheckPoint = 0;
            status.WaitHint = 0;
            status.CurrentState = state;

            if (!SetServiceStatus(statusHandle, status))
            {
                Throw.Command.Win32Exception("Failed to set service status.");
            }
        }

        internal void Delete()
        {
            if (!DeleteService(this.handle))
            {
                Throw.Command.Win32Exception("Failed to delete service.");
            }
        }

        internal void SetDescription(string description)
        {
            if (!ChangeServiceConfig2(
                this.handle,
                ServiceConfigInfoLevels.DESCRIPTION,
                new SERVICE_DESCRIPTION { Description = description }))
            {
                Throw.Command.Win32Exception("Failed to configure the description.");
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
                    Throw.Command.Win32Exception("Failed to configure the failure actions.");
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
                Throw.Command.Win32Exception("Failed to configure the delayed auto-start setting.");
            }
        }

        internal void SetSecurityDescriptor(RawSecurityDescriptor securityDescriptor)
        {
            byte[] securityDescriptorBytes = new byte[securityDescriptor.BinaryLength];
            securityDescriptor.GetBinaryForm(securityDescriptorBytes, 0);
            if (!SetServiceObjectSecurity(this.handle, SecurityInfos.DiscretionaryAcl, securityDescriptorBytes))
            {
                Throw.Command.Win32Exception("Failed to configure the security descriptor.");
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
