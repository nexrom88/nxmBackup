﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using K4os.Compression.LZ4.Streams;
using nxmBackup.Language;

namespace Common
{
    public class LZ4Archive : IArchive
    {
        private string path;
        private Common.EventHandler eventHandler;
        private const int NO_RELATED_EVENT = -1;
        private bool useEncryption;
        private byte[] aesKey;
        private bool usingDedupe;
        private StopRequestWrapper stopRequest;


        public LZ4Archive(string path, Common.EventHandler eventHandler, bool useEncryption, byte[] aesKey, bool usingDedupe, StopRequestWrapper stopRequest)
        {
            this.path = path;
            this.eventHandler = eventHandler;
            this.useEncryption = useEncryption;
            this.aesKey = aesKey;
            this.usingDedupe = usingDedupe;

            if (stopRequest != null) {
                this.stopRequest = stopRequest;
            }
            else
            {
                this.stopRequest = new StopRequestWrapper();
            }
        }

        //adds a whole folder to the archive
        public void addDirectory(string folder)
        {
            //list all files
            string[] entries = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);

            //iterate through all files
            foreach (string file in entries)
            {
                //build path
                string zipFile = file.Substring(folder.Length + 1).Replace("\\", "/");
                string archivePath = "";
                if (zipFile.Contains("/"))
                {
                    archivePath = zipFile.Substring(0, zipFile.LastIndexOf("/"));
                }

                //add file to archive
                addFile(file, archivePath);
            }
        }

        //adds a given file to the archive
        public TransferDetails addFile(string file, string path)
        {
            bool errorOccured = false;
            TransferDetails transferDetails = new TransferDetails();

            path = path.Replace("/", "\\");
            string fileName = Path.GetFileName(file);

            bool calculateRates = false;
            if (fileName.EndsWith("vhdx") || file.EndsWith("vhd"))
            {
                calculateRates = true;
            }

            //get base io Streams
            System.IO.FileStream baseSourceStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            try
            {
                //create dest path
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(this.path, path));
            }catch(Exception ex)
            {
                Common.DBQueries.addLog("could not create directory", Environment.StackTrace, ex);
                transferDetails.successful = false;
                return transferDetails;
            }
            
            //get dest stream
            System.IO.FileStream baseDestStream = new FileStream(System.IO.Path.Combine(this.path, path + "\\" + fileName) , FileMode.Create);

            //open LZ4 stream
            BlockCompression.LZ4BlockStream compressionStream = new BlockCompression.LZ4BlockStream(baseDestStream, BlockCompression.AccessMode.write, this.useEncryption, this.aesKey, this.usingDedupe);

            if (!compressionStream.init())
            {
                transferDetails.successful = false;
                return transferDetails;
            }

            //create buffer and read counter
            byte[] buffer = new byte[4096];
            long bytesRemaining = baseSourceStream.Length;
            int lastPercentage = -1;

            int relatedEventId = -1;
            if (this.eventHandler != null)
            {
                relatedEventId = this.eventHandler.raiseNewEvent(LanguageHandler.getString("reading") + " " + fileName + " - 0%", false, false, NO_RELATED_EVENT, EventStatus.inProgress);
            }

            Int64 lastTotalByteTransferCounter = 0;
            Int64 lastTransferTimestamp = System.DateTimeOffset.Now.ToUnixTimeMilliseconds();
            Int64 transferRate = 0;
            Int64 byteProcessCounter = 0;
            Int64 processRate = 0;

