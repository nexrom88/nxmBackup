using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using K4os.Compression.LZ4.Streams;

namespace BlockCompression
{
    public class LZ4BlockStream : System.IO.Stream, IDisposable
    {
        MemoryStream mStream;
        LZ4EncoderStream compressionStream;
        LZ4DecoderStream decompressionStream;

        private System.IO.FileStream fileStream;
        private AccessMode mode;


        //needed for write
        private LZ4EncoderSettings encoderSettings = new LZ4EncoderSettings();
        private ulong totalDecompressedByteCount = 0;
        private ulong decompressedByteCountWithinBlock = 0;
        private ulong decompressedBlockSize = 5000000; // = 5MB

        //need for read
        private ulong position = 0;
        private ulong decompressedFileSize = 0;



        public LZ4BlockStream(System.IO.FileStream filestream, AccessMode mode)
        {
            this.encoderSettings.CompressionLevel = K4os.Compression.LZ4.LZ4Level.L00_FAST;
            this.fileStream = filestream;
            this.mode = mode;
            this.mStream = new MemoryStream((int)this.decompressedBlockSize);

            //if "write-mode" then create new file and fill the first 2 X 8 header bytes
            if (this.mode == AccessMode.write)
            {
                this.fileStream.Seek(0, SeekOrigin.Begin);

                //we don't know "decompressed file size" yet, fill it with zeroes
                for (int i = 0; i < 8; i++)
                {
                    this.fileStream.WriteByte(0);
                }

                //write  "decompressed block size"
                byte[] blockSizeBytes = BitConverter.GetBytes(this.decompressedBlockSize);
                this.fileStream.Write(blockSizeBytes, 0, 8);

                //write first block dummies
                for (int i = 0; i < 16; i++)
                {
                    this.fileStream.WriteByte(0);
                }

                //open compression stream
                this.compressionStream = LZ4Stream.Encode(this.mStream, this.encoderSettings, true);

            }else if (this.mode == AccessMode.read) //if read mode: read file header
            {
                byte[] buffer = new byte[8];
                this.fileStream.Read(buffer, 0, 8);
                this.decompressedFileSize = BitConverter.ToUInt64(buffer, 0);

                this.fileStream.Read(buffer, 0, 8);
                this.decompressedBlockSize = BitConverter.ToUInt64(buffer, 0);

                
            }
        }

        //starts new block and closes the old one
        private void startNewBlock()
        {
            closeBlock();

            //write "decompressed file byte offset" for the new block
            byte[] buffer = BitConverter.GetBytes(this.totalDecompressedByteCount);
            this.fileStream.Write(buffer, 0, 8);

            //write dummy for "compressed block size"
            for (int i = 0; i < 8; i++)
            {
                this.fileStream.WriteByte(0);
            }

            //reset block values an memory stream
            this.decompressedByteCountWithinBlock = 0;
            this.mStream = new MemoryStream();
            this.compressionStream = LZ4Stream.Encode(this.mStream, this.encoderSettings, true);
        }

        //closes the current block
        private void closeBlock()
        {
            //close compression stream 
            compressionStream.Close();

            //is this block empty? Then remove the new block header
            if (this.decompressedByteCountWithinBlock == 0)
            {
                this.fileStream.SetLength (this.fileStream.Length - 16);
                return;
            }


            //close block by filling the 8 byte block header (compressed block size)

            //go back 8 bytes
            this.fileStream.Seek(-8, SeekOrigin.Current);

            //write "compressed block size"
            byte[] buffer = BitConverter.GetBytes(this.mStream.Length);
            this.fileStream.Write(buffer, 0, 8);

            //go back to file head
            this.fileStream.Seek(0, SeekOrigin.End);

            //write memory stream to file
            this.mStream.Position = 0;
            this.mStream.CopyTo(this.fileStream);
        }

        //write header to file and dispose
        public new void Dispose()
        {
            //write "decompressed file size" if in write mode
            if (this.mode == AccessMode.write)
            {
                closeBlock();
                this.fileStream.Seek(0, SeekOrigin.Begin);
                byte[] buffer = BitConverter.GetBytes(this.totalDecompressedByteCount);
                this.fileStream.Write(buffer, 0, 8);
            }
            else if (this.mode == AccessMode.read) // read mode
            {
                if (this.decompressionStream != null)
                {
                    this.decompressionStream.Close();
                }
            }
            this.fileStream.Close();
            Dispose(true);
        }

        //closes the file
        public new void Close()
        {
            this.Dispose();
        }



        //returns whether the stream is readable or not
        public override bool CanRead
        {
            get => this.mode == AccessMode.read;
        }

            
        //returns whether the stream is seekable or not (always true)
        public override bool CanSeek => true;

        //returns whether the stream is readable or not
        public override bool CanWrite
        {
            get => this.mode == AccessMode.write;
        }

        public override long Length => throw new NotImplementedException();

        public override long Position { get => (long)this.position; set => this.position = (ulong)value; }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            //search the "starting" block:
            //read current block header
            this.fileStream.Position = 16;
            byte[] headerData = new byte[8];
            ulong decompressedFileByteOffset = 0;
            ulong compressedBlockSize = 0;
            ulong totalUncompressedBytesRead = 0;
            byte[] tempData = new byte[(ulong)count + this.decompressedBlockSize];
            ulong startDiffOffset = 0;
            MemoryStream destMemoryStream;

