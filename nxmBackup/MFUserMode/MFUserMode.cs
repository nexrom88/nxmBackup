using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using HVRestoreCore;

namespace nxmBackup.MFUserMode
{
    using nxmBackup.MFUserMode;
    using System.ComponentModel;
    using System.Runtime.InteropServices;
    using System.Threading;

    public class MFUserMode
    {
        // Constant buffer size
        public const int BUFFER_SIZE = 100;

        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int memcmp(byte[] b1, byte[] b2, long count);

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
        private BackupChainReader[] readableBackupChains;

        //the handle to the km connection
        private IntPtr kmHandle;

        //minifilter instance name
        private const string mfName = "nxmmf";

        //lb time limit
        private UInt64 lbTimeLimit;

        //vars used for encryption
        private bool useEncryption;
        private byte[] aesKey;

        //FileStream compareStream = new FileStream(@"d:\original_fixed.vhdx", System.IO.FileMode.Open, System.IO.FileAccess.Read);


        //shared memory with km
        SharedMemory sharedMemoryHandler = new SharedMemory();

        public MFUserMode(BackupChainReader[] readableBackupChains, UInt64 lbTimeLimit, bool useEncryption, byte[]aesKey)
        {
            this.readableBackupChains = readableBackupChains;
            this.lbTimeLimit = lbTimeLimit;
            this.aesKey = aesKey;
            this.useEncryption = useEncryption;
        }

        //empty constructor for debugging purposes
        public MFUserMode()
        {

        }

        //loads the minifilter driver
        private bool loadMF()
        {
            //first unload if already running
            unloadMF(false);

            //start mf
            System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo("fltmc.exe");
            psi.Arguments = "load " + mfName;
            psi.UseShellExecute = true;
            psi.RedirectStandardError = true;
            psi.RedirectStandardOutput = true;
            psi.UseShellExecute = false;
            System.Diagnostics.Process proc = System.Diagnostics.Process.Start(psi);
            proc.WaitForExit();
            int errorCode = proc.ExitCode;

            //write to log when error
            if (errorCode != 0)
            {
                string errorMessage = proc.StandardOutput.ReadToEnd();
                Common.DBQueries.addLog("loading MF failed. Error Code: " + errorCode.ToString() + Environment.NewLine + errorMessage, Environment.StackTrace, null);
            }
            return errorCode == 0;
        }

        //unloads the minifilter driver
        private bool unloadMF(bool cancelLB)
        {
            if (cancelLB)
            {
                //send stop signal to km
                byte[] data = new byte[128];
                data[0] = 3; //stop signal

                //write data buffers to km
                writeMessage(data);
                System.Threading.Thread.Sleep(500);
            }

            //exec command: fltmc unload nxmmf
            System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo("fltmc.exe");
            psi.Arguments = "unload " + mfName;
            psi.UseShellExecute = true;
            psi.RedirectStandardError = true;
            psi.RedirectStandardOutput = true;
            psi.UseShellExecute = false;
            System.Diagnostics.Process proc = System.Diagnostics.Process.Start(psi);
            proc.WaitForExit();
            int errorCode = proc.ExitCode;
            return errorCode == 0;
        }

