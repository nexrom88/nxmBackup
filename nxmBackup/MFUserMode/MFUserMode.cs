using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using HVBackupCore;

namespace nxmBackup.MFUserMode
{
    using nxmBackup.MFUserMode;
    using System.ComponentModel;
    using System.Runtime.InteropServices;
    public class MFUserMode
    {

        // Constant buffer size
        public const int BUFFER_SIZE = 100;

        [DllImport("fltlib", CharSet = CharSet.Auto)]
        static extern unsafe uint FilterConnectCommunicationPort(
            string lpPortName,
            uint dwOptions,
            IntPtr lpContext,
            uint dwSizeOfContext,
            IntPtr lpSecurityAttributes,
            out IntPtr hPort
       );

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hHandle);

        [DllImport("fltlib")]
        static extern unsafe uint FilterSendMessage(
            IntPtr hPort,
            IntPtr lpInBuffer,
            int dwInBufferSize,
            IntPtr lpOutBuffer,
            int dwOutBufferSize,
            out int lpBytesReturned
        );

        [DllImport("FltLib", CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Auto)]
        private static extern uint FilterGetMessage(
             IntPtr hPort,
             ref FILTER_MESSAGE_HEADER lpMessageBuffer,
             int dwMessageBufferSize,
             IntPtr lpOverlapped);

        [DllImport("FltLib", CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Auto)]
        private static extern uint FilterReplyMessage(
            IntPtr hPort,
            ref FILTER_REPLY_MESSAGE lpMessageBuffer,
            int dwReplyBufferSize);


        //source stream (usually seekable decompression stream)
        private BackupChainReader readableBackupChain;

        //the handle to the km connection
        private IntPtr kmHandle;


        //shared memory with km
        SharedMemory sharedMemoryHandler = new SharedMemory();

        public MFUserMode(BackupChainReader readableBackupChain)
        {
            this.readableBackupChain = readableBackupChain;
        }

        //empty constructor for debugging purposes
        public MFUserMode()
        {
        }

        //starts the connection to kernel Mode driver
        public bool connectToKM(string portName, string sectionName)
        {
            this.kmHandle = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(IntPtr)));
            uint result = FilterConnectCommunicationPort(portName, 0, IntPtr.Zero, 0, IntPtr.Zero,  out kmHandle);

            //alloc shared memory
            if (result == 0)
            {
                return this.sharedMemoryHandler.mapSharedBuffer(sectionName);
            }
            else
            {
                return false;
            }    

        }

        //close the km connection
        public void closeConnection()
        {
            CloseHandle(this.kmHandle);

            //close shared memory view if necessary
            if (this.sharedMemoryHandler.SharedMemoryPointer != IntPtr.Zero)
            {
                this.sharedMemoryHandler.unmapSharedBuffer();
            }
        }

        //writes one "standalone" message to km (no response)
        public unsafe bool writeMessage(byte[] data)
        {
            //copy data to unmanaged memory
            IntPtr dataPtr = Marshal.AllocHGlobal(data.Length);
            Marshal.Copy(data, 0, dataPtr, data.Length);

            //write message to KM
            int dummy;
            return FilterSendMessage(this.kmHandle, dataPtr, data.Length, IntPtr.Zero, 0, out dummy) == 0;

        }

        //reads one LB message from kernel mode and reads shared memory
        public unsafe LB_BLOCK handleLBMessage()
        {
            DATA_RECEIVE dataReceive = new DATA_RECEIVE();
            LB_BLOCK retVal = new LB_BLOCK();

            int headerSize = Marshal.SizeOf(dataReceive.messageHeader);
            int dataSize = 100;

            //read message
            uint status = FilterGetMessage(this.kmHandle, ref dataReceive.messageHeader, dataSize, IntPtr.Zero);

            if (status != 0)
            {
                retVal.isValid = false;
                return retVal;

            }

            byte[] managedBuffer = new byte[8 + 8 + 4];
            for(int i = 0; i < managedBuffer.Length; i++)
            {
                managedBuffer[i] = dataReceive.messageContent[i];
            }

            long offset = BitConverter.ToInt64(managedBuffer, 0);
            long length = BitConverter.ToInt64(managedBuffer, 8);
            int objectID = BitConverter.ToInt32(managedBuffer, 16);

            //copy shared memory to retVal struct
            retVal.buffer = new byte[length];
            Marshal.Copy(IntPtr.Add(this.sharedMemoryHandler.SharedMemoryPointer, 1), retVal.buffer, 0, (int)length);
            retVal.isValid = true;
            retVal.length = length;
            retVal.offset = offset;
            retVal.objectID = objectID;

            //set sharedmemory unseen flag to 0
            Marshal.WriteByte(this.sharedMemoryHandler.SharedMemoryPointer, 0);

            return retVal;

        }


        //reads one flr message
        public unsafe void handleFLRMessage()
        {
            DATA_RECEIVE dataReceive = new DATA_RECEIVE();

            int headerSize = Marshal.SizeOf(dataReceive.messageHeader);
            int dataSize = BUFFER_SIZE + headerSize;


            uint status = FilterGetMessage(this.kmHandle, ref dataReceive.messageHeader, dataSize, IntPtr.Zero);

            if (status != 0)
            {
                return;

            }

            FILTER_REPLY_MESSAGE reply = new FILTER_REPLY_MESSAGE();

            byte[] data = new byte[BUFFER_SIZE];
            //move data to managed memory
            for (int i = 0; i < BUFFER_SIZE; i++)
            {
                data[i] = dataReceive.messageContent[i];
            }

            byte requestType = data[0];

            long offset = BitConverter.ToInt64(data, 1);
            long length = BitConverter.ToInt64(data, 9);

            //have to read data?
            if (requestType == 1)
            {
                string output = "offset: " + offset + " length: " + length + "\n";
                System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
                byte[] buffer = enc.GetBytes(output);

                //read the requested data from backup chain
                data = new byte[length];
                this.readableBackupChain.readFromChain(offset, length, data, 0);

            }


            //write payload data to shared memory
            Marshal.Copy(data, 0, this.sharedMemoryHandler.SharedMemoryPointer, data.Length);

            //byte[] temp = new byte[100];
            //for (int i = 0; i < 100; i++)
            //{
            //    temp[i] = Marshal.ReadByte(this.sharedMemoryHandler.SharedMemoryPointer, i);
            //}

            reply.replyHeader.messageId = dataReceive.messageHeader.messageId;
            reply.replyHeader.status = 0;

            int size = sizeof(FILTER_REPLY_MESSAGE);

            status = FilterReplyMessage(this.kmHandle, ref reply, size);

        }

        //written block for LB
        public struct LB_BLOCK
        {
            public bool isValid;
            public int objectID;
            public long offset;
            public long length;
            public byte[] buffer;
        }

        // message receive struct
        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct DATA_RECEIVE
        {
            public FILTER_MESSAGE_HEADER messageHeader;
            public fixed byte messageContent[BUFFER_SIZE];
        }

        // message header struct
        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct FILTER_MESSAGE_HEADER
        {
            public uint replyLength;
            public ulong messageId;
        }

        // message header struct
        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct FILTER_REPLY_HEADER
        {
            public uint status;
            public ulong messageId;
        }

        // message receive struct
        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct FILTER_REPLY_MESSAGE
        {
            public FILTER_REPLY_HEADER replyHeader;
            public fixed byte data[1];
        }
    }
}
