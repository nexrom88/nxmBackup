using Common;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public partial class DeviceIO
    {
        public const uint FILE_SHARE_READ = 0x00000001;
        public const uint FILE_SHARE_WRITE = 0x00000002;
        public const uint FILE_SHARE_DELETE = 0x00000004;
        public const uint OPEN_EXISTING = 3;

        public const uint GENERIC_READ = (0x80000000);
        public const uint GENERIC_WRITE = (0x40000000);

        public const uint FILE_FLAG_NO_BUFFERING = 0x20000000;
        public const uint FILE_FLAG_WRITE_THROUGH = 0x80000000;
        public const uint FILE_READ_ATTRIBUTES = (0x0080);
        public const uint FILE_WRITE_ATTRIBUTES = 0x0100;
        public const uint ERROR_INSUFFICIENT_BUFFER = 122;

        public const int IOCTL_DISK_GET_PARTITION_INFO = 0x74004;
        public const int IOCTL_DISK_CREATE_DISK = 0x7C058;
        public const int IOCTL_DISK_UPDATE_PROPERTIES = 0x70140;
        public const int IOCTL_DISK_SET_DRIVE_LAYOUT_EX = 0x7C054;
        public const UInt32 IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS = 0x560000;

        public const int FSCTL_LOCK_VOLUME = 0x00090018;
        public const int FSCTL_DISMOUNT_VOLUME = 0x00090020;

        public const byte PARTITION_IFS = 0x07;

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct DRIVE_LAYOUT_INFORMATION_EX
        {
            public PARTITION_STYLE PartitionStyle;
            public Int32 PartitionCount;
            public DRIVE_LAYOUT_INFORMATION_MBR Mbr;
            public PARTITION_INFORMATION_EX Partition1;
        }

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct DRIVE_LAYOUT_INFORMATION_MBR
        {
            public Int32 Signature;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public Byte[] Reserved; //because of DRIVE_LAYOUT_INFORMATION_GPT
        }

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct PARTITION_INFORMATION_MBR
        {
            public Byte PartitionType;
            [MarshalAsAttribute(UnmanagedType.Bool)]
            public Boolean BootIndicator;
            [MarshalAsAttribute(UnmanagedType.Bool)]
            public Boolean RecognizedPartition;
            public Int32 HiddenSectors;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 96)]
            public Byte[] Reserved; //because of PARTITION_INFORMATION_GPT
        }

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct PARTITION_INFORMATION_EX
        {
            public PARTITION_STYLE PartitionStyle;
            public Int64 StartingOffset;
            public Int64 PartitionLength;
            public Int32 PartitionNumber;
            [MarshalAsAttribute(UnmanagedType.Bool)]
            public Boolean RewritePartition;
            public PARTITION_INFORMATION_MBR Mbr;
        }


        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct PARTITION_INFORMATION
        {
            public Int64 StartingOffset;
            public Int64 PartitionLength;
            public UInt32 HiddenSectors;
            public UInt32 PartitionNumber;
            public Byte PartitionType;
            public Byte BootIndicator;
            public Byte RecognizedPartition;
            public Byte RewritePartition;
        }

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct CREATE_DISK
        {
            public PARTITION_STYLE PartitionStyle;
            public CREATE_DISK_UNION_MBR_GPT MbrGpt;
        }

        public enum PARTITION_STYLE
        {
            PARTITION_STYLE_MBR = 0,
            PARTITION_STYLE_GPT = 1,
            PARTITION_STYLE_RAW = 2,
        }

        [StructLayoutAttribute(LayoutKind.Explicit)]
        public struct CREATE_DISK_UNION_MBR_GPT
        {
            [FieldOffset(0)]
            public CREATE_DISK_MBR Mbr;
            [FieldOffset(0)]
            public CREATE_DISK_GPT Gpt;
        }

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct CREATE_DISK_MBR
        {
            public Int32 Signature;
        }

        [SecurityPermission(SecurityAction.Demand)]
        public class VolumeSafeHandle : SafeHandleMinusOneIsInvalid
        {

            public VolumeSafeHandle()
                : base(true) { }


            protected override bool ReleaseHandle()
            {
                return CloseHandle(this.handle);
            }

            public IntPtr getPointer()
            {
                return this.handle;
            }

            public override string ToString()
            {
                return this.handle.ToString();
            }

            [DllImportAttribute("kernel32.dll", SetLastError = true)]
            [return: MarshalAsAttribute(UnmanagedType.Bool)]
            public static extern Boolean CloseHandle(IntPtr hObject);

        }

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct CREATE_DISK_GPT
        {
            public Guid DiskId;
            public Int32 MaxPartitionCount;
        }

        [DllImport("kernel32.dll")]
        public static extern bool GetFileSizeEx(IntPtr hFile, out long lpFileSize);

        [DllImportAttribute("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true)]
        public static extern VolumeSafeHandle CreateVolumeFile([InAttribute()][MarshalAsAttribute(UnmanagedType.LPWStr)] String lpFileName, UInt32 dwDesiredAccess, UInt32 dwShareMode, [InAttribute()] IntPtr lpSecurityAttributes, UInt32 dwCreationDisposition, UInt32 dwFlagsAndAttributes, [InAttribute()] IntPtr hTemplateFile);


        [DllImportAttribute("kernel32.dll", EntryPoint = "DeviceIoControl", SetLastError = true)]
        [return: MarshalAsAttribute(UnmanagedType.Bool)]
        public static extern bool DeviceIoControl([InAttribute()] VolumeSafeHandle hDevice, UInt32 dwIoControlCode, [InAttribute()] IntPtr lpInBuffer, Int32 nInBufferSize, ref VirtualDiskHandler.VOLUME_DISK_EXTENTS lpOutBuffer, Int32 nOutBufferSize, ref Int32 lpBytesReturned, IntPtr lpOverlapped);


        [DllImportAttribute("kernel32.dll", EntryPoint = "DeviceIoControl", SetLastError = true)]
        [return: MarshalAsAttribute(UnmanagedType.Bool)]
        public static extern bool DeviceIoControl([InAttribute()] VolumeSafeHandle hDevice, UInt32 dwIoControlCode, [InAttribute()] IntPtr lpInBuffer, Int32 nInBufferSize, IntPtr lpOutBuffer, Int32 nOutBufferSize, ref Int32 lpBytesReturned, IntPtr lpOverlapped);


        [DllImportAttribute("kernel32.dll", EntryPoint = "DeviceIoControl", SetLastError = true)]
        [return: MarshalAsAttribute(UnmanagedType.Bool)]
        public static extern Boolean DeviceIoControl(SafeFileHandle hDevice, Int32 dwIoControlCode, ref DRIVE_LAYOUT_INFORMATION_EX lpInBuffer, int nInBufferSize, IntPtr lpOutBuffer, Int32 nOutBufferSize, ref Int32 lpBytesReturned, IntPtr lpOverlapped);


        [DllImportAttribute("kernel32.dll", EntryPoint = "DeviceIoControl", SetLastError = true)]
        [return: MarshalAsAttribute(UnmanagedType.Bool)]
        public static extern Boolean DeviceIoControl(SafeFileHandle hDevice, Int32 dwIoControlCode, IntPtr lpInBuffer, int nInBufferSize, ref PARTITION_INFORMATION lpOutBuffer, Int32 nOutBufferSize, ref Int32 lpBytesReturned, IntPtr lpOverlapped);


        [DllImportAttribute("kernel32.dll", EntryPoint = "DeviceIoControl", SetLastError = true)]
        [return: MarshalAsAttribute(UnmanagedType.Bool)]
        public static extern Boolean DeviceIoControl(SafeFileHandle hDevice, Int32 dwIoControlCode, IntPtr lpInBuffer, int nInBufferSize, IntPtr lpOutBuffer, Int32 nOutBufferSize, ref Int32 lpBytesReturned, IntPtr lpOverlapped);

        [DllImportAttribute("kernel32.dll", EntryPoint = "DeviceIoControl", SetLastError = true)]
        [return: MarshalAsAttribute(UnmanagedType.Bool)]
        public static extern Boolean DeviceIoControl(SafeFileHandle hDevice, Int32 dwIoControlCode, ref CREATE_DISK lpInBuffer, int nInBufferSize, IntPtr lpOutBuffer, Int32 nOutBufferSize, ref Int32 lpBytesReturned, IntPtr lpOverlapped);


        [DllImport("kernel32.dll", SetLastError = true)]
        public static unsafe extern SafeFileHandle CreateFile(
            string FileName,
            uint DesiredAccess,
            uint ShareMode,
            IntPtr SecurityAttributes,
            uint CreationDisposition,
            uint FlagsAndAttributes,
            IntPtr hTemplateFile);


        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(SafeFileHandle hHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(DeviceIO.VolumeSafeHandle hHandle);



        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern unsafe bool WriteFile(
            SafeFileHandle hFile,
            byte* pBuffer,
            uint NumberOfBytesToWrite,
            uint* pNumberOfBytesWritten,
            IntPtr Overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern unsafe bool WriteFile(
            DeviceIO.VolumeSafeHandle hFile,
            byte* pBuffer,
            uint NumberOfBytesToWrite,
            uint* pNumberOfBytesWritten,
            IntPtr Overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern unsafe bool ReadFile(
            SafeFileHandle hFile,
            byte* pBuffer,
            uint NumberOfBytesToRead,
            uint* pNumberOfBytesRead,
            IntPtr Overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern unsafe bool ReadFile(
            DeviceIO.VolumeSafeHandle hFile,
            byte* pBuffer,
            uint NumberOfBytesToRead,
            uint* pNumberOfBytesRead,
            IntPtr Overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetFilePointerEx(
            SafeFileHandle hFile,
            ulong liDistanceToMove,
            out ulong lpNewFilePointer,
            uint dwMoveMethod);

        [DllImport("kernel32.dll")]
        public static extern bool FlushFileBuffers(
            SafeFileHandle hFile);

    }
}