        //starts the connection to kernel Mode driver
        public bool connectToKM(string portName, string sectionName)
        {
            //load mf first
            if (!loadMF())
            {

                return false;
            }

            //start km connection
            this.kmHandle = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(IntPtr)));
            uint result = FilterConnectCommunicationPort(portName, 0, IntPtr.Zero, 0, IntPtr.Zero, out kmHandle);

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
        public void closeConnection(bool cancelLB)
        {
            CloseHandle(this.kmHandle);

            //close shared memory view if necessary
            if (this.sharedMemoryHandler.SharedMemoryPointer != IntPtr.Zero)
            {
                this.sharedMemoryHandler.unmapSharedBuffer(true);
            }

            //unload mf
            unloadMF(cancelLB);
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

            int dataSize = BUFFER_SIZE + headerSize;

            //read message
            uint status = FilterGetMessage(this.kmHandle, ref dataReceive.messageHeader, dataSize, IntPtr.Zero);

            if (status != 0)
            {
                retVal.isValid = false;
                return retVal;

            }

            byte[] managedBuffer = new byte[1+ 8 + 8 + 4];
            for (int i = 0; i < managedBuffer.Length; i++)
            {
                managedBuffer[i] = dataReceive.messageContent[i];
            }

            //ignore first byte, because it is always set to 2 (write mode)
            long offset = BitConverter.ToInt64(managedBuffer, 1);
            long length = BitConverter.ToInt64(managedBuffer, 9);
            int objectID = BitConverter.ToInt32(managedBuffer, 17);

            //copy shared memory to retVal struct
            retVal.buffer = new byte[length];
            Marshal.Copy(this.sharedMemoryHandler.SharedMemoryPointer, retVal.buffer, 0, (int)length);

            //send reply to km
            FILTER_REPLY_MESSAGE reply = new FILTER_REPLY_MESSAGE();
            reply.replyHeader.messageId = dataReceive.messageHeader.messageId;
            reply.replyHeader.status = 0;
            int size = sizeof(FILTER_REPLY_HEADER) + 1;
            status = FilterReplyMessage(this.kmHandle, ref reply, size);


            retVal.isValid = true;
            retVal.length = length;
            retVal.offset = offset;
            retVal.objectID = objectID;

            return retVal;

        }

        //reads one lr message
        public unsafe void handleLRMessage()
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
            byte vhdxTargetIndex = data[17];


            //get lr operation mode (2 = write, 1 = read)
            LROperationMode operationMode = new LROperationMode();
            switch (data[0])
            {
                case 2:
                    operationMode = LROperationMode.write;
                    break;
                case 1:
                    operationMode = LROperationMode.read;
                    break;
            }


            data = new byte[length];
            //perform a read request?
            if (operationMode == LROperationMode.read)
            {

                string logString = offset.ToString() + "|" + length.ToString() + Environment.NewLine;
                byte[] logBuffer = System.Text.Encoding.ASCII.GetBytes(logString);

                //read payload data from backup chain
                this.readableBackupChains[vhdxTargetIndex].readFromChain(offset, length, data, 0);
                this.readableBackupChains[vhdxTargetIndex].readFromLB(offset, length, data, lbTimeLimit, this.useEncryption, this.aesKey); //read from lb and system written blocks

                //set LogGUID to zero to disable log replay
                //disableLogReplay(data, offset, length);


                //write payload data to shared memory
                Marshal.Copy(data, 0, this.sharedMemoryHandler.SharedMemoryPointer, data.Length);


            }else if (operationMode == LROperationMode.write)
            {
                SYSTEM_WRITTEN_BLOCK newBlock = new SYSTEM_WRITTEN_BLOCK();
                newBlock.length = length;
                newBlock.offset = offset;

                //copy shared memory to struct
                newBlock.buffer = new byte[length];
                Marshal.Copy(this.sharedMemoryHandler.SharedMemoryPointer, newBlock.buffer, 0, (int)length);

                //copy to list
                this.readableBackupChains[vhdxTargetIndex].systemWrittenBlocks.Add(newBlock);
            }

            //build reply struct
            reply.replyHeader.messageId = dataReceive.messageHeader.messageId;
            reply.replyHeader.status = 0;

            int size = sizeof(FILTER_REPLY_MESSAGE);

            status = FilterReplyMessage(this.kmHandle, ref reply, size);

            //perform a write request
            //if (operationMode == LROperationMode.write)
            //{
            //    //get data from shared memory
            //    Marshal.Copy(this.sharedMemoryHandler.SharedMemoryPointer, data, 0, data.Length);

