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
        private System.IO.FileStream fileStream;
        private AccessMode mode;
        private ulong decompressedBlockSize = 1000000; // = 1MB

        //needed for write
        private ulong totalUncompressedByteCount = 0;
        private ulong decompressedByteCountWithinBlock = 0;


        public LZ4BlockStream(System.IO.FileStream filestream, AccessMode mode)
        {
            this.fileStream = filestream;
            this.mode = mode;

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
            }
        }

        //write header to file and dispose
        public new void Dispose()
        {
            //write "decompressed file size" if in write mode
            if (this.mode == AccessMode.write)
            {
                this.fileStream.Seek(0, SeekOrigin.Begin);
                byte[] buffer = BitConverter.GetBytes(this.totalUncompressedByteCount);
                this.fileStream.Write(buffer, 0, 8);
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

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        //writes te given buffer (uncompressed) to the destination file (compressed)
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
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

