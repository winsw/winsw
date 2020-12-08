using System;
using System.Management;
using System.Reflection;
using System.Text;
using DynamicProxy;

namespace WMI
{
    // https://docs.microsoft.com/windows/win32/cimwin32prov/create-method-in-class-win32-service
    public enum ReturnValue : uint
    {
        Success = 0,
        NotSupported = 1,
        AccessDenied = 2,
        DependentServicesRunning = 3,
        InvalidServiceControl = 4,
        ServiceCannotAcceptControl = 5,
        ServiceNotActive = 6,
        ServiceRequestTimeout = 7,
        UnknownFailure = 8,
        PathNotFound = 9,
        ServiceAlreadyRunning = 10,
        ServiceDatabaseLocked = 11,
        ServiceDependencyDeleted = 12,
        ServiceDependencyFailure = 13,
        ServiceDisabled = 14,
        ServiceLogonFailure = 15,
        ServiceMarkedForDeletion = 16,
        ServiceNoThread = 17,
        StatusCircularDependency = 18,
        StatusDuplicateName = 19,
        StatusInvalidName = 20,
        StatusInvalidParameter = 21,
        StatusInvalidServiceAccount = 22,
        StatusServiceExists = 23,
        ServiceAlreadyPaused = 24,

        NoSuchService = 200
    }

    /// <summary>
    /// Signals a problem in WMI related operations
    /// </summary>
    public class WmiException : Exception
    {
        public readonly ReturnValue ErrorCode;

        public WmiException(string message, ReturnValue code)
            : base(message)
        {
            this.ErrorCode = code;
        }

        public WmiException(ReturnValue code)
            : this(code.ToString(), code)
        {
        }
    }

    /// <summary>
    /// Associated a WMI class name to the proxy interface (which should extend from IWmiCollection)
    /// </summary>
    public class WmiClassName : Attribute
    {
        public readonly string Name;

        public WmiClassName(string name) => this.Name = name;
    }

    /// <summary>
    /// Marker interface to denote a collection in WMI.
    /// </summary>
    public interface IWmiCollection
    {
    }

    /// <summary>
    /// Marker interface to denote an individual managed object
    /// </summary>
    public interface IWmiObject
    {
    }

    public sealed class WmiRoot
    {
        private readonly ManagementScope wmiScope;

        public WmiRoot()
        {
            var options = new ConnectionOptions
            {
                EnablePrivileges = true,
                Impersonation = ImpersonationLevel.Impersonate,
                Authentication = AuthenticationLevel.PacketPrivacy,
            };

            this.wmiScope = new ManagementScope(@"\\.\root\cimv2", options);
            this.wmiScope.Connect();
        }

        private static string Capitalize(string s)
        {
            return char.ToUpper(s[0]) + s.Substring(1);
        }

        private abstract class BaseHandler : IProxyInvocationHandler
        {
            public abstract object? Invoke(object proxy, MethodInfo method, object[] arguments);

            protected void CheckError(ManagementBaseObject result)
            {
                uint code = (uint)result["returnValue"];
                if (code != 0)
                {
                    throw new WmiException((ReturnValue)code);
                }
            }

            protected ManagementBaseObject GetMethodParameters(ManagementObject wmiObject, string methodName, ParameterInfo[] methodParameters, object[] arguments)
            {
                var wmiParameters = wmiObject.GetMethodParameters(methodName);
                for (int i = 0; i < arguments.Length; i++)
                {
                    string capitalizedName = Capitalize(methodParameters[i].Name!);
                    wmiParameters[capitalizedName] = arguments[i];
                }

                return wmiParameters;
            }
        }

        private class InstanceHandler : BaseHandler, IWmiObject
        {
            private readonly ManagementObject wmiObject;

            public InstanceHandler(ManagementObject wmiObject) => this.wmiObject = wmiObject;

            public override object? Invoke(object proxy, MethodInfo method, object[] arguments)
            {
                if (method.DeclaringType == typeof(IWmiObject))
                {
                    return method.Invoke(this, arguments);
                }

                // TODO: proper property support
                if (method.Name.StartsWith("set_"))
                {
                    this.wmiObject[method.Name.Substring(4)] = arguments[0];
                    return null;
                }

                if (method.Name.StartsWith("get_"))
                {
                    return this.wmiObject[method.Name.Substring(4)];
                }

                string methodName = method.Name;
                using var wmiParameters = arguments.Length == 0 ? null :
                    this.GetMethodParameters(this.wmiObject, methodName, method.GetParameters(), arguments);
                using var result = this.wmiObject.InvokeMethod(methodName, wmiParameters, null);
                this.CheckError(result);
                return null;
            }
        }

        private class ClassHandler : BaseHandler
        {
            private readonly ManagementClass wmiClass;
            private readonly string className;

            public ClassHandler(ManagementScope wmiScope, string className)
            {
                this.wmiClass = new ManagementClass(wmiScope, new ManagementPath(className), null);
                this.className = className;
            }

            public override object? Invoke(object proxy, MethodInfo method, object[] arguments)
            {
                var methodParameters = method.GetParameters();

                if (method.Name == nameof(IWin32Services.Select))
                {
                    // select method to find instances
                    var query = new StringBuilder("SELECT * FROM ").Append(this.className).Append(" WHERE ");
                    for (int i = 0; i < arguments.Length; i++)
                    {
                        if (i != 0)
                        {
                            query.Append(" AND ");
                        }

                        query.Append(' ').Append(Capitalize(methodParameters[i].Name!)).Append(" = '").Append(arguments[i]).Append('\'');
                    }

                    using var searcher = new ManagementObjectSearcher(this.wmiClass.Scope, new ObjectQuery(query.ToString()));
                    using var results = searcher.Get();

                    // TODO: support collections
                    foreach (ManagementObject wmiObject in results)
                    {
                        return ProxyFactory.Create(new InstanceHandler(wmiObject), method.ReturnType, true);
                    }

                    return null;
                }

                string methodName = method.Name;
                using var wmiParameters = arguments.Length == 0 ? null :
                    this.GetMethodParameters(this.wmiClass, methodName, methodParameters, arguments);
                using var result = this.wmiClass.InvokeMethod(methodName, wmiParameters, null);
                this.CheckError(result);
                return null;
            }
        }

        /// <summary>
        /// Obtains an object that corresponds to a table in WMI, which is a collection of a managed object.
        /// </summary>
        public T GetCollection<T>()
            where T : IWmiCollection
        {
            var className = (WmiClassName)typeof(T).GetCustomAttributes(typeof(WmiClassName), false)[0];

            return (T)ProxyFactory.Create(new ClassHandler(this.wmiScope, className.Name), typeof(T), true);
        }
    }
}
