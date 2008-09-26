using System;
using System.Reflection;
using System.Collections;
using System.Reflection.Emit;
using System.Threading;

namespace DynamicProxy
{
    /// <summary>
    /// Interface that a user defined proxy handler needs to implement.  This interface 
    /// defines one method that gets invoked by the generated proxy.  
    /// </summary>
    public interface IProxyInvocationHandler
    {
        /// <param name="proxy">The instance of the proxy</param>
        /// <param name="method">The method info that can be used to invoke the actual method on the object implementation</param>
        /// <param name="parameters">Parameters to pass to the method</param>
        /// <returns>Object</returns>
        object Invoke(object proxy, MethodInfo method, object[] parameters);
    }

    /// <summary>
    /// Factory class used to cache Types instances
    /// </summary>
    public class MetaDataFactory
    {
        private static Hashtable typeMap = new Hashtable();

        /// <summary>
        /// Class constructor.  Private because this is a static class.
        /// </summary>
        private MetaDataFactory()
        {
        }

        ///<summary>
        /// Method to add a new Type to the cache, using the type's fully qualified
        /// name as the key
        ///</summary>
        ///<param name="interfaceType">Type to cache</param>
        public static void Add(Type interfaceType)
        {
            if (interfaceType != null)
            {
                lock (typeMap.SyncRoot)
                {
                    if (!typeMap.ContainsKey(interfaceType.FullName))
                    {
                        typeMap.Add(interfaceType.FullName, interfaceType);
                    }
                }
            }
        }

        ///<summary>
        /// Method to return the method of a given type at a specified index.
        ///</summary>
        ///<param name="name">Fully qualified name of the method to return</param>
        ///<param name="i">Index to use to return MethodInfo</param>
        ///<returns>MethodInfo</returns>
        public static MethodInfo GetMethod(string name, int i)
        {
            Type type = null;
            lock (typeMap.SyncRoot)
            {
                type = (Type)typeMap[name];
            }

            return type.GetMethods()[i];
        }

        public static PropertyInfo GetProperty(string name, int i)
        {
            Type type = null;
            lock (typeMap.SyncRoot)
            {
                type = (Type)typeMap[name];
            }

            return type.GetProperties()[i];
        }
    }

    /// <summary>
    /// </summary>
    public class ProxyFactory
    {
        private static ProxyFactory instance;
        private static Object lockObj = new Object();

        private Hashtable typeMap = Hashtable.Synchronized(new Hashtable());
        private static readonly Hashtable opCodeTypeMapper = new Hashtable();

        private const string PROXY_SUFFIX = "Proxy";
        private const string ASSEMBLY_NAME = "ProxyAssembly";
        private const string MODULE_NAME = "ProxyModule";
        private const string HANDLER_NAME = "handler";

        // Initialize the value type mapper.  This is needed for methods with intrinsic 
        // return types, used in the Emit process.
        static ProxyFactory()
        {
            opCodeTypeMapper.Add(typeof(System.Boolean), OpCodes.Ldind_I1);
            opCodeTypeMapper.Add(typeof(System.Int16), OpCodes.Ldind_I2);
            opCodeTypeMapper.Add(typeof(System.Int32), OpCodes.Ldind_I4);
            opCodeTypeMapper.Add(typeof(System.Int64), OpCodes.Ldind_I8);
            opCodeTypeMapper.Add(typeof(System.Double), OpCodes.Ldind_R8);
            opCodeTypeMapper.Add(typeof(System.Single), OpCodes.Ldind_R4);
            opCodeTypeMapper.Add(typeof(System.UInt16), OpCodes.Ldind_U2);
            opCodeTypeMapper.Add(typeof(System.UInt32), OpCodes.Ldind_U4);
        }

        private ProxyFactory()
        {
        }

        public static ProxyFactory GetInstance()
        {
            if (instance == null)
            {
                CreateInstance();
            }

            return instance;
        }

        private static void CreateInstance()
        {
            lock (lockObj)
            {
                if (instance == null)
                {
                    instance = new ProxyFactory();
                }
            }
        }

