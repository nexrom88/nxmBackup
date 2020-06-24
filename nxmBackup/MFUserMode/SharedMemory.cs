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
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr OpenFileMapping(uint dwDesiredAccess, bool bInheritHandle, string lpName);

        private const int sectionSize = 1048576;
        //maps a view to the km shared memory section
        public Stream mapSharedBuffer()
        {
            var mappedFile = System.IO.MemoryMappedFiles.MemoryMappedFile.CreateOrOpen("\\GLOBAL\\nxmmf", sectionSize);

            return mappedFile.CreateViewStream();
        }


    }
}
