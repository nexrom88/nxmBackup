using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using HVBackupCore;

namespace MFUserMode
{
    using System.ComponentModel;
    using System.Runtime.InteropServices;
    public class MFUserMode
    {

        // Constant buffer size
        public const int BUFFER_SIZE = 2048;

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

        FileStream logStream = new FileStream("c:\\target\\log.txt", FileMode.Create, FileAccess.Write);

        public MFUserMode(BackupChainReader readableBackupChain)
        {
            this.readableBackupChain = readableBackupChain;
        }

        //empty constructor for debugging purposes
        public MFUserMode()
        {
        }

        //starts the connection to kernel Mode driver
        public bool connectToKM()
        {
            this.kmHandle = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(IntPtr)));
            uint result = FilterConnectCommunicationPort("\\nxmQueryPort", 0, IntPtr.Zero, 0, IntPtr.Zero,  out kmHandle);

            //return when "connect" is not successful
            if (result != 0)
            {
                return false;
            }


            // send command "1" to init shared memory
            int bytesReturnedDummy;
            byte[] inBuffer = new byte[1];
            inBuffer[0] = 1;
            IntPtr PInBuffer = Marshal.AllocHGlobal(1);
            Marshal.Copy(inBuffer, 0, PInBuffer, 1);


            result = FilterSendMessage(kmHandle, PInBuffer, 1, IntPtr.Zero, 0, out bytesReturnedDummy);

            //free memory
            Marshal.FreeHGlobal(PInBuffer);

            return result == 0;        

        }

        //close the km connection
        public void closeConnection()
        {
            CloseHandle(this.kmHandle);
            this.logStream.Close();
        }

        //reads one message
        public unsafe void readMessages()
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
                this.logStream.Write(buffer, 0, buffer.Length);

                //read the requested data from backup chain
                data = new byte[length];
                this.readableBackupChain.readFromChain(offset, length, ref data, 0);
            }



            //build reply
            for (int i = 0; i < data.Length; i++)
            {
                reply.data[i] = data[i];
            }

            reply.replyHeader.messageId = dataReceive.messageHeader.messageId;
            reply.replyHeader.status = 0;

            int size = sizeof(FILTER_REPLY_MESSAGE);

            status = FilterReplyMessage(this.kmHandle, ref reply, size);

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
            public fixed byte data[BUFFER_SIZE];
        }
    }
}