        public Object Create(IProxyInvocationHandler handler, Type objType, bool isObjInterface)
        {
            string typeName = objType.FullName + PROXY_SUFFIX;
            Type type = (Type)typeMap[typeName];

            // check to see if the type was in the cache.  If the type was not cached, then
            // create a new instance of the dynamic type and add it to the cache.
            if (type == null)
            {
                if (isObjInterface)
                {
                    type = CreateType(handler, new Type[] { objType }, typeName);
                }
                else
                {
                    type = CreateType(handler, objType.GetInterfaces(), typeName);
                }

                typeMap.Add(typeName, type);
            }

            // return a new instance of the type.
            return Activator.CreateInstance(type, new object[] { handler });
        }

        public Object Create(IProxyInvocationHandler handler, Type objType)
        {
            return Create(handler, objType, false);
        }

        private Type CreateType(IProxyInvocationHandler handler, Type[] interfaces, string dynamicTypeName)
        {
            Type retVal = null;

            if (handler != null && interfaces != null)
            {
                Type objType = typeof(System.Object);
                Type handlerType = typeof(IProxyInvocationHandler);

                AppDomain domain = Thread.GetDomain();
                AssemblyName assemblyName = new AssemblyName();
                assemblyName.Name = ASSEMBLY_NAME;
                assemblyName.Version = new Version(1, 0, 0, 0);

                // create a new assembly for this proxy, one that isn't presisted on the file system
                AssemblyBuilder assemblyBuilder = domain.DefineDynamicAssembly(
                    assemblyName, AssemblyBuilderAccess.Run);
                    // assemblyName, AssemblyBuilderAccess.RunAndSave,".");  // to save it to the disk

                // create a new module for this proxy
                ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule(MODULE_NAME);

                // Set the class to be public and sealed
                TypeAttributes typeAttributes =
                    TypeAttributes.Class | TypeAttributes.Public | TypeAttributes.Sealed;

                // Gather up the proxy information and create a new type builder.  One that
                // inherits from Object and implements the interface passed in
                TypeBuilder typeBuilder = moduleBuilder.DefineType(
                    dynamicTypeName, typeAttributes, objType, interfaces);

                // Define a member variable to hold the delegate
                FieldBuilder handlerField = typeBuilder.DefineField(
                    HANDLER_NAME, handlerType, FieldAttributes.Private);


                // build a constructor that takes the delegate object as the only argument
                //ConstructorInfo defaultObjConstructor = objType.GetConstructor( new Type[0] );
                ConstructorInfo superConstructor = objType.GetConstructor(new Type[0]);
                ConstructorBuilder delegateConstructor = typeBuilder.DefineConstructor(
                    MethodAttributes.Public, CallingConventions.Standard, new Type[] { handlerType });

                #region( "Constructor IL Code" )
                ILGenerator constructorIL = delegateConstructor.GetILGenerator();

                // Load "this"
                constructorIL.Emit(OpCodes.Ldarg_0);
                // Load first constructor parameter
                constructorIL.Emit(OpCodes.Ldarg_1);
                // Set the first parameter into the handler field
                constructorIL.Emit(OpCodes.Stfld, handlerField);
                // Load "this"
                constructorIL.Emit(OpCodes.Ldarg_0);
                // Call the super constructor
                constructorIL.Emit(OpCodes.Call, superConstructor);
                // Constructor return
                constructorIL.Emit(OpCodes.Ret);
                #endregion

                // for every method that the interfaces define, build a corresponding 
                // method in the dynamic type that calls the handlers invoke method.  
                foreach (Type interfaceType in interfaces)
                {
                    GenerateMethod(interfaceType, handlerField, typeBuilder);
                }

                retVal = typeBuilder.CreateType();

                // assemblyBuilder.Save(dynamicTypeName + ".dll");
            }

            return retVal;
        }

        private static readonly MethodInfo INVOKE_METHOD = typeof(IProxyInvocationHandler).GetMethod("Invoke");
        private static readonly MethodInfo GET_METHODINFO_METHOD = typeof(MetaDataFactory).GetMethod("GetMethod", new Type[] { typeof(string), typeof(int) });