            //    //write data to writecache
            //    writeCache.writeCacheStream.Seek(0, SeekOrigin.End);
            //    writeCache.writeCacheStream.Write(data, 0, data.Length);
            //    MountHandler.WriteCachePosition newCachePosition = new MountHandler.WriteCachePosition();
            //    newCachePosition.filePosition = (UInt64)writeCache.writeCacheStream.Position;
            //    newCachePosition.length = (UInt64)length;
            //    newCachePosition.offset = (UInt64)offset;
            //    writeCache.positions.Add(newCachePosition);

            //    //build reply struct, to ack message
            //    reply.replyHeader.messageId = dataReceive.messageHeader.messageId;
            //    reply.replyHeader.status = 0;

            //    int size = sizeof(FILTER_REPLY_MESSAGE);

            //    status = FilterReplyMessage(this.kmHandle, ref reply, size);
            //}

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
                //read the requested data from backup chain and from lb
                data = new byte[length];

                //read from first element in chains. On FLR there can only be one chain
                this.readableBackupChains[0].readFromChain(offset, length, data, 0);
                this.readableBackupChains[0].readFromLB(offset, length, data, this.lbTimeLimit);

                //write payload data to shared memory
                Marshal.Copy(data, 0, this.sharedMemoryHandler.SharedMemoryPointer, data.Length);

                //set LogGUID to zero to disable log replay
                //disableLogReplay(data, offset, length);
            }
            else if(requestType == 2) //write buffer received
            {
                SYSTEM_WRITTEN_BLOCK newBlock = new SYSTEM_WRITTEN_BLOCK();
                newBlock.length = length;
                newBlock.offset = offset;

                //copy shared memory to struct
                newBlock.buffer = new byte[length];
                Marshal.Copy(this.sharedMemoryHandler.SharedMemoryPointer, newBlock.buffer, 0, (int)length);

                //copy to list
                this.readableBackupChains[0].systemWrittenBlocks.Add(newBlock);
            }

            reply.replyHeader.messageId = dataReceive.messageHeader.messageId;
            reply.replyHeader.status = 0;


            int size = sizeof(FILTER_REPLY_MESSAGE);

            status = FilterReplyMessage(this.kmHandle, ref reply, size);

        }

        //sets LogGUID to zero to disable log replay
        private void disableLogReplay(byte[] data, Int64 offset, Int64 length)
        {
            //header offset
            Int32 headerOffset = 65536;

            //loop to adjust both headers
            for (int i = 0; i < 2; i++)
            {
                Int32 logGuidOffset = headerOffset + 48;
                if (logGuidOffset >= offset && logGuidOffset < offset + length)
                {
                    Int64 dataOffset = logGuidOffset - offset;
                    Int64 bytesToWrite = (offset + length) - dataOffset;
                    if (bytesToWrite > 16)
                    {
                        bytesToWrite = 16;
                    }

                    //location found, now write zeros
                    byte[] zeroData = new byte[bytesToWrite];
                    Array.Copy(zeroData, 0, data, dataOffset, bytesToWrite);

                    //adjust header checksum
                    Int32 checksumOffset = headerOffset + 4;
                    byte[] headerData = new byte[4096];
                    Array.Copy(data, headerOffset - offset, headerData, 0, 4096);

                    //set crc bytes to zero
                    headerData[4] = 0;
                    headerData[5] = 0;
                    headerData[6] = 0;
                    headerData[7] = 0;

                    //calculate crc32c
                    byte[] crcData = BitConverter.GetBytes(Force.Crc32.Crc32CAlgorithm.Compute(headerData));

                    //copy back crc value
                    Array.Copy(crcData, 0, data, checksumOffset, 4);

                }
                //jump to header 2
                headerOffset *= 2;
            }

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

        //a written block used by flr and lr
        public struct SYSTEM_WRITTEN_BLOCK
        {
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

        //LR operation mode
        public enum LROperationMode
        {
            read, write
        }
    }
}
