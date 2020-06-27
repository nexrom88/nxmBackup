using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.IO.MemoryMappedFiles;
using System.IO;

namespace nxmBackup.MFUserMode
{
     public class SharedMemory
    {
        [DllImport("ntdll.dll", CharSet = CharSet.Unicode)]
        public static extern uint ZwOpenSection(out IntPtr sectionHandle, uint desiredAccess, ref OBJECT_ATTRIBUTES attributes);

        [DllImport("ntdll.dll", SetLastError = false)]
        static extern int NtClose(IntPtr hObject);

        private const uint SECTION_MAP_WRITE = 2;

        private const uint OBJ_FORCE_ACCESS_CHECK = 0x00000400;
        private const uint OBJ_KERNEL_HANDLE = 0x00000200;

        private const int sectionSize = 1048576;
        //maps a view to the km shared memory section
        public void mapSharedBuffer()
        {
            IntPtr sectionHandle = IntPtr.Zero;

            OBJECT_ATTRIBUTES attributes = new OBJECT_ATTRIBUTES("\\BaseNamedObjects\\nxmmf", 0);

            uint mappingHandle = ZwOpenSection(out sectionHandle, SECTION_MAP_WRITE, ref attributes);


            NtClose(sectionHandle);
        }

        //native structs for win32 API
        [StructLayout(LayoutKind.Sequential)]
        public struct OBJECT_ATTRIBUTES : IDisposable
        {
            public int Length;
            public IntPtr RootDirectory;
            private IntPtr objectName;
            public uint Attributes;
            public IntPtr SecurityDescriptor;
            public IntPtr SecurityQualityOfService;

            public OBJECT_ATTRIBUTES(string name, uint attrs)
            {
                Length = 0;
                RootDirectory = IntPtr.Zero;
                objectName = IntPtr.Zero;
                Attributes = attrs;
                SecurityDescriptor = IntPtr.Zero;
                SecurityQualityOfService = IntPtr.Zero;

                Length = Marshal.SizeOf(this);
                ObjectName = new UNICODE_STRING(name);
            }

            public UNICODE_STRING ObjectName
            {
                get
                {
                    return (UNICODE_STRING)Marshal.PtrToStructure(
                     objectName, typeof(UNICODE_STRING));
                }

                set
                {
                    bool fDeleteOld = objectName != IntPtr.Zero;
                    if (!fDeleteOld)
                        objectName = Marshal.AllocHGlobal(Marshal.SizeOf(value));
                    Marshal.StructureToPtr(value, objectName, fDeleteOld);
                }
            }

            public void Dispose()
            {
                if (objectName != IntPtr.Zero)
                {
                    Marshal.DestroyStructure(objectName, typeof(UNICODE_STRING));
                    Marshal.FreeHGlobal(objectName);
                    objectName = IntPtr.Zero;
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct UNICODE_STRING : IDisposable
        {
            public ushort Length;
            public ushort MaximumLength;
            private IntPtr buffer;

            public UNICODE_STRING(string s)
            {
                Length = (ushort)(s.Length * 2);
                MaximumLength = (ushort)(Length + 2);
                buffer = Marshal.StringToHGlobalUni(s);
            }

            public void Dispose()
            {
                Marshal.FreeHGlobal(buffer);
                buffer = IntPtr.Zero;
            }

            public override string ToString()
            {
                return Marshal.PtrToStringUni(buffer);
            }
        }


    }
}
