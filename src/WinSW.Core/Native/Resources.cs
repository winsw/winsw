using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using static WinSW.Native.ResourceApis;

namespace WinSW.Native
{
    internal static class Resources
    {
        /// <exception cref="CommandException" />
        internal static unsafe bool UpdateCompanyName(string path, string outputPath, string companyName)
        {
            var module = LoadLibraryW(path);
            try
            {
                var verInfo = FindResourceW(module, VS_VERSION_INFO, RT_VERSION);
                if (verInfo == IntPtr.Zero)
                {
                    Exit();
                }

                var resData = LoadResource(module, verInfo);
                if (resData == IntPtr.Zero)
                {
                    Exit();
                }

                var resAddr = LockResource(resData);

                var address = resAddr;
                int offset = 0;

                short length = ((short*)address)[0];
                short valueLength = ReadHeaderAndAdvance();
                string key = ReadKeyAndAdvance();
                Debug.Assert(key == "VS_VERSION_INFO");
                offset += valueLength;
                Align();

                valueLength = ReadHeaderAndAdvance();
                key = ReadKeyAndAdvance();
                Debug.Assert(key == "VarFileInfo");
                offset += valueLength;
                Align();

                valueLength = ReadHeaderAndAdvance();
                key = ReadKeyAndAdvance();
                Debug.Assert(key == "Translation");
                ushort language = ((ushort*)address)[0];
                ushort codePage = ((ushort*)address)[1];
                offset += valueLength;
                address = resAddr + offset;

                valueLength = ReadHeaderAndAdvance();
                key = ReadKeyAndAdvance();
                Debug.Assert(key == "StringFileInfo");
                offset += valueLength;
                Align();

                short stringTableLength = ((short*)address)[0];
                int stringTableEndOffset = offset + stringTableLength;
                valueLength = ReadHeaderAndAdvance();
                key = ReadKeyAndAdvance();
                Debug.Assert(key == $"{language:x4}{codePage:x4}");

                do
                {
                    int valueLengthOffset = offset + sizeof(short);
                    valueLength = ReadHeaderAndAdvance();
                    key = ReadKeyAndAdvance();

                    if (key != "CompanyName")
                    {
                        offset += sizeof(short) * valueLength;
                        Align(); // ?
                        continue;
                    }

                    // int oldLength = "CloudBees, Inc.".Length + 1; // 16
                    int newLength = companyName.Length + 1;
                    Debug.Assert(newLength > 12 && newLength <= 16);

                    var newAddress = Marshal.AllocHGlobal(length);
                    try
                    {
                        Buffer.MemoryCopy((void*)resAddr, (void*)newAddress, length, length);

                        *(short*)(newAddress + valueLengthOffset) = (short)newLength;
                        fixed (char* ptr = companyName)
                        {
                            Buffer.MemoryCopy(ptr, (void*)(newAddress + offset), newLength * sizeof(char), newLength * sizeof(char));
                        }

                        File.Copy(path, outputPath, true);

                        var update = BeginUpdateResourceW(outputPath, false);
                        if (update == IntPtr.Zero)
                        {
                            Exit();
                        }

                        try
                        {
                            if (!UpdateResourceW(update, RT_VERSION, VS_VERSION_INFO, language, newAddress, length))
                            {
                                Exit();
                            }

                            if (!EndUpdateResourceW(update, false))
                            {
                                Exit();
                            }

                            return true;
                        }
                        catch
                        {
                            _ = EndUpdateResourceW(update, true);
                            throw;
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(newAddress);
                    }
                }
                while (offset < stringTableEndOffset);

                return false;

                static void Exit()
                {
                    Throw.Command.Win32Exception();
                }

                void Align()
                {
                    if ((offset & 3) != 0)
                    {
                        offset &= ~3;
                        offset += 4;
                    }

                    address = resAddr + offset;
                }

                short ReadHeaderAndAdvance()
                {
                    valueLength = ((short*)address)[1];
                    offset += sizeof(short) * 3;
                    address = resAddr + offset;
                    return valueLength;
                }

                string ReadKeyAndAdvance()
                {
                    string key = Marshal.PtrToStringUni(address)!;
                    offset += sizeof(char) * (key.Length + 1);
                    Align();
                    return key;
                }
            }
            finally
            {
                _ = FreeLibrary(module);
            }
        }
    }
}
