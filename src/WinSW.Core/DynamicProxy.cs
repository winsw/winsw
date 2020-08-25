using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

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
        object? Invoke(object proxy, MethodInfo method, object[] parameters);
    }

    /// <summary>
    /// </summary>
    public static class ProxyFactory
    {
        private const string ProxySuffix = "Proxy";
        private const string AssemblyName = "ProxyAssembly";
        private const string ModuleName = "ProxyModule";
        private const string HandlerName = "handler";

        private static readonly Dictionary<string, Type> TypeCache = new Dictionary<string, Type>();

        private static readonly AssemblyBuilder AssemblyBuilder =
#if VNEXT
            AssemblyBuilder.DefineDynamicAssembly(
#else
            AppDomain.CurrentDomain.DefineDynamicAssembly(
#endif
                new AssemblyName(AssemblyName), AssemblyBuilderAccess.Run);

        private static readonly ModuleBuilder ModuleBuilder = AssemblyBuilder.DefineDynamicModule(ModuleName);

        public static object Create(IProxyInvocationHandler handler, Type objType, bool isObjInterface = false)
        {
            string typeName = objType.FullName + ProxySuffix;
            Type? type = null;
            lock (TypeCache)
            {
                if (!TypeCache.TryGetValue(typeName, out type))
                {
                    type = CreateType(typeName, isObjInterface ? new Type[] { objType } : objType.GetInterfaces());
                    TypeCache.Add(typeName, type);
                }
            }

            return Activator.CreateInstance(type, new object[] { handler })!;
        }

        private static Type CreateType(string dynamicTypeName, Type[] interfaces)
        {
            Type objType = typeof(object);
            Type handlerType = typeof(IProxyInvocationHandler);

            TypeAttributes typeAttributes = TypeAttributes.Public | TypeAttributes.Sealed;

            // Gather up the proxy information and create a new type builder.  One that
            // inherits from Object and implements the interface passed in
            TypeBuilder typeBuilder = ModuleBuilder.DefineType(
                dynamicTypeName, typeAttributes, objType, interfaces);

            // Define a member variable to hold the delegate
            FieldBuilder handlerField = typeBuilder.DefineField(
                HandlerName, handlerType, FieldAttributes.Private | FieldAttributes.InitOnly);

            // build a constructor that takes the delegate object as the only argument
            ConstructorInfo baseConstructor = objType.GetConstructor(Type.EmptyTypes)!;
            ConstructorBuilder delegateConstructor = typeBuilder.DefineConstructor(
                MethodAttributes.Public, CallingConventions.Standard, new Type[] { handlerType });

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
            constructorIL.Emit(OpCodes.Call, baseConstructor);

            // Constructor return
            constructorIL.Emit(OpCodes.Ret);

            // for every method that the interfaces define, build a corresponding
            // method in the dynamic type that calls the handlers invoke method.
            foreach (Type interfaceType in interfaces)
            {
                GenerateMethod(interfaceType, handlerField, typeBuilder);
            }

            return typeBuilder.CreateType()!;
        }

        /// <summary>
        /// <see cref="IProxyInvocationHandler.Invoke(object, MethodInfo, object[])"/>.
        /// </summary>
        private static readonly MethodInfo InvokeMethod = typeof(IProxyInvocationHandler).GetMethod(nameof(IProxyInvocationHandler.Invoke))!;

        /// <summary>
        /// <see cref="MethodBase.GetMethodFromHandle(RuntimeMethodHandle)"/>.
        /// </summary>
        private static readonly MethodInfo GetMethodFromHandleMethod = typeof(MethodBase).GetMethod(nameof(MethodBase.GetMethodFromHandle), new[] { typeof(RuntimeMethodHandle) })!;

        private static void GenerateMethod(Type interfaceType, FieldBuilder handlerField, TypeBuilder typeBuilder)
        {
            MethodInfo[] interfaceMethods = interfaceType.GetMethods();

            for (int i = 0; i < interfaceMethods.Length; i++)
            {
                MethodInfo methodInfo = interfaceMethods[i];

                // Get the method parameters since we need to create an array
                // of parameter types
                ParameterInfo[] methodParams = methodInfo.GetParameters();
                int numOfParams = methodParams.Length;
                Type[] methodParameters = new Type[numOfParams];

                // convert the ParameterInfo objects into Type
                for (int j = 0; j < numOfParams; j++)
                {
                    methodParameters[j] = methodParams[j].ParameterType;
                }

                // create a new builder for the method in the interface
                MethodBuilder methodBuilder = typeBuilder.DefineMethod(
                    methodInfo.Name,
                    /*MethodAttributes.Public | MethodAttributes.Virtual | */ methodInfo.Attributes & ~MethodAttributes.Abstract,
                    CallingConventions.Standard,
                    methodInfo.ReturnType,
                    methodParameters);

                ILGenerator methodIL = methodBuilder.GetILGenerator();

                // invoke target: IProxyInvocationHandler
                methodIL.Emit(OpCodes.Ldarg_0);
                methodIL.Emit(OpCodes.Ldfld, handlerField);

                // 1st parameter: object proxy
                methodIL.Emit(OpCodes.Ldarg_0);

                // 2nd parameter: MethodInfo method
                methodIL.Emit(OpCodes.Ldtoken, methodInfo);
                methodIL.Emit(OpCodes.Call, GetMethodFromHandleMethod);
                methodIL.Emit(OpCodes.Castclass, typeof(MethodInfo));

                // 3rd parameter: object[] parameters
                methodIL.Emit(OpCodes.Ldc_I4, numOfParams);
                methodIL.Emit(OpCodes.Newarr, typeof(object));

                // if we have any parameters, then iterate through and set the values
                // of each element to the corresponding arguments
                for (int j = 0; j < numOfParams; j++)
                {
                    methodIL.Emit(OpCodes.Dup); // copy the array
                    methodIL.Emit(OpCodes.Ldc_I4, j);
                    methodIL.Emit(OpCodes.Ldarg, j + 1); // +1 for "this"
                    if (methodParameters[j].IsValueType)
                    {
                        methodIL.Emit(OpCodes.Box, methodParameters[j]);
                    }

                    methodIL.Emit(OpCodes.Stelem_Ref);
                }

                // call the Invoke method
                methodIL.Emit(OpCodes.Callvirt, InvokeMethod);

                if (methodInfo.ReturnType != typeof(void))
                {
                    methodIL.Emit(OpCodes.Unbox_Any, methodInfo.ReturnType);
                }
                else
                {
                    // pop the return value that Invoke returned from the stack since
                    // the method's return type is void.
                    methodIL.Emit(OpCodes.Pop);
                }

                // Return
                methodIL.Emit(OpCodes.Ret);
            }

            // Iterate through the parent interfaces and recursively call this method
            foreach (Type parentType in interfaceType.GetInterfaces())
            {
                GenerateMethod(parentType, handlerField, typeBuilder);
            }
        }
    }
}
