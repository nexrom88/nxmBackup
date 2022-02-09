﻿using System;
using System.Collections;
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
        //LZ4EncoderStream compressionStream;
        LZ4DecoderStream decompressionStream;

        private System.IO.FileStream fileStream;
        private AccessMode mode;

        //needed for dedupe
        private bool usingDedupe;
        private Dictionary<SHA1Struct, Int64> blocksDict;

        //needed for write
        private LZ4EncoderSettings encoderSettings = new LZ4EncoderSettings();
        private ulong totalDecompressedByteCount = 0;
        private ulong decompressedByteCountWithinBlock = 0;
        private ulong decompressedBlockSize = 500000; // = 500kb
        private MemoryStream structBuffer;

        //for statistics
        public UInt64 TotalCompressedBytesWritten { get; set; }

        //need for read
        private ulong position = 0;
        private ulong decompressedFileSize = 0;
        private long decompressedFileSizeOffset = 0;
        private UInt64 structOffset = 0;
        private UInt64 structLength = 0;

        //used for block caching
        private ulong maxBlocksInCache = 30;
        List<CacheEntry> cache = new List<CacheEntry>();

        //used for struct cache (for read mode only)
        private List<StructCacheEntry> structCache;

        //used for encryption
        private bool useEncryption;
        AesCryptoServiceProvider aesProvider;
        ICryptoTransform encryptor;
        ICryptoTransform decryptor;
        private byte[] aesKey;


        public bool CachingMode { get; set; }

        public LZ4BlockStream(FileStream filestream, AccessMode mode, bool useEncryption, byte[] aesKey, bool usingDedupe)
        {
            this.CachingMode = false;
            this.encoderSettings.CompressionLevel = K4os.Compression.LZ4.LZ4Level.L00_FAST;
            this.fileStream = filestream;
            this.mode = mode;
            this.mStream = new MemoryStream((int)this.DecompressedBlockSize);
            this.useEncryption = useEncryption;
            this.aesKey = aesKey;
            this.usingDedupe = usingDedupe; //only necessary when within write mode


            //init dedupe dict if necessary
            if (this.usingDedupe)
            {
                this.blocksDict = new Dictionary<SHA1Struct, long>();
            }
        }


        public bool init()
        {
            //if "write-mode" then create new file and fill the first 2 X 8 header bytes
            if (this.mode == AccessMode.write)
            {
                byte[] signatureBytes = System.Text.Encoding.ASCII.GetBytes("nxmlz4");
                this.structBuffer = new MemoryStream();

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

                //write dedupe switch
                this.fileStream.WriteByte(this.usingDedupe ? (byte)1 : (byte)0);

                //write dummies for struct length and offset
                byte[] dummyBytes = new byte[16];
                this.fileStream.Write(dummyBytes, 0, dummyBytes.Length);

                //write dummy header for first block to mem stream
                dummyBytes = new byte[16];
                this.structBuffer.Write(dummyBytes, 0, 16);


            }
            else if (this.mode == AccessMode.read) //if read mode: read file header
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

                //read dedupe switch
                this.usingDedupe = this.fileStream.ReadByte() == 1 ? true : false;

                //read struct offset
                this.fileStream.Read(buffer, 0, 8);
                this.structOffset = BitConverter.ToUInt64(buffer, 0);

                //read struct length
                this.fileStream.Read(buffer, 0, 8);
                this.structLength = BitConverter.ToUInt64(buffer, 0);

                //build struct cache
                buildStructCache();

            }

            return true;
        }

        //builds the struct cache
        private void buildStructCache()
        {
            bool blockDeduped = false;
            this.structCache = new List<StructCacheEntry>();

            //jump to struct offset
            this.fileStream.Seek((Int64)this.structOffset, SeekOrigin.Begin);

            //read whole struct to memory stream
            byte[] rawStruct = new byte[this.structLength];
            this.fileStream.Read(rawStruct, 0, (int)this.structLength);
            MemoryStream structStream = new MemoryStream(rawStruct);

            byte[] buffer = new byte[24];

            while (structStream.Position < structStream.Length)
            {
                //build cache entry
                StructCacheEntry cacheEntry = new StructCacheEntry();
                structStream.Read(buffer, 0, 24);

                cacheEntry.decompressedFileByteOffset = BitConverter.ToUInt64(buffer, 0);
                cacheEntry.compressedBlockSize = BitConverter.ToUInt64(buffer, 8);

                if (this.usingDedupe)
                {
                    //read payloadDataOffset
                    UInt64 payloadDataOffset = BitConverter.ToUInt64(buffer, 16);
                    cacheEntry.fileOffset = payloadDataOffset > 0 ? payloadDataOffset : (ulong)this.fileStream.Position;
                    blockDeduped = payloadDataOffset > 0 ? true : false;
                }
                else
                {
                    cacheEntry.fileOffset = (ulong)this.fileStream.Position;
                }

                //add entry to cache
                this.structCache.Add(cacheEntry);

            }

        }

        //starts new block and closes the old one
        private void startNewBlock()
        {
            closeBlock();

            //write "decompressed file byte offset" for the new block
            byte[] buffer = BitConverter.GetBytes(this.totalDecompressedByteCount);
            this.structBuffer.Write(buffer, 0, 8);

            //write dummy for "compressed block size"
            for (int i = 0; i < 8; i++)
            {
                this.structBuffer.WriteByte(0);
            }

            //reset block values on memory stream
            this.decompressedByteCountWithinBlock = 0;
            this.mStream.Close();
            this.mStream.Dispose();
            this.mStream = new MemoryStream((int)this.decompressedBlockSize);
        }

        //closes the current block
        private void closeBlock()
        {

            //is this block empty? Do nothing
            if (this.decompressedByteCountWithinBlock == 0)
            {
                return;
            }


            //write memory stream to file
            this.mStream.Position = 0;


            //write block to file
            if (this.useEncryption)
            {

                //check dedupe dict if necessary
                if (this.usingDedupe)
                {
                    Int64 dedupeStructOffset = getOffsetFromDeDupeDict(this.mStream, this.structBuffer.Position +8);
                    if (dedupeStructOffset >= 0) //dedupe entry found
                    {
                        //read deduped struct block
                        this.structBuffer.Seek(dedupeStructOffset, SeekOrigin.Begin);
                        byte[] structBlockBuffer = new byte[24];
                        this.structBuffer.Read(structBlockBuffer, 0, 24);

                        //jump back to structBuffer end
                        this.structBuffer.Seek(0, SeekOrigin.End);


                        //jump and write to cbs from current block
                        this.structBuffer.Seek(-8, SeekOrigin.End);
                        this.structBuffer.Write(structBlockBuffer, 8, 8);

                        //write payload data offset
                        this.structBuffer.Write(structBlockBuffer, 16, 8);

                    }
                    else //dedupe entry not found, write bytes to current stream
                    {
                        //write payloaddataoffset == current position within filestream
                        byte[] offsetBuffer = BitConverter.GetBytes(this.fileStream.Position);
                        this.structBuffer.Write(offsetBuffer, 0, 8);

                        //write payload data:
                        writeToStorage(this.mStream, this.fileStream, this.useEncryption);

                    }

                }
                else //dedupe is disabled
                {
                    //write payloaddataoffset == 0
                    byte[] offsetBuffer = BitConverter.GetBytes(this.fileStream.Position);
                    this.structBuffer.Write(offsetBuffer, 0, 8);

                    //write payload data:
                    writeToStorage(this.mStream, this.fileStream, this.useEncryption);
                }

            }
            else //encryption disabled
            {
                //check dedupe dict if necessary
                if (this.usingDedupe)
                {
                    Int64 dedupeStructOffset = getOffsetFromDeDupeDict(this.mStream, this.structBuffer.Position +8);
                    if (dedupeStructOffset >= 0) //dedupe entry found
                    {
                        //read deduped struct block
                        this.structBuffer.Seek(dedupeStructOffset, SeekOrigin.Begin);
                        byte[] structBlockBuffer = new byte[24];
                        this.structBuffer.Read(structBlockBuffer, 0, 24);

                        //jump back to structBuffer end
                        this.structBuffer.Seek(0, SeekOrigin.End);


                        //jump and write to cbs from current block
                        this.structBuffer.Seek(-8, SeekOrigin.End);
                        this.structBuffer.Write(structBlockBuffer, 8, 8);

                        //write payload data offset
                        this.structBuffer.Write(structBlockBuffer, 16, 8);
                    }
                    else //dedupe entry not found, write bytes to current stream
                    {
                        //write payloaddataoffset == current position within filestream
                        byte[] offsetBuffer = BitConverter.GetBytes(this.fileStream.Position);
                        this.structBuffer.Write(offsetBuffer, 0, 8);

                        //write payload data
                        writeToStorage(this.mStream, this.fileStream, this.useEncryption);
                    }

                }
                else //dedupe is disabled
                {
                    //write payloaddataoffset == 0
                    byte[] offsetBuffer = BitConverter.GetBytes(this.fileStream.Position);
                    this.structBuffer.Write(offsetBuffer, 0, 8);

                    //write payload data
                    writeToStorage(this.mStream, this.fileStream, this.useEncryption);
                }
            }
        }

        //writes the given payload data to storage
        private void writeToStorage(MemoryStream source, FileStream target, bool useEncryption)
        {
            using (MemoryStream compMemStream = new MemoryStream())
            using (LZ4EncoderStream compressionStream = LZ4Stream.Encode(compMemStream, this.encoderSettings, true))
            {
                MemoryStream cryptoMemStream = null;
                CryptoStream cryptoStream = null;
                //do compression
                source.WriteTo(compressionStream);
                compressionStream.Close();

                //add compressed bytes count to var for statistics
                TotalCompressedBytesWritten += (UInt64)compMemStream.Length;

                //write compressed block size to block header
                byte[] buffer;
                long compressedBlockSize = 0;
                if (useEncryption)
                {
                    //init enryptor module
                    cryptoMemStream = new MemoryStream();
                    cryptoStream = new CryptoStream(cryptoMemStream, this.encryptor, CryptoStreamMode.Write);

                    //aes is 16 byte aligned
                    compressedBlockSize = compMemStream.Length;
                    compressedBlockSize += 16 - (compressedBlockSize % 16);
                    buffer = BitConverter.GetBytes(compressedBlockSize);
                }
                else
                {
                    buffer = BitConverter.GetBytes(compMemStream.Length);
                }
                this.structBuffer.Seek(-16, SeekOrigin.Current); //jump back 16 bytes to find right position for cbs
                this.structBuffer.Write(buffer, 0, 8);
                this.structBuffer.Seek(8, SeekOrigin.Current); //jump forward to point to end again

                if (useEncryption)
                {
                    //encryption
                    compMemStream.WriteTo(cryptoStream);
                    cryptoStream.FlushFinalBlock();

                    //write to storage
                    cryptoMemStream.WriteTo(target);

                    //close encryptor module
                    cryptoStream.Dispose();
                    cryptoMemStream.Dispose();
                }
                else
                {
                    //write to storage without encryption
                    compMemStream.WriteTo(target);
                }
            }
        }

        //can block be found within dedupe table? currentFileOffset is for adding a new dedupe entry
        private Int64 getOffsetFromDeDupeDict(MemoryStream memStream, Int64 currentStructOffset)
        {

            //compute md5 hash
            byte[] hash = Common.SHA1Provider.computeHash(memStream.ToArray());
            SHA1Struct sha1Struct = new SHA1Struct();
            sha1Struct.block1 = BitConverter.ToUInt64(hash, 0);
            sha1Struct.block2 = BitConverter.ToUInt64(hash, 8);
            sha1Struct.block3 = BitConverter.ToUInt32(hash, 16);

            //check dict for hash
            Int64 foundOffset;
            if (this.blocksDict.TryGetValue(sha1Struct, out foundOffset))
            {
                //entry found, return file offset
                return foundOffset;
            }
            else
            {
                //entry not found, add it to dict
                this.blocksDict.Add(sha1Struct, currentStructOffset);
                return -1;
            }
        }


        //write header to file and dispose
        public new void Dispose()
        {
            //write "decompressed file size" if in write mode
            if (this.mode == AccessMode.write)
            {
                closeBlock();
                
                //write decompressed file size
                this.fileStream.Seek(this.decompressedFileSizeOffset, SeekOrigin.Begin);
                byte[] buffer = BitConverter.GetBytes(this.totalDecompressedByteCount);
                this.fileStream.Write(buffer, 0, 8);

                //write struct offset and length
                this.fileStream.Seek(9, SeekOrigin.Current);

                buffer = BitConverter.GetBytes(this.fileStream.Length); //offset
                this.fileStream.Write(buffer, 0, 8);

                buffer = BitConverter.GetBytes(this.structBuffer.Length); //length
                this.fileStream.Write(buffer, 0, 8);

                //now write struct itself to eof
                this.fileStream.Seek(0, SeekOrigin.End);
                this.structBuffer.WriteTo(this.fileStream);
                this.structBuffer.Close();


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

        public override long Length
        {
            get => (long)this.decompressedFileSize;
        }

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
            byte[] headerData = new byte[8];
            ulong decompressedFileByteOffset = 0;
            ulong fileOffset = 0;
            ulong compressedBlockSize = 0;
            ulong totalUncompressedBytesRead = 0;
            ulong startDiffOffset = 0;
            ulong structCacheEntryOffset = 0;
            MemoryStream destMemoryStream;

            bool blockFound = false;
            foreach (StructCacheEntry entry in this.structCache)
            {
                decompressedFileByteOffset = entry.decompressedFileByteOffset;
                compressedBlockSize = entry.compressedBlockSize;

                //block found?
                if (decompressedFileByteOffset + this.DecompressedBlockSize > (ulong)this.Position)
                {
                    fileOffset = entry.fileOffset;
                    blockFound = true;
                    break;
                }
                else
                {
                    structCacheEntryOffset++;
                }

            }

            //block not found?
            if (!blockFound)
            {
                return 0;
            }

            //start block found
            startDiffOffset = (ulong)this.Position - decompressedFileByteOffset;


            //"starting" block found:


            //read all necessary blocks 
            double blocksToRead = Math.Ceiling((double)count / (double)this.DecompressedBlockSize) + 1;
            destMemoryStream = new MemoryStream((int)(this.DecompressedBlockSize * blocksToRead));

            for (double i = 0; i < blocksToRead; i++)
            {
                //read complete block here within for:

                //check if this is the block "after the last block (not readable)"
                if (structCacheEntryOffset + 1 > (ulong)this.structCache.Count)
                {
                    continue;
                }

                //get header from struct cache
                decompressedFileByteOffset = structCache[(int)structCacheEntryOffset].decompressedFileByteOffset;
                compressedBlockSize = structCache[(int)structCacheEntryOffset].compressedBlockSize;

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
                    this.fileStream.Seek((long)(this.structCache[(int)structCacheEntryOffset].fileOffset), SeekOrigin.Begin);
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

                    //init cache block if necessarry
                    if (this.CachingMode)
                    {
                        cacheBlock = new byte[this.decompressedBlockSize];
                    }

                    while (dataRead != 0)
                    {
                        dataRead = this.decompressionStream.Read(destBuffer, 0, destBuffer.Length);
                        destMemoryStream.Write(destBuffer, 0, (int)dataRead);
                        totalUncompressedBytesRead += (ulong)dataRead;

                        //add data to cache block
                        if (this.CachingMode)
                        {
                            destMemoryStream.Seek(dataRead * -1, SeekOrigin.Current);
                            destMemoryStream.Read(cacheBlock, blockUncompressedBytesRead, dataRead);
                        }

                        blockUncompressedBytesRead += dataRead;

                    }

                    if (this.CachingMode)
                    {
                        addBlockToCache(decompressedFileByteOffset, compressedBlockSize, cacheBlock);
                    }

                    //jump to next block
                    structCacheEntryOffset++;

                    this.decompressionStream.Close();
                }
                else //cache match
                {
                    destMemoryStream.Write(cacheBlock, 0, cacheBlock.Length);

                    //jump to next block
                    structCacheEntryOffset++;
                }
            }

            destMemoryStream.Position = (long)(startDiffOffset);


            //write bytes to buffer[] and return read bytes
            int bytesdecompressed = destMemoryStream.Read(buffer, offset, count);


            //could not read all necessary bytes?
            //if (bytesdecompressed < count && bytesdecompressed > 0)
            //{
            //    //read remainign bytes
            //    Read(buffer, offset + bytesdecompressed, count - bytesdecompressed);
            //}


            destMemoryStream.Dispose();
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
                    this.mStream.Write(buffer, offset, count);
                    this.mStream.Flush();
                    offset += count;
                    this.decompressedByteCountWithinBlock += (ulong)count;
                    this.totalDecompressedByteCount += (ulong)count;
                    count = 0;
                }
                else
                {
                    //remaining bytes are more than remaining bytes within block
                    ulong bytesRemainingWithinBlock = this.DecompressedBlockSize - this.decompressedByteCountWithinBlock;
                    this.mStream.Write(buffer, offset, (int)bytesRemainingWithinBlock);
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

    //represents a SHA1 has with 20bytes to be better comparable
    public struct SHA1Struct
    {
        public UInt64 block1;
        public UInt64 block2;
        public UInt32 block3;
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


    //one struct cache entry
    public struct StructCacheEntry
    {
        public UInt64 fileOffset;
        public UInt64 decompressedFileByteOffset;
        public UInt64 compressedBlockSize;
    }
}

//Block Compression Fileformat:
// see spec file