            while (bytesRemaining > 0 && !this.stopRequest.value)//still bytes to read?
            {
                if (bytesRemaining >= buffer.Length) //still a whole block to read?
                {
                    baseSourceStream.Read(buffer, 0, buffer.Length);
                    
                    try
                    {
                        compressionStream.Write(buffer, 0, buffer.Length);
                    }catch(Exception ex)
                    {
                        this.eventHandler.raiseNewEvent(LanguageHandler.getString("destination_writing_failed"), false, false, NO_RELATED_EVENT, EventStatus.error);
                        errorOccured = true;
                        break;
                    }

                    bytesRemaining -= buffer.Length;
                }
                else //eof -> read the last smaller block
                {
                    buffer = new byte[bytesRemaining];
                    baseSourceStream.Read(buffer, 0, buffer.Length);

                    try
                    {
                        compressionStream.Write(buffer, 0, buffer.Length);
                    }catch(Exception ex)
                    {
                        this.eventHandler.raiseNewEvent(LanguageHandler.getString("destination_writing_failed"), false, false, NO_RELATED_EVENT, EventStatus.error);
                        errorOccured = true;
                        break;
                    }

                    bytesRemaining -= buffer.Length;
                }

                //add read bytes to statistics counter
                transferDetails.bytesProcessed += (UInt64)buffer.Length;

                if (calculateRates)
                {
                    //rate calculations
                    byteProcessCounter += buffer.Length;
                    if (System.DateTimeOffset.Now.ToUnixTimeMilliseconds() - 1000 >= lastTransferTimestamp)
                    {
                        transferRate = (Int64)compressionStream.TotalCompressedBytesWritten - lastTotalByteTransferCounter;
                        lastTotalByteTransferCounter = (Int64)compressionStream.TotalCompressedBytesWritten;

                        processRate = byteProcessCounter;

                        byteProcessCounter = 0;

                        lastTransferTimestamp = System.DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    }
                }
                else
                {
                    transferRate = -1;
                    processRate = -1;
                }

                //calculate progress
                float percentage = (((float)baseSourceStream.Length - (float)bytesRemaining) / (float)baseSourceStream.Length) * 100.0f;

                //progress changed?
                if (lastPercentage != (int)percentage && this.eventHandler != null)
                {
                    this.eventHandler.raiseNewEvent(LanguageHandler.getString("reading") + " " + fileName + " - " + (int)percentage + "%", transferRate, processRate, false, true, relatedEventId, EventStatus.inProgress);
                    lastPercentage = (int)percentage;
                }

            }

            //add transfered bytes to statistics counter
            transferDetails.bytesTransfered = compressionStream.TotalCompressedBytesWritten;

