using System;
using System.Runtime.InteropServices;
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

        /// <exception cref="CommandException" />
        internal static ServiceManager Open(ServiceManagerAccess access = ServiceManagerAccess.All)
        {
            var handle = OpenSCManager(null, null, access);
            if (handle == IntPtr.Zero)
            {
                Throw.Command.Win32Exception("Failed to open the service control manager database.");
            }

            return new ServiceManager(handle);
        }

        /// <exception cref="CommandException" />
        internal Service CreateService(
            string serviceName,
            string displayName,
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
                ServiceType.Win32OwnProcess,
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

        /// <exception cref="CommandException" />
        internal ServiceEnumerator EnumerateServices()
        {
            int resume = 0;
            _ = EnumServicesStatus(
                this.handle,
                ServiceType.Win32OwnProcess,
                ServiceState.All,
                IntPtr.Zero,
                0,
                out int bytesNeeded,
                out _,
                ref resume);

            var services = Marshal.AllocHGlobal(bytesNeeded);
            try
            {
                if (!EnumServicesStatus(
                    this.handle,
                    ServiceType.Win32OwnProcess,
                    ServiceState.All,
                    services,
                    bytesNeeded,
                    out _,
                    out int count,
                    ref resume))
                {
                    Throw.Command.Win32Exception("Failed to enumerate services.");
                }

                return new(services, count);
            }
            catch
            {
                Marshal.FreeHGlobal(services);
                throw;
            }
        }

        /// <exception cref="CommandException" />
        internal unsafe Service OpenService(char* serviceName, ServiceAccess access = ServiceAccess.All)
        {
            var serviceHandle = ServiceApis.OpenService(this.handle, serviceName, access);
            if (serviceHandle == IntPtr.Zero)
            {
                Throw.Command.Win32Exception("Failed to open the service.");
            }

            return new Service(serviceHandle);
        }

        /// <exception cref="CommandException" />
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
            var serviceHandle = ServiceApis.OpenService(this.handle, serviceName, ServiceAccess.QueryStatus);
            if (serviceHandle == IntPtr.Zero)
            {
                return false;
            }

            _ = CloseServiceHandle(serviceHandle);
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

        internal ref struct ServiceEnumerator
        {
            private readonly IntPtr services;
            private readonly int count;

            private int index;

            internal ServiceEnumerator(IntPtr services, int count)
            {
                this.services = services;
                this.count = count;
                this.index = -1;
            }

            public unsafe ENUM_SERVICE_STATUS* Current => (ENUM_SERVICE_STATUS*)this.services + this.index;

            public void Dispose() => Marshal.FreeHGlobal(this.services);

            public ServiceEnumerator GetEnumerator() => this;

            public bool MoveNext() => ++this.index < this.count;

            public void Reset() => throw new NotImplementedException();
        }
    }

    internal ref struct Service
    {
        private IntPtr handle;

        internal Service(IntPtr handle) => this.handle = handle;

        /// <exception cref="CommandException" />
        internal unsafe string ExecutablePath
        {
            get
            {
                _ = QueryServiceConfig(
                    this.handle,
                    IntPtr.Zero,
                    0,
                    out int bytesNeeded);

                var config = Marshal.AllocHGlobal(bytesNeeded);
                try
                {
                    if (!QueryServiceConfig(
                        this.handle,
                        config,
                        bytesNeeded,
                        out _))
                    {
                        Throw.Command.Win32Exception("Failed to query service config.");
                    }

                    return Marshal.PtrToStringUni((IntPtr)((QUERY_SERVICE_CONFIG*)config)->BinaryPathName)!;
                }
                finally
                {
                    Marshal.FreeHGlobal(config);
                }
            }
        }

        /// <exception cref="CommandException" />
        internal unsafe int ProcessId
        {
            get
            {
                if (!QueryServiceStatusEx(
                    this.handle,
                    ServiceStatusType.ProcessInfo,
                    out var status,
                    sizeof(SERVICE_STATUS_PROCESS),
                    out _))
                {
                    Throw.Command.Win32Exception("Failed to query service status.");
                }

                return status.CurrentState == ServiceControllerStatus.Running ? status.ProcessId : -1;
            }
        }

        /// <exception cref="CommandException" />
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

        /// <exception cref="CommandException" />
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

        /// <exception cref="CommandException" />
        internal void ChangeConfig(
            string displayName,
            ServiceStartMode startMode,
            string[] dependencies)
        {
            unchecked
            {
                if (!ChangeServiceConfig(
                    this.handle,
                    (ServiceType)SERVICE_NO_CHANGE,
                    startMode,
                    (ServiceErrorControl)SERVICE_NO_CHANGE,
                    null,
                    null,
                    IntPtr.Zero,
                    GetNativeDependencies(dependencies),
                    null,
                    null,
                    displayName))
                {
                    Throw.Command.Win32Exception("Failed to change service config.");
                }
            }
        }

        /// <exception cref="CommandException" />
        internal void Delete()
        {
            if (!DeleteService(this.handle))
            {
                Throw.Command.Win32Exception("Failed to delete service.");
            }
        }

        /// <exception cref="CommandException" />
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

        /// <exception cref="CommandException" />
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

        /// <exception cref="CommandException" />
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

        /// <exception cref="CommandException" />
        internal void SetPreshutdownTimeout(TimeSpan timeout)
        {
            if (!ChangeServiceConfig2(
                this.handle,
                ServiceConfigInfoLevels.PRESHUTDOWN_INFO,
                new SERVICE_PRESHUTDOWN_INFO { PreshutdownTimeout = (int)timeout.TotalMilliseconds }))
            {
                Throw.Command.Win32Exception("Failed to configure the preshutdown timeout.");
            }
        }

        /// <exception cref="CommandException" />
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
