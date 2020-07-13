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
        [DllImport("ntdll.dll")]
        public static extern uint NtOpenSection(out IntPtr sectionHandle, uint desiredAccess, ref OBJECT_ATTRIBUTES attributes);

        [DllImport("ntdll.dll", SetLastError = true)]
        static extern uint NtMapViewOfSection(IntPtr SectionHandle, IntPtr ProcessHandle, ref IntPtr BaseAddress, uint ZeroBits, uint CommitSize, UIntPtr SectionOffset, out uint ViewSize, uint InheritDisposition, uint AllocationType, uint Win32Protect);

        [DllImport("ntdll.dll", SetLastError = true)]
        static extern uint NtUnmapViewOfSection(IntPtr hProc, IntPtr baseAddr);

        private const uint SECTION_MAP_WRITE = 2;
        private const uint SECTION_MAP_READ = 4;

        private const uint VIEW_UNMAP = 2;

        private const uint PAGE_READWRITE = 4;

        private const uint OBJ_FORCE_ACCESS_CHECK = 0x00000400;
        private const uint OBJ_KERNEL_HANDLE = 0x00000200;

        private const int sectionSize = 1048576;
        private IntPtr baseAddress = IntPtr.Zero;
        private IntPtr sectionHandle = IntPtr.Zero;

        public IntPtr SharedMemoryPointer { get => baseAddress;}

        //maps a view to the km shared memory section
        public bool mapSharedBuffer()
        {  

            OBJECT_ATTRIBUTES attributes = new OBJECT_ATTRIBUTES("\\BaseNamedObjects\\nxmmfflr", 0);

            //opens the section created in km
            uint status = NtOpenSection(out sectionHandle, SECTION_MAP_WRITE | SECTION_MAP_READ, ref attributes);

            if (status != 0)
            {
                //error occured, return null pointer
                return false;
            }

            uint viewSize = sectionSize;

            //maps the section to a view
            status = NtMapViewOfSection(sectionHandle, System.Diagnostics.Process.GetCurrentProcess().Handle, ref this.baseAddress, 0, 0, UIntPtr.Zero, out viewSize, VIEW_UNMAP, 0, PAGE_READWRITE);

            //set memory to zero
            initMemory(viewSize);

            return status == 0;

        }

        //inits a given mapped view memory
        private void initMemory(uint size)
        {
            for (int i = 0; i < size; i++)
            {
                Marshal.WriteByte(this.baseAddress, 0);
            }
        }

        //unmaps a view to the km memory shared section
        public void unmapSharedBuffer()
        {
            if (this.baseAddress != IntPtr.Zero)
            {
                NtUnmapViewOfSection(System.Diagnostics.Process.GetCurrentProcess().Handle, this.baseAddress);
            }
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
