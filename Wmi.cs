using System;
using System.Reflection;
using System.Management;

namespace winsw
{
    //Reference: http://msdn2.microsoft.com/en-us/library/aa389390(VS.85).aspx

    public enum ReturnValue
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

        public WmiException(string msg, ReturnValue code)
            : base(msg)
        {
            ErrorCode = code;
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
        public WmiClassName(string name) { Name = name; }
    }

    /// <summary>
    /// Marker interface to denote a collection in WMI.
    /// </summary>
    public interface IWmiCollection {}

    /// <summary>
    /// Marker interface to denote an individual managed object
    /// </summary>
    public interface IWmiObject
    {
        /// <summary>
        /// Reflect updates made to this object to the WMI provider.
        /// </summary>
        void Commit();
    }

    public class WmiRoot
    {
        private readonly ManagementScope scope;

        public WmiRoot() : this(null) { }

        public WmiRoot(string machineName)
        {
            var options = new ConnectionOptions
                {
                    EnablePrivileges = true,
                    Impersonation = ImpersonationLevel.Impersonate,
                    Authentication = AuthenticationLevel.PacketPrivacy
                };

            var path = machineName != null ? String.Format(@"\\{0}\root\cimv2", machineName) : @"\root\cimv2";
            scope = new ManagementScope(path, options);
            scope.Connect();
        }

        private static string Capitalize(string s)
        {
            return char.ToUpper(s[0]) + s.Substring(1);
        }

        abstract class BaseHandler : IProxyInvocationHandler
        {
            public abstract object Invoke(object proxy, MethodInfo method, object[] args);

            protected static void CheckError(ManagementBaseObject result)
            {
                var code = Convert.ToInt32(result["returnValue"]);
                if (code != 0)
                    throw new WmiException((ReturnValue)code);
            }
        }

        class InstanceHandler : BaseHandler, IWmiObject
        {
            private readonly ManagementObject mo;

            public InstanceHandler(ManagementObject o) { mo = o; }

            public override object Invoke(object proxy, MethodInfo method, object[] args)
            {
                if (method.DeclaringType == typeof(IWmiObject))
                {
                    return method.Invoke(this, args);
                }

                // TODO: proper property support
                if (method.Name.StartsWith("set_"))
                {
                    mo[method.Name.Substring(4)] = args[0];
                    return null;
                }
                if (method.Name.StartsWith("get_"))
                {
                    return mo[method.Name.Substring(4)];
                }

                // method invocations
                var methodArgs = method.GetParameters();

                var wmiArgs = mo.GetMethodParameters(method.Name);
                for (var i = 0; i < args.Length; i++)
                    wmiArgs[Capitalize(methodArgs[i].Name)] = args[i];

                CheckError(mo.InvokeMethod(method.Name, wmiArgs, null));
                return null;
            }

            public void Commit()
            {
                mo.Put();
            }
        }

        class ClassHandler : BaseHandler
        {
            private readonly ManagementClass mc;
            private readonly string wmiClass;

            public ClassHandler(ManagementClass mc, string wmiClass) { this.mc = mc; this.wmiClass = wmiClass; }

            public override object Invoke(object proxy, MethodInfo method, object[] args)
            {
                var methodArgs = method.GetParameters();

                if (method.Name.StartsWith("Select"))
                {
                    // select method to find instances
                    var query = "SELECT * FROM " + wmiClass + " WHERE ";
                    for (var i = 0; i < args.Length; i++)
                    {
                        if (i != 0) query += " AND ";
                        query += ' ' + Capitalize(methodArgs[i].Name) + " = '" + args[i] + "'";
                    }

                    var searcher = new ManagementObjectSearcher(mc.Scope, new ObjectQuery(query));
                    var results = searcher.Get();
                    // TODO: support collections
                    foreach (ManagementObject manObject in results)
                        return ProxyFactory.GetInstance().Create(new InstanceHandler(manObject), method.ReturnType, true);
                    return null;
                }

                var wmiArgs = mc.GetMethodParameters(method.Name);
                for (int i = 0; i < args.Length; i++)
                    wmiArgs[Capitalize(methodArgs[i].Name)] = args[i];

                CheckError(mc.InvokeMethod(method.Name, wmiArgs, null));
                return null;
            }
        }

        /// <summary>
        /// Obtains an object that corresponds to a table in WMI, which is a collection of a managed object.
        /// </summary>
        public T GetCollection<T>() where T : IWmiCollection
        {
            var cn = (WmiClassName)typeof(T).GetCustomAttributes(typeof(WmiClassName), false)[0];

            var getOptions = new ObjectGetOptions();
            var path = new ManagementPath(cn.Name);
            var manClass = new ManagementClass(scope, path, getOptions);
            return (T)ProxyFactory.GetInstance().Create(new ClassHandler(manClass, cn.Name), typeof(T), true);
        }
    }
}