            //read first block header
            this.fileStream.Read(headerData, 0, 8);
            decompressedFileByteOffset = BitConverter.ToUInt64(headerData, 0);

            this.fileStream.Read(headerData, 0, 8);
            compressedBlockSize = BitConverter.ToUInt64(headerData, 0);

            while (decompressedFileByteOffset + this.decompressedBlockSize < (ulong)this.Position)
            {
                
                //jump to next block
                this.fileStream.Seek((long)compressedBlockSize, SeekOrigin.Current);

                //no forward seek possible? eof!
                if (this.fileStream.Read(headerData, 0, 8) == 0)
                {
                    return 0; //eof
                }
                decompressedFileByteOffset = BitConverter.ToUInt64(headerData, 0);

                if (this.fileStream.Read(headerData, 0, 8) == 0)
                {
                    return 0;
                }
                compressedBlockSize = BitConverter.ToUInt64(headerData, 0);
            }

            //start block found

            startDiffOffset = (ulong)this.Position - decompressedFileByteOffset;

            //header must be read again within "for". Jump back
            this.fileStream.Seek(-16, SeekOrigin.Current);

            //"starting" block found:

            //decompress block

            //read all necessary blocks 
            double blocksToRead = Math.Ceiling((double)count / (double)this.decompressedBlockSize) + 1;
            destMemoryStream = new MemoryStream((int)(this.decompressedBlockSize * blocksToRead));

            for (double i = 0; i< blocksToRead ; i++)
            {
                //read complete block here within for:

                //check if this is the block "after the last block (not readable)"
                if (this.fileStream.Position == this.fileStream.Length)
                {
                    continue;
                }

                //read header
                this.fileStream.Read(headerData, 0, 8);
                decompressedFileByteOffset = BitConverter.ToUInt64(headerData, 0);

                this.fileStream.Read(headerData, 0, 8);
                compressedBlockSize = BitConverter.ToUInt64(headerData, 0);


                //fill memory stream with compressed block data
                byte[] compData = new byte[compressedBlockSize];
                this.fileStream.Read(compData, 0, (int)compressedBlockSize);
                this.mStream.Dispose();
                this.mStream = new MemoryStream(compData);

                //init decompression stream
                this.decompressionStream = LZ4Stream.Decode(this.mStream, 0, false);

   
                byte[] destBuffer = new byte[4096];
                long dataRead = -1;

                //read one block
                while (dataRead != 0)
                {
                    dataRead = (long)this.decompressionStream.Read(destBuffer, 0, destBuffer.Length);
                    destMemoryStream.Write(destBuffer, 0, (int)dataRead);
                    totalUncompressedBytesRead += (ulong)dataRead;
                }

                this.decompressionStream.Close();

            }

            this.Position += (long)count;

            destMemoryStream.Position = (long)(startDiffOffset);
            

            //write bytes to buffer[] and return read bytes
            return destMemoryStream.Read(buffer, offset, count);
            
        }

        //set the current position within the compressed file
        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    this.position = (ulong)offset;
                    break;
                case SeekOrigin.Current:
                    this.position += (ulong)offset;
                    break;
                case SeekOrigin.End:
                    this.position = this.decompressedFileSize + (ulong)offset;
                    break;
            }

            return (long)this.position;
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        //writes the given buffer (uncompressed) to the destination file (compressed)
        public override void Write(byte[] buffer, int offset, int count)
        {
            
            
            //just write when in "write mode"
            if (this.mode != AccessMode.write)
            {
                return;
            }

            //write bytes/blocks until count == 0
            while (count > 0)
            {
                
                //can write all bytes at once?
                if (this.decompressedBlockSize - this.decompressedByteCountWithinBlock > (ulong)count)
                {
                    compressionStream.Write(buffer, offset, count);
                    compressionStream.Flush();
                    offset += count;
                    this.decompressedByteCountWithinBlock += (ulong)count;
                    this.totalDecompressedByteCount += (ulong)count;
                    count = 0;
                }
                else
                {
                    //remaining bytes are more than remaining bytes within block
                    ulong bytesRemainingWithinBlock = this.decompressedBlockSize - this.decompressedByteCountWithinBlock;
                    compressionStream.Write(buffer, offset, (int)bytesRemainingWithinBlock);
                    offset += (int)bytesRemainingWithinBlock;
                    count -= (int)bytesRemainingWithinBlock;
                    this.decompressedByteCountWithinBlock += bytesRemainingWithinBlock;
                    this.totalDecompressedByteCount += bytesRemainingWithinBlock;

                    //close block and start a new one
                    startNewBlock();
                }
            }

        }
    }

    public enum AccessMode
    {
        write, read
    }
}

//Block Compression Fileformat:
//----------header----------
//8 bytes: decompressed file size in bytes
//8 bytes: decompressed block size in bytes
//
//----------body blocks----------
//8 bytes: decompressed file byte offset
//8 bytes: compressed block size (cbs) in bytes
//cbs bytes: compressed data
//
//----------next block----------
//...

