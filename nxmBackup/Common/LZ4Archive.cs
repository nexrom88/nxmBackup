﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using K4os.Compression.LZ4.Streams;

namespace Common
{
    public class LZ4Archive : IArchive
    {
        private string path;
        private Common.EventHandler eventHandler;
        private const int NO_RELATED_EVENT = -1;


        public LZ4Archive(string path,Common.EventHandler eventHandler)
        {
            this.path = path;
            this.eventHandler = eventHandler;

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
        public void addFile(string file, string path)
        {
            path = path.Replace("/", "\\");
            string fileName = Path.GetFileName(file);

            //get base io Streams
            System.IO.FileStream baseSourceStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            //create dest path
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(this.path, path));
            
            //get dest stream
            System.IO.FileStream baseDestStream = new FileStream(System.IO.Path.Combine(this.path, path + "\\" + fileName) , FileMode.Create);

            //open LZ4 stream
            BlockCompression.LZ4BlockStream compressionStream = new BlockCompression.LZ4BlockStream(baseDestStream, BlockCompression.AccessMode.write);

            //create buffer and read counter
            byte[] buffer = new byte[4096];
            long bytesRemaining = baseSourceStream.Length;
            int lastPercentage = -1;

            int relatedEventId = -1;
            if (this.eventHandler != null)
            {
                relatedEventId = this.eventHandler.raiseNewEvent("Lese " + fileName + " - 0%", false, false, NO_RELATED_EVENT, EventStatus.inProgress);
            }

            while (bytesRemaining > 0)//still bytes to read?
            {
                if (bytesRemaining >= buffer.Length) //still a whole block to read?
                {
                    baseSourceStream.Read(buffer, 0, buffer.Length);
                    compressionStream.Write(buffer, 0, buffer.Length);
                    bytesRemaining -= buffer.Length;
                }
                else //eof -> read the last smaller block
                {
                    buffer = new byte[bytesRemaining];
                    baseSourceStream.Read(buffer, 0, buffer.Length);
                    compressionStream.Write(buffer, 0, buffer.Length);
                    bytesRemaining -= buffer.Length;
                }

                //calculate progress
                float percentage = (((float)baseSourceStream.Length - (float)bytesRemaining) / (float)baseSourceStream.Length) * 100.0f;

                //progress changed?
                if (lastPercentage != (int)percentage && this.eventHandler != null)
                {
                    this.eventHandler.raiseNewEvent("Lese " + fileName + " - " + (int)percentage + "%", false, true, relatedEventId, EventStatus.inProgress);
                    lastPercentage = (int)percentage;
                }

            }

            //transfer completed
            if (this.eventHandler != null)
            {
                this.eventHandler.raiseNewEvent("Lese " + fileName + " - 100%", false, true, relatedEventId, EventStatus.successful);
            }
            compressionStream.Dispose();
            baseSourceStream.Close();

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
            BlockCompression.LZ4BlockStream compressionStream = new BlockCompression.LZ4BlockStream(baseDestStream, BlockCompression.AccessMode.write);
            
            return compressionStream;
        }

        //decompresses an entry to a given destination
        public void getFile(string archivePath, string destinationPath)
        {
            string lastProgress = "";
            string fileName = Path.GetFileName(destinationPath);

            string path = archivePath.Replace("/", "\\");

            string sourcePath = System.IO.Path.Combine(this.path, path);

            int relatedEventId = -1;
            if (this.eventHandler != null)
            {
                relatedEventId = this.eventHandler.raiseNewEvent("Stelle wieder her: " + fileName + "... ", false, false, NO_RELATED_EVENT, EventStatus.inProgress);
            }

            //open source file
            System.IO.FileStream sourceStream = new FileStream(sourcePath, FileMode.Open);

            //open decoder stream
            BlockCompression.LZ4BlockStream blockCompressionStream = new BlockCompression.LZ4BlockStream(sourceStream, BlockCompression.AccessMode.read);
            //LZ4DecoderStream compressionStream = LZ4Stream.Decode(sourceStream, 0);

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
            FileStream destStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write);

            byte[] buffer = new byte[2000000];
            long totalReadBytes = 0;
            int readBytes = -1;
            //read source and write destination
            while (readBytes != 0)
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
                    this.eventHandler.raiseNewEvent("Stelle wieder her: " + fileName + "... " + progress, false, true, relatedEventId, EventStatus.inProgress);
                    lastProgress = progress;
                }

            }

            if (this.eventHandler != null)
            {
                this.eventHandler.raiseNewEvent("Stelle wieder her: " + fileName + "... erfolgreich", false, true, relatedEventId, EventStatus.successful);
            }
            destStream.Close();
            blockCompressionStream.Close();
            sourceStream.Close();

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
            BlockCompression.LZ4BlockStream blockCompressionStream = new BlockCompression.LZ4BlockStream(sourceStream, BlockCompression.AccessMode.read);

            return blockCompressionStream;

        }

    }
}