using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
        private ulong decompressedBlockSize = 2000000; // = 2MB

        //need for read
        private ulong position = 0;
        private ulong decompressedFileSize = 0;
        private long decompressedFileSizeOffset = 0;

        //used for block caching
        private ulong maxBlocksInCache = 30;
        List<CacheEntry> cache = new List<CacheEntry>();

        //used for encryption (general)
        private bool useEncryption;
        AesCryptoServiceProvider aesProvider;
        ICryptoTransform encryptor;
        ICryptoTransform decryptor;
        private byte[] aesKey;


        public bool CachingMode { get; set; }

        public LZ4BlockStream(FileStream filestream, AccessMode mode, bool useEncryption, byte[] aesKey)
        {
            this.CachingMode = false;
            this.encoderSettings.CompressionLevel = K4os.Compression.LZ4.LZ4Level.L00_FAST;
            this.fileStream = filestream;
            this.mode = mode;
            this.mStream = new MemoryStream((int)this.DecompressedBlockSize);
            this.useEncryption = useEncryption;
            this.aesKey = aesKey;

        }

        public bool init()
        { 
            //if "write-mode" then create new file and fill the first 2 X 8 header bytes
            if (this.mode == AccessMode.write)
            {
                byte[] signatureBytes = System.Text.Encoding.ASCII.GetBytes("nxmlz4");

                //init crypto provider
                if (this.useEncryption)
                {
                    this.aesProvider = new AesCryptoServiceProvider();
                    this.aesProvider.KeySize = 256;
                    this.aesProvider.Key = aesKey;
                    this.aesProvider.GenerateIV();
                    this.encryptor = this.aesProvider.CreateEncryptor(this.aesProvider.Key, this.aesProvider.IV);
                    MemoryStream memStream = new MemoryStream();

                    CryptoStream cryptoStream = new CryptoStream(memStream, this.encryptor, CryptoStreamMode.Write);

                    //write aes IV
                    this.fileStream.Seek(0, SeekOrigin.Begin);
                    this.fileStream.Write(BitConverter.GetBytes(this.aesProvider.IV.Length), 0, 4); //IV length
                    this.fileStream.Write(this.aesProvider.IV, 0, this.aesProvider.IV.Length); //IV

                    //write signature
                    cryptoStream.Write(signatureBytes, 0, 6);
                    cryptoStream.FlushFinalBlock();

                    memStream.WriteTo(this.fileStream);
                    cryptoStream.Close();

                }
                else
                {
                    //write no IV
                    this.fileStream.Seek(0, SeekOrigin.Begin);
                    this.fileStream.Write(BitConverter.GetBytes(0), 0, 4); //IV length

                    //write signature
                    this.fileStream.Write(signatureBytes, 0, signatureBytes.Length);

                    //signature consists of 16 bytes, write 10 dummy bytes
                    byte[] signatureDummy = new byte[10];
                    this.fileStream.Write(signatureDummy, 0, signatureDummy.Length);
                }


                //we don't know "decompressed file size" yet, fill it with zeroes
                this.decompressedFileSizeOffset = this.fileStream.Position;
                for (int i = 0; i < 8; i++)
                {
                    this.fileStream.WriteByte(0);
                }

                //write  "decompressed block size"
                byte[] blockSizeBytes = BitConverter.GetBytes(this.DecompressedBlockSize);
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

                //read IV length
                this.fileStream.Read(buffer, 0, 4);
                int ivLength = BitConverter.ToInt32(buffer, 0);

                //encryption error?
                if ((ivLength > 0 && !this.useEncryption) || (ivLength == 0 && this.useEncryption))
                {
                    this.fileStream.Close();
                    return false;

                }

                //read iv
                if (ivLength > 0)
                {
                    byte[] iv = new byte[ivLength];
                    this.fileStream.Read(iv, 0, ivLength);

                    //init crypto
                    this.aesProvider = new AesCryptoServiceProvider();
                    this.aesProvider.KeySize = 256;
                    this.aesProvider.Key = aesKey;
                    this.aesProvider.IV = iv;
                    this.decryptor = this.aesProvider.CreateDecryptor(this.aesProvider.Key, this.aesProvider.IV);

                    //read encrypted signature
                    MemoryStream memStream = new MemoryStream();
                    byte[] signature = new byte[16];
                    this.fileStream.Read(signature, 0, 16);
                    memStream.Write(signature, 0, signature.Length);
                    memStream.Seek(0, SeekOrigin.Begin);

                    CryptoStream cryptoStream = new CryptoStream(memStream, this.decryptor, CryptoStreamMode.Read);
                    signature = new byte[16];
                    int readBytes = cryptoStream.Read(signature, 0, 16);
                    string signatureString = System.Text.Encoding.ASCII.GetString(signature, 0, readBytes);
                    this.decompressedFileSizeOffset = this.fileStream.Position;
                    cryptoStream.Close();
                    memStream.Close();
                    cryptoStream.Dispose();

                    //decryption error
                    if (signatureString != "nxmlz4")
                    { 
                        this.fileStream.Close();
                        return false;
                    }

                }
                else
                {
                    //read unencrypted signature
                    byte[] signature = new byte[6];
                    this.fileStream.Read(signature, 0, signature.Length);

                    //signature has 16 bytes, not just 6. Jump another 10 bytes
                    this.fileStream.Seek(10, SeekOrigin.Current);

                    this.decompressedFileSizeOffset = this.fileStream.Position;

                    string signatureString = System.Text.Encoding.ASCII.GetString(signature);

                    //signature ok?
                    if (signatureString != "nxmlz4")
                    {
                        this.fileStream.Close();
                        return false;
                    }
                }

                

                this.fileStream.Read(buffer, 0, 8);
                this.decompressedFileSize = BitConverter.ToUInt64(buffer, 0);

                this.fileStream.Read(buffer, 0, 8);
                this.DecompressedBlockSize = BitConverter.ToUInt64(buffer, 0);

                
            }

            return true;
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

            //reset block values on memory stream
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
            byte[] buffer;
            long compressedBlockSize = 0;
            if (this.useEncryption)
            {
                //aes is 16 byte aligned
                compressedBlockSize = this.mStream.Length;
                compressedBlockSize += 16 -(compressedBlockSize % 16);
                buffer = BitConverter.GetBytes(compressedBlockSize);
            }
            else
            {
                buffer = BitConverter.GetBytes(this.mStream.Length);
            }
            this.fileStream.Write(buffer, 0, 8);

            //go back to file head
            this.fileStream.Seek(0, SeekOrigin.End);

            //write memory stream to file
            this.mStream.Position = 0;

            //write block to file
            if (this.useEncryption)
            {
                using (MemoryStream memStream = new MemoryStream())
                using (CryptoStream cryptoStream = new CryptoStream(memStream, this.encryptor, CryptoStreamMode.Write))
                {
                    this.mStream.WriteTo(cryptoStream);
                    cryptoStream.FlushFinalBlock();
                    memStream.WriteTo(this.fileStream);
                }
            }
            else
            { 
                this.mStream.WriteTo(this.fileStream);
            }
        }

        //write header to file and dispose
        public new void Dispose()
        {
            //write "decompressed file size" if in write mode
            if (this.mode == AccessMode.write)
            {
                closeBlock();
                this.fileStream.Seek(this.decompressedFileSizeOffset, SeekOrigin.Begin);
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
            base.Dispose(true);
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
        public ulong DecompressedBlockSize { get => decompressedBlockSize; set => decompressedBlockSize = value; }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            //search the "starting" block:
            //read current block header
            this.fileStream.Position = 16 + this.decompressedFileSizeOffset; // jump over header
            byte[] headerData = new byte[8];
            ulong decompressedFileByteOffset = 0;
            ulong compressedBlockSize = 0;
            ulong totalUncompressedBytesRead = 0;
            byte[] tempData = new byte[(ulong)count + this.DecompressedBlockSize];
            ulong startDiffOffset = 0;
            MemoryStream destMemoryStream;

            //read first block header
            this.fileStream.Read(headerData, 0, 8);
            decompressedFileByteOffset = BitConverter.ToUInt64(headerData, 0);

            this.fileStream.Read(headerData, 0, 8);
            compressedBlockSize = BitConverter.ToUInt64(headerData, 0);

            while (decompressedFileByteOffset + this.DecompressedBlockSize < (ulong)this.Position)
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
            double blocksToRead = Math.Ceiling((double)count / (double)this.DecompressedBlockSize) + 1;
            destMemoryStream = new MemoryStream((int)(this.DecompressedBlockSize * blocksToRead));

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

                byte[] cacheBlock = null;
                //check whether block exists within cache
                if (this.CachingMode)
                {
                    cacheBlock = getBlockFromCache(decompressedFileByteOffset);
                }

                //within cache?
                if (cacheBlock == null) //cache miss
                {
                    //fill memory stream with compressed block data
                    byte[] compData = new byte[compressedBlockSize];
                    this.fileStream.Read(compData, 0, (int)compressedBlockSize);
                    int readableBytesCount = (int)compressedBlockSize;

                    //decryption necessary?
                    if (this.useEncryption)
                    {
                        using (MemoryStream memStream = new MemoryStream(compData, true))
                        using (CryptoStream cryptoStream = new CryptoStream(memStream, this.decryptor, CryptoStreamMode.Read))
                        {
                            readableBytesCount = cryptoStream.Read(compData, 0, compData.Length);
                        }

                    }

                    this.mStream.Dispose();
                    this.mStream = new MemoryStream(compData, 0, readableBytesCount);

                    //init decompression stream
                    this.decompressionStream = LZ4Stream.Decode(this.mStream, 0, false);


                    byte[] destBuffer = new byte[4096];
                    int dataRead = -1;

                    //read one block
                    int blockUncompressedBytesRead = 0;
                    cacheBlock = new byte[this.decompressedBlockSize];
                    while (dataRead != 0)
                    {
                        dataRead = this.decompressionStream.Read(destBuffer, 0, destBuffer.Length);
                        destMemoryStream.Write(destBuffer, 0, (int)dataRead);
                        totalUncompressedBytesRead += (ulong)dataRead;

                        //add data to cache block
                        destMemoryStream.Seek(dataRead * -1, SeekOrigin.Current);
                        destMemoryStream.Read(cacheBlock, blockUncompressedBytesRead, dataRead);

                        blockUncompressedBytesRead += dataRead;

                    }

                    if (this.CachingMode)
                    {
                        addBlockToCache(decompressedFileByteOffset, compressedBlockSize, cacheBlock);
                    }
                    
                    this.decompressionStream.Close();
                }
                else //cache match
                {
                    destMemoryStream.Write(cacheBlock, 0, cacheBlock.Length);

                    //jump to next block
                    this.fileStream.Seek((long)compressedBlockSize, SeekOrigin.Current);
                }
            }

            destMemoryStream.Position = (long)(startDiffOffset);
            

            //write bytes to buffer[] and return read bytes
            int bytesdecompressed = destMemoryStream.Read(buffer, offset, count);
            this.Position += (long)bytesdecompressed;
            return bytesdecompressed;
            
        }

        //adds a new block to cache
        private void addBlockToCache(ulong decompressedFileByteOffset, ulong compressedBlockSize, byte[] data)
        {
            CacheEntry newEntry = new CacheEntry();
            newEntry.decompressedBlockData = data;

            BlockHeader newBlockHeader = new BlockHeader();
            newBlockHeader.compressedBlockSize = compressedBlockSize;
            newBlockHeader.decompressedFileByteOffset = decompressedFileByteOffset;

            newEntry.blockHeader = newBlockHeader;

            this.cache.Insert(0, newEntry);

            //check if cache exceeds size limit
            if ((ulong)cache.Count > this.maxBlocksInCache)
            {
                this.cache.RemoveAt(this.cache.Count - 1);
            }
        }

        //checks whether given block is within cache and returns the decompressed block
        private byte[] getBlockFromCache(ulong decompressedFileByteOffset)
        {
            byte[] decompressedBlock = null;
            //iterate through all cache blocks
            for (int i = 0; i < this.cache.Count; i++)
            {
                CacheEntry entry = this.cache[i];
                if (entry.blockHeader.decompressedFileByteOffset == decompressedFileByteOffset)
                {
                    //cache match
                    decompressedBlock = entry.decompressedBlockData;

                    //move current cache block to top
                    this.cache.RemoveAt(i);
                    this.cache.Insert(0, entry);
                    break;
                }
            }

            return decompressedBlock;
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
                if (this.DecompressedBlockSize - this.decompressedByteCountWithinBlock > (ulong)count)
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
                    ulong bytesRemainingWithinBlock = this.DecompressedBlockSize - this.decompressedByteCountWithinBlock;
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

    //one cache entry
    public struct CacheEntry
    {
        public BlockHeader blockHeader;
        public byte[] decompressedBlockData;
    }

    //block header for one cache entry
    public struct BlockHeader
    {
        public ulong decompressedFileByteOffset;
        public ulong compressedBlockSize;
    }
}

//Block Compression Fileformat:
//----------header----------
//4 bytes: aes IV length
//IV length bytes: aes IV
//16 bytes: signature string "nxmlz4" (16 bytes aligned, because of AES blocksize)
//
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

