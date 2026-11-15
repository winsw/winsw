using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Interop;
using Windows.Win32;
using WinSW.Native;
using Xunit;

namespace WinSW.Tests.Util
{
    internal sealed class InterProcessCodeCoverageSession : IDebugEventCallbacks
    {
        private readonly Type trackerType;
        private readonly FieldInfo hitsField;

        private readonly IDebugControl control;
        private readonly DataTarget target;
        private readonly Thread thread;

        private List<Exception> exceptions;
        private bool exited;

        internal InterProcessCodeCoverageSession(string serviceName)
        {
            var trackerType = this.trackerType = typeof(Program).Assembly.GetTypes().Single(type => type.Namespace == "Coverlet.Core.Instrumentation.Tracker");
            var hitsField = this.hitsField = trackerType.GetField("HitsArray", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(hitsField);

            using var scm = ServiceManager.Open(ServiceApis.ServiceManagerAccess.Connect);
            using var sc = scm.OpenService(serviceName, ServiceApis.ServiceAccess.QueryStatus);

            int processId = sc.ProcessId;
            Assert.True(processId >= 0);

            var guid = new Guid("27fe5639-8407-4f47-8364-ee118fb08ac8");
            PInvoke.DebugCreate(guid, out object unknown).ThrowOnFailure();

            var client = (IDebugClient)unknown;
            this.control = (IDebugControl)unknown;

            int hr = client.AttachProcess(0, (uint)processId, DEBUG_ATTACH.DEFAULT);
            AssertEx.Succeeded(hr);

            hr = client.SetEventCallbacks(this);
            AssertEx.Succeeded(hr);

            var pointer = Marshal.GetIUnknownForObject(client);
            Assert.Equal(3, Marshal.Release(pointer));

            target = DataTarget.CreateFromDbgEng(pointer);

            var thread = this.thread = new Thread(() =>
            {
                try
                {
                    using (this.target)
                    {
                        do
                        {
                            int hr = this.control.WaitForEvent(DEBUG_WAIT.DEFAULT, 0xffffffff);
                            AssertEx.Succeeded(hr);
                        }
                        while (!this.exited);
                    }
                }
                catch (Exception e)
                {
                    (this.exceptions ??= new List<Exception>()).Add(e);
                }
            });
            thread.Start();
        }

        /// <exception cref="AggregateException" />
        internal void Wait()
        {
            this.thread.Join();

            if (this.exceptions != null)
            {
                throw new AggregateException(this.exceptions);
            }
        }

        int IDebugEventCallbacks.GetInterestMask(out DEBUG_EVENT Mask)
        {
            Mask = DEBUG_EVENT.EXIT_PROCESS;
            return 0;
        }

        int IDebugEventCallbacks.Breakpoint(IDebugBreakpoint Bp)
        {
            throw new NotImplementedException();
        }

        int IDebugEventCallbacks.Exception(in EXCEPTION_RECORD64 Exception, uint FirstChance)
        {
            throw new NotImplementedException();
        }

        int IDebugEventCallbacks.CreateThread(ulong Handle, ulong DataOffset, ulong StartOffset)
        {
            throw new NotImplementedException();
        }

        int IDebugEventCallbacks.ExitThread(uint ExitCode)
        {
            throw new NotImplementedException();
        }

        int IDebugEventCallbacks.CreateProcess(ulong ImageFileHandle, ulong Handle, ulong BaseOffset, uint ModuleSize, string ModuleName, string ImageName, uint CheckSum, uint TimeDateStamp, ulong InitialThreadHandle, ulong ThreadDataOffset, ulong StartOffset)
        {
            throw new NotImplementedException();
        }

        int IDebugEventCallbacks.ExitProcess(uint ExitCode)
        {
            this.exited = true;

            try
            {
                using var runtime = this.target.ClrVersions.Single().CreateRuntime();

                ClrModule module = runtime.EnumerateModules().First(module => module.Name == typeof(Program).Assembly.Location);

                var type = module.GetTypeByName(this.trackerType.FullName);
                var field = type.GetStaticFieldByName(this.hitsField.Name);
                var array = field.ReadObject(runtime.AppDomains.Single()).AsArray();

                int[] hits = (int[])this.hitsField.GetValue(null);

                int[] values = array.ReadValues<int>(0, hits.Length);
                for (int i = 0; i < hits.Length; i++)
                {
                    hits[i] += values[i];
                }
            }
            catch (Exception e)
            {
                (this.exceptions ??= new List<Exception>()).Add(e);
            }

            return (int)DEBUG_STATUS.BREAK;
        }

        int IDebugEventCallbacks.LoadModule(ulong ImageFileHandle, ulong BaseOffset, uint ModuleSize, string ModuleName, string ImageName, uint CheckSum, uint TimeDateStamp)
        {
            throw new NotImplementedException();
        }

        int IDebugEventCallbacks.UnloadModule(string ImageBaseName, ulong BaseOffset)
        {
            throw new NotImplementedException();
        }

        int IDebugEventCallbacks.SystemError(uint Error, uint Level)
        {
            throw new NotImplementedException();
        }

        int IDebugEventCallbacks.SessionStatus(DEBUG_SESSION Status)
        {
            throw new NotImplementedException();
        }

        int IDebugEventCallbacks.ChangeDebuggeeState(DEBUG_CDS Flags, ulong Argument)
        {
            throw new NotImplementedException();
        }

        int IDebugEventCallbacks.ChangeEngineState(DEBUG_CES Flags, ulong Argument)
        {
            throw new NotImplementedException();
        }

        int IDebugEventCallbacks.ChangeSymbolState(DEBUG_CSS Flags, ulong Argument)
        {
            throw new NotImplementedException();
        }
    }
}
