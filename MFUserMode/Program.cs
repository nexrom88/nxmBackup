using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nxmMFUserMode
{
    using System.Runtime.InteropServices;
    class Program
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

        [DllImport("fltlib")]
        static extern unsafe uint FilterSendMessage(
            IntPtr hPort,
            void* lpInBuffer,
            int dwInBufferSize,
            void* lpOutBuffer,
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


        static void Main(string[] args)
        {
            IntPtr handle = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(IntPtr)));
            uint result = FilterConnectCommunicationPort("\\nxmQueryPort", 0, IntPtr.Zero, 0, IntPtr.Zero,  out handle);

            if (result != 0)
            {
                return;
            }

            while (true)
            {
                readMessages(handle);
            }

        }


        //reads one message
        public unsafe static void readMessages(IntPtr handle)
        {
            DATA_RECEIVE dataReceive = new DATA_RECEIVE();

            int headerSize = Marshal.SizeOf(dataReceive.messageHeader);
            int dataSize = BUFFER_SIZE + headerSize;



            uint status = FilterGetMessage(handle, ref dataReceive.messageHeader, dataSize, IntPtr.Zero);

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

            long offset = BitConverter.ToInt64(data, 0);
            long length = BitConverter.ToInt64(data, 8);

            Console.WriteLine(offset + "||" + length);

            data = new byte[length];
            System.IO.FileStream sourceStream = System.IO.File.OpenRead("c:\\source.mp3");
            sourceStream.Seek(offset, System.IO.SeekOrigin.Begin);
            sourceStream.Read(data, 0, (int)length);
            sourceStream.Close();


            //build reply
            for (int i = 0; i < length; i++)
            {
                reply.data[i] = data[i];
            }

            reply.replyHeader.messageId = dataReceive.messageHeader.messageId;
            reply.replyHeader.status = 0;

            int size = sizeof(FILTER_REPLY_MESSAGE);

            status = FilterReplyMessage(handle, ref reply, size);

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
