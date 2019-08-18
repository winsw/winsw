using System;
using System.Diagnostics;
#if !FEATURE_CIM
using System.Management;
#endif
using System.Reflection;
using DynamicProxy;
#if FEATURE_CIM
using Microsoft.Management.Infrastructure;
using Microsoft.Management.Infrastructure.Generic;
#endif

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

        public WmiClassName(string name) => Name = name;
    }

    /// <summary>
    /// Marker interface to denote a collection in WMI.
    /// </summary>
    public interface IWmiCollection { }

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

    public sealed class WmiRoot
    {
#if FEATURE_CIM
        private const string CimNamespace = "root/cimv2";

        private readonly CimSession cimSession;
#else
        private readonly ManagementScope wmiScope;
#endif

        public WmiRoot(string? machineName = null)
        {
#if FEATURE_CIM
            this.cimSession = CimSession.Create(machineName);
#else
            ConnectionOptions options = new ConnectionOptions
            {
                EnablePrivileges = true,
                Impersonation = ImpersonationLevel.Impersonate,
                Authentication = AuthenticationLevel.PacketPrivacy,
            };

            string path;

            if (machineName != null)
                path = $@"\\{machineName}\root\cimv2";
            else
                path = @"\root\cimv2";
            wmiScope = new ManagementScope(path, options);
            wmiScope.Connect();
#endif
        }

        private static string Capitalize(string s)
        {
            return char.ToUpper(s[0]) + s.Substring(1);
        }

        private abstract class BaseHandler : IProxyInvocationHandler
        {
            public abstract object? Invoke(object proxy, MethodInfo method, object[] arguments);

#if FEATURE_CIM
            protected void CheckError(CimMethodResult result)
            {
                uint code = (uint)result.ReturnValue.Value;
                if (code != 0)
                    throw new WmiException((ReturnValue)code);
            }
#else
            protected void CheckError(ManagementBaseObject result)
            {
                uint code = (uint)result["returnValue"];
                if (code != 0)
                    throw new WmiException((ReturnValue)code);
            }
#endif

#if FEATURE_CIM
            protected CimMethodParametersCollection GetMethodParameters(CimClass cimClass, string methodName, ParameterInfo[] methodParameters, object[] arguments)
            {
                CimMethodParametersCollection cimParameters = new CimMethodParametersCollection();
                CimReadOnlyKeyedCollection<CimMethodParameterDeclaration> cimParameterDeclarations = cimClass.CimClassMethods[methodName].Parameters;
                for (int i = 0; i < arguments.Length; i++)
                {
                    string capitalizedName = Capitalize(methodParameters[i].Name!);
                    cimParameters.Add(CimMethodParameter.Create(capitalizedName, arguments[i], cimParameterDeclarations[capitalizedName].CimType, CimFlags.None));
                }

                return cimParameters;
            }
#else
            protected ManagementBaseObject GetMethodParameters(ManagementObject wmiObject, string methodName, ParameterInfo[] methodParameters, object[] arguments)
            {
                ManagementBaseObject wmiParameters = wmiObject.GetMethodParameters(methodName);
                for (int i = 0; i < arguments.Length; i++)
                {
                    string capitalizedName = Capitalize(methodParameters[i].Name!);
                    wmiParameters[capitalizedName] = arguments[i];
                }

                return wmiParameters;
            }
#endif
        }

        private class InstanceHandler : BaseHandler, IWmiObject
        {
#if FEATURE_CIM
            private readonly CimSession cimSession;
            private readonly CimInstance cimInstance;

            public InstanceHandler(CimSession cimSession, CimInstance cimInstance)
            {
                this.cimSession = cimSession;
                this.cimInstance = cimInstance;
            }
#else
            private readonly ManagementObject wmiObject;

            public InstanceHandler(ManagementObject wmiObject) => this.wmiObject = wmiObject;
#endif

            public override object? Invoke(object proxy, MethodInfo method, object[] arguments)
            {
                if (method.DeclaringType == typeof(IWmiObject))
                {
                    return method.Invoke(this, arguments);
                }

                // TODO: proper property support
                if (method.Name.StartsWith("set_"))
                {
#if FEATURE_CIM
                    CimProperty cimProperty = this.cimInstance.CimInstanceProperties[method.Name.Substring(4)];
                    Debug.Assert((cimProperty.Flags & CimFlags.ReadOnly) == CimFlags.None);
                    cimProperty.Value = arguments[0];
#else
                    this.wmiObject[method.Name.Substring(4)] = arguments[0];
#endif
                    return null;
                }

                if (method.Name.StartsWith("get_"))
                {
#if FEATURE_CIM
                    return this.cimInstance.CimInstanceProperties[method.Name.Substring(4)].Value;
#else
                    return this.wmiObject[method.Name.Substring(4)];
#endif
                }

                string methodName = method.Name;
#if FEATURE_CIM
                using CimMethodParametersCollection? cimParameters = arguments.Length == 0 ? null :
                    this.GetMethodParameters(this.cimInstance.CimClass, methodName, method.GetParameters(), arguments);
                using CimMethodResult result = this.cimSession.InvokeMethod(CimNamespace, this.cimInstance, methodName, cimParameters);
                this.CheckError(result);
#else
                using ManagementBaseObject? wmiParameters = arguments.Length == 0 ? null :
                    this.GetMethodParameters(this.wmiObject, methodName, method.GetParameters(), arguments);
                using ManagementBaseObject result = this.wmiObject.InvokeMethod(methodName, wmiParameters, null);
                this.CheckError(result);
#endif
                return null;
            }

            public void Commit()
            {
#if !FEATURE_CIM
                this.wmiObject.Put();
#endif
            }
        }

        private class ClassHandler : BaseHandler
        {
#if FEATURE_CIM
            private readonly CimSession cimSession;
            private readonly CimClass cimClass;
#else
            private readonly ManagementClass wmiClass;
#endif
            private readonly string className;

#if FEATURE_CIM
            public ClassHandler(CimSession cimSession, string className)
            {
                this.cimSession = cimSession;
                this.cimClass = cimSession.GetClass(CimNamespace, className);
                this.className = className;
            }
#else
            public ClassHandler(ManagementScope wmiScope, string className)
            {
                this.wmiClass = new ManagementClass(wmiScope, new ManagementPath(className), null);
                this.className = className;
            }
#endif

            public override object? Invoke(object proxy, MethodInfo method, object[] arguments)
            {
                ParameterInfo[] methodParameters = method.GetParameters();

                if (method.Name == nameof(Win32Services.Select))
                {
                    // select method to find instances
                    string query = "SELECT * FROM " + this.className + " WHERE ";
                    for (int i = 0; i < arguments.Length; i++)
                    {
                        if (i != 0)
                            query += " AND ";

                        query += ' ' + Capitalize(methodParameters[i].Name!) + " = '" + arguments[i] + "'";
                    }

#if FEATURE_CIM
                    // TODO: support collections
                    foreach (CimInstance cimInstance in this.cimSession.QueryInstances(CimNamespace, "WQL", query))
                    {
                        return ProxyFactory.GetInstance().Create(new InstanceHandler(this.cimSession, cimInstance), method.ReturnType, true);
                    }
#else
                    using ManagementObjectSearcher searcher = new ManagementObjectSearcher(this.wmiClass.Scope, new ObjectQuery(query));
                    using ManagementObjectCollection results = searcher.Get();
                    // TODO: support collections
                    foreach (ManagementObject wmiObject in results)
                    {
                        return ProxyFactory.GetInstance().Create(new InstanceHandler(wmiObject), method.ReturnType, true);
                    }
#endif

                    return null;
                }

                string methodName = method.Name;
#if FEATURE_CIM
                using CimMethodParametersCollection? cimParameters = arguments.Length == 0 ? null :
                    this.GetMethodParameters(this.cimClass, methodName, methodParameters, arguments);
                using CimMethodResult result = this.cimSession.InvokeMethod(CimNamespace, this.className, methodName, cimParameters);
                this.CheckError(result);
#else
                using ManagementBaseObject? wmiParameters = arguments.Length == 0 ? null :
                    this.GetMethodParameters(this.wmiClass, methodName, methodParameters, arguments);
                using ManagementBaseObject result = this.wmiClass.InvokeMethod(methodName, wmiParameters, null);
                this.CheckError(result);
#endif
                return null;
            }
        }

        /// <summary>
        /// Obtains an object that corresponds to a table in WMI, which is a collection of a managed object.
        /// </summary>
        public T GetCollection<T>() where T : IWmiCollection
        {
            WmiClassName className = (WmiClassName)typeof(T).GetCustomAttributes(typeof(WmiClassName), false)[0];

            return (T)ProxyFactory.GetInstance().Create(
#if FEATURE_CIM
                new ClassHandler(this.cimSession, className.Name),
#else
                new ClassHandler(this.wmiScope, className.Name),
#endif
                typeof(T),
                true);
        }
    }
}