        private void GenerateMethod( Type interfaceType, FieldBuilder handlerField, TypeBuilder typeBuilder ) {
            MetaDataFactory.Add( interfaceType );
            MethodInfo[] interfaceMethods = interfaceType.GetMethods();
            PropertyInfo[] props = interfaceType.GetProperties();

            for ( int i = 0; i < interfaceMethods.Length; i++ ) {
                MethodInfo methodInfo = interfaceMethods[i];

                // Get the method parameters since we need to create an array
                // of parameter types
                ParameterInfo[] methodParams = methodInfo.GetParameters();
                int numOfParams = methodParams.Length;
                Type[] methodParameters = new Type[ numOfParams ];

                // convert the ParameterInfo objects into Type
                for ( int j = 0; j < numOfParams; j++ ) {
                    methodParameters[j] = methodParams[j].ParameterType;
                }

                // create a new builder for the method in the interface
                MethodBuilder methodBuilder = typeBuilder.DefineMethod(
                    methodInfo.Name, 
                    /*MethodAttributes.Public | MethodAttributes.Virtual | */ methodInfo.Attributes&~MethodAttributes.Abstract,
                    CallingConventions.Standard,
                    methodInfo.ReturnType, methodParameters );                                                   

                #region( "Handler Method IL Code" )
                ILGenerator methodIL = methodBuilder.GetILGenerator();
                        
                // load "this"
                methodIL.Emit( OpCodes.Ldarg_0 );
                // load the handler
                methodIL.Emit( OpCodes.Ldfld, handlerField );
                // load "this" since its needed for the call to invoke
                methodIL.Emit( OpCodes.Ldarg_0 );
                // load the name of the interface, used to get the MethodInfo object
                // from MetaDataFactory
                methodIL.Emit( OpCodes.Ldstr, interfaceType.FullName );
                // load the index, used to get the MethodInfo object 
                // from MetaDataFactory 
                methodIL.Emit( OpCodes.Ldc_I4, i ); 
                // invoke GetMethod in MetaDataFactory
                methodIL.Emit( OpCodes.Call, GET_METHODINFO_METHOD);

                // load the number of parameters onto the stack
                methodIL.Emit( OpCodes.Ldc_I4, numOfParams );
                // create a new array, using the size that was just pused on the stack
                methodIL.Emit( OpCodes.Newarr, typeof(object) );
                        
                // if we have any parameters, then iterate through and set the values
                // of each element to the corresponding arguments
                for ( int j = 0; j < numOfParams; j++ ) {
                    methodIL.Emit( OpCodes.Dup );   // this copies the array
                    methodIL.Emit( OpCodes.Ldc_I4, j );
                    methodIL.Emit( OpCodes.Ldarg, j + 1 );
                    if ( methodParameters[j].IsValueType ) {
                        methodIL.Emit( OpCodes.Box, methodParameters[j] );
                    }
                    methodIL.Emit( OpCodes.Stelem_Ref );                                    
                }

                // call the Invoke method
                methodIL.Emit( OpCodes.Callvirt, INVOKE_METHOD );
                        
                if ( methodInfo.ReturnType != typeof(void) ) { 
                    // if the return type if a value type, then unbox the return value
                    // so that we don't get junk.
                    if ( methodInfo.ReturnType.IsValueType  ) {
                        methodIL.Emit( OpCodes.Unbox, methodInfo.ReturnType );
                        if ( methodInfo.ReturnType.IsEnum ) {
                            methodIL.Emit( OpCodes.Ldind_I4 );
                        } else if ( !methodInfo.ReturnType.IsPrimitive ) {
                            methodIL.Emit( OpCodes.Ldobj, methodInfo.ReturnType );
                        } else {
                            methodIL.Emit( (OpCode) opCodeTypeMapper[ methodInfo.ReturnType ] );
                        }
                    }                                                                     
                } else {
                    // pop the return value that Invoke returned from the stack since
                    // the method's return type is void. 
                    methodIL.Emit( OpCodes.Pop );
                }
                                            
                // Return
                methodIL.Emit( OpCodes.Ret );
                #endregion
            }

            //for (int i = 0; i < props.Length; i++)
            //{
            //    PropertyInfo p = props[i];

            //    PropertyBuilder pb = typeBuilder.DefineProperty(p.Name, p.Attributes, p.PropertyType, new Type[] { p.PropertyType });
            //    pb.SetGetMethod((MethodBuilder)methodTable[p.GetGetMethod()]);
            //    pb.SetSetMethod((MethodBuilder)methodTable[p.GetSetMethod()]);
            //}

            // Iterate through the parent interfaces and recursively call this method
            foreach ( Type parentType in interfaceType.GetInterfaces() ) {
                GenerateMethod( parentType, handlerField, typeBuilder );            
            }
        }
    }
}