            //transfer completed
            if (this.eventHandler != null)
            {
                if (this.stopRequest.value)
                {
                    this.eventHandler.raiseNewEvent(LanguageHandler.getString("reading") + " " + fileName + " - " + LanguageHandler.getString("canceled"), false, true, relatedEventId, EventStatus.error);
                    errorOccured = true;
                }
                else
                {
                    this.eventHandler.raiseNewEvent(LanguageHandler.getString("reading") + " " + fileName + " - 100%", false, true, relatedEventId, EventStatus.successful);
                }
                
            }
            compressionStream.Dispose();
            baseSourceStream.Close();
            transferDetails.successful = !errorOccured;
            return transferDetails;

        }

        //not implemented here
        public void close()
        {
            return;
        }

        //creates the archive folder
        public void create()
        {
            System.IO.Directory.CreateDirectory(this.path);
        }

        //creates an archive entry and returns the compression stream
        public Stream createAndGetFileStream(string path)
        {
            path = path.Replace("/", "\\");

            //to create directories, remove filename from path first
            string directoriesPath;
            if (path.Contains("\\"))
            {
                directoriesPath = path.Substring(0, path.LastIndexOf("\\"));
            }
            else
            {
                directoriesPath = "";
            }

            //create dest path
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(this.path, directoriesPath));

            //create file and io stream
            System.IO.FileStream baseDestStream = new FileStream(System.IO.Path.Combine(this.path, path), FileMode.Create);

            //open LZ4 stream
            BlockCompression.LZ4BlockStream compressionStream = new BlockCompression.LZ4BlockStream(baseDestStream, BlockCompression.AccessMode.write, this.useEncryption, this.aesKey, this.usingDedupe);

            if (!compressionStream.init())
            {
                return null;
            }

            return compressionStream;
        }

        //decompresses an entry to a given destination
        public bool getFile(string archivePath, string destinationPath)
        {
            string lastProgress = "";
            string fileName = Path.GetFileName(destinationPath);

            string path = archivePath.Replace("/", "\\");

            string sourcePath = System.IO.Path.Combine(this.path, path);

            int relatedEventId = -1;
            if (this.eventHandler != null)
            {
                relatedEventId = this.eventHandler.raiseNewEvent(LanguageHandler.getString("processing") + ": " + fileName + "... ", false, false, NO_RELATED_EVENT, EventStatus.inProgress);
            }

            //open source file
            System.IO.FileStream sourceStream = new FileStream(sourcePath, FileMode.Open);

            //open decoder stream
            BlockCompression.LZ4BlockStream blockCompressionStream = new BlockCompression.LZ4BlockStream(sourceStream, BlockCompression.AccessMode.read, this.useEncryption, this.aesKey, this.usingDedupe);
            if (!blockCompressionStream.init())
            {                
                return false;
            }
            
            //LZ4DecoderStream compressionStream = LZ4Stream.Decode(sourceStream, 0);

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
            FileStream destStream = null; ;
            try
            {
                //open dest file stream
                destStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write);

                //set file size            
                destStream.SetLength(blockCompressionStream.Length);
            }catch(Exception ex)
            {
                //not enough space
                destStream.Close();
                System.IO.File.Delete(destinationPath);
                blockCompressionStream.Close();
                sourceStream.Close();
                this.eventHandler.raiseNewEvent(LanguageHandler.getString("processing") + ": " + fileName + "... " + LanguageHandler.getString("failed"), false, true, relatedEventId, EventStatus.error);
                return false;
            }

            byte[] buffer = new byte[20000000];
            long totalReadBytes = 0;
            int readBytes = -1;
            //read source and write destination
            while (readBytes != 0 && !this.stopRequest.value)
            {
                //transfer one block
                readBytes = blockCompressionStream.Read(buffer, 0, buffer.Length);
                if (readBytes > 0)
                {
                    destStream.Write(buffer, 0, readBytes);
                    totalReadBytes += readBytes;
                }

                //show progress
                string progress = Common.PrettyPrinter.prettyPrintBytes(totalReadBytes);
                if (progress != lastProgress && this.eventHandler != null)
                {
                    this.eventHandler.raiseNewEvent(LanguageHandler.getString("processing") + ": " + fileName + "... " + progress, false, true, relatedEventId, EventStatus.inProgress);
                    lastProgress = progress;
                }

            }

            if (this.eventHandler != null)
            {
                if (!this.stopRequest.value)
                {
                    //finished "normally"
                    this.eventHandler.raiseNewEvent(LanguageHandler.getString("processing") + ": " + fileName + "... " + LanguageHandler.getString("successful"), false, true, relatedEventId, EventStatus.successful);
                }
                else
                {
                    //stopped by user
                    this.eventHandler.raiseNewEvent(LanguageHandler.getString("processing")+ ": " + fileName + "... " + LanguageHandler.getString("failed"), false, true, relatedEventId, EventStatus.error);
                }
            }
            destStream.Close();
            blockCompressionStream.Close();
            sourceStream.Close();
            return true;

        }

        //lists all archive entries
        public List<string> listEntries()
        {
            List<string> retVal = new List<string>();

            string[] files = System.IO.Directory.GetFiles(this.path, "*", SearchOption.AllDirectories);

            //iterate files
            foreach(string file in files)
            {
                //remove base archive path
                string archivePath = file.Substring(this.path.Length + 1);

                archivePath = archivePath.Replace("\\", "/");
                retVal.Add(archivePath);
            }

            return retVal;
        }

        //not implemented here
        public void open(ZipArchiveMode mode)
        {
            return;
        }

        //opens an archive file and returns the decompression stream
        public Stream openAndGetFileStream(string path)
        {
            path = path.Replace("/", "\\");

            //open base filestream
            FileStream sourceStream = new FileStream(System.IO.Path.Combine(this.path, path), FileMode.Open);

            //open decoder stream
            BlockCompression.LZ4BlockStream blockCompressionStream = new BlockCompression.LZ4BlockStream(sourceStream, BlockCompression.AccessMode.read, this.useEncryption, this.aesKey, this.usingDedupe);

            if (!blockCompressionStream.init())
            {
                return null;
            }

            return blockCompressionStream;

        }

    }

    //wrapper to pass stop request by ref
    public class StopRequestWrapper
    {
        public bool value;
    }
}
