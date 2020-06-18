using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace nxmBackup.MFUserMode
{
     public class SharedMemory
    {
        [DllImport("ntdll.dll", SetLastError = true)]
        static extern uint NtMapViewOfSection(IntPtr SectionHandle, IntPtr ProcessHandle, ref IntPtr BaseAddress, IntPtr ZeroBits, IntPtr CommitSize, out ulong SectionOffset, out uint ViewSize, uint InheritDisposition, uint AllocationType, uint Win32Protect);

        [DllImport("ntdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int NtCreateSection(ref IntPtr section, uint desiredAccess, IntPtr pAttrs, ref Int64 pMaxSize, uint pageProt, uint allocationAttribs, IntPtr hFile);

        [DllImport("ntdll.dll", SetLastError = false)]
        static extern int NtClose(IntPtr hObject);

        private IntPtr sectionHandler = IntPtr.Zero;

        //creates a shared memory buffer with the given byte length
        public byte[] allocSharedBuffer(Int64 length)
        {
            UInt32 length32 = (UInt32)length;
            IntPtr baseAddr = IntPtr.Zero;
            ulong sectionOffset = 0;
            int status = NtCreateSection(ref this.sectionHandler, SECTION_ALL_ACCESS, (IntPtr)0, ref length, PageReadWriteExecute, SecCommit, (IntPtr)0);
            uint ustatus = NtMapViewOfSection(this.sectionHandler, System.Diagnostics.Process.GetCurrentProcess().Handle, ref baseAddr, (IntPtr)0, (IntPtr)0, out sectionOffset, out length32, 1, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);


            return new byte[8];
        }

        //releases the shared memory
        public void releaseSharedBuffer()
        {
            if (this.sectionHandler != IntPtr.Zero)
            {
                int status = NtClose(this.sectionHandler);
            }
        }

        public const UInt32 STANDARD_RIGHTS_REQUIRED = 0x000F0000;
        public const UInt32 SECTION_QUERY = 0x0001;
        public const UInt32 SECTION_MAP_WRITE = 0x0002;
        public const UInt32 SECTION_MAP_READ = 0x0004;
        public const UInt32 SECTION_MAP_EXECUTE = 0x0008;
        public const UInt32 SECTION_EXTEND_SIZE = 0x0010;
        public const UInt32 SECTION_ALL_ACCESS = (STANDARD_RIGHTS_REQUIRED | SECTION_QUERY | SECTION_MAP_WRITE | SECTION_MAP_READ | SECTION_MAP_EXECUTE | SECTION_EXTEND_SIZE);

        public const uint PageReadWriteExecute = 0x40;

        public const uint SecCommit = 0x08000000;

        private const UInt32 MEM_COMMIT = 0x00001000;
        private const UInt32 MEM_RESERVE = 0x00002000;

        private const UInt32 PAGE_READWRITE = 0x04;

    }
}
