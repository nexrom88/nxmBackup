using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Compression;
using System.IO;


namespace Common
{
    public class ZipArchive : IArchive
    {
        string path;
        System.IO.Compression.ZipArchive archiveStream;
        FileStream fileStream;
        private Job.newEventDelegate newEvent;
        System.IO.Compression.CompressionLevel compressionLevel = CompressionLevel.Optimal;

        public ZipArchive (string path, Job.newEventDelegate newEvent)
        {
            this.newEvent = newEvent;
            this.path = path;
        }

        //creates a new zip archive
        public void create()
        {
            FileStream fileStream = new FileStream(this.path, FileMode.Create);
            System.IO.Compression.ZipArchive archiveStream = new System.IO.Compression.ZipArchive(fileStream, ZipArchiveMode.Create);
            archiveStream.Dispose();
            fileStream.Close();
        }

        //opens an existing zip archive
        public void open(ZipArchiveMode mode)
        {
           //open necessary streams
           this.fileStream = new FileStream(this.path, FileMode.Open);
           this.archiveStream = new System.IO.Compression.ZipArchive(fileStream, mode);          
        }

        //closes the archive
        public void close()
        {
            this.archiveStream.Dispose();
            this.fileStream.Close();
        }

        //creates a new entry and returns the io-strem
        public Stream createAndGetFileStream(string path)
        {
            //creaty zip entry
            ZipArchiveEntry entry = this.archiveStream.CreateEntry(path, compressionLevel);
            Stream outStream = entry.Open();
            return outStream;
        }

        //opens an entry and returns the io-strem
        public Stream openAndGetFileStream(string path)
        {
            //creaty zip entry
            ZipArchiveEntry entry = this.archiveStream.GetEntry(path);
            Stream outStream = entry.Open();
            return outStream;
        }


        //adds a file to the archive
        public void addFile(string file, string path)
        {
            //extract filename first
            string fileName = Path.GetFileName(file);
            FileStream sourceStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            //creaty zip entry
            ZipArchiveEntry entry = this.archiveStream.CreateEntry(path + "/" + fileName, compressionLevel); //virtual hard disks/xyz.vhdx
            Stream outStream = entry.Open();

            //create buffer and read counter
            byte[] buffer = new byte[4096];
            long bytesRemaining = sourceStream.Length;
            int lastPercentage = -1;

            raiseNewEvent("Lese " + fileName + " - 0%", false, false);

            while (bytesRemaining > 0)//still bytes to read?
            {
                if (bytesRemaining >= buffer.Length) //still a whole block to read?
                {
                    sourceStream.Read(buffer, 0, buffer.Length);
                    outStream.Write(buffer, 0, buffer.Length);
                    bytesRemaining -= buffer.Length;
                }
                else //eof -> read the last smaller block
                {
                    buffer = new byte[bytesRemaining];
                    sourceStream.Read(buffer, 0, buffer.Length);
                    outStream.Write(buffer, 0, buffer.Length);
                    bytesRemaining -= buffer.Length;
                }

                //calculate progress
                float percentage = (((float)sourceStream.Length - (float)bytesRemaining) / (float)sourceStream.Length) * 100.0f;

                //progress changed?
                if (lastPercentage != (int)percentage)
                {
                    raiseNewEvent("Lese " + fileName + " - " + (int)percentage + "%", false, true);
                    lastPercentage = (int)percentage;
                }
                
            }

            //transfer completed
            raiseNewEvent("Lese " + fileName + " - 100%", false, true);
            outStream.Close();
            sourceStream.Close();

        }

        //adds a whole folder to the archive
        public void addDirectory(string folder)
        {
            string[] entries = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);

            //iterate through all files
            foreach(string file in entries)
            {
                //build path
                string zipFile = file.Substring(folder.Length + 1).Replace("\\", "/");
                string archivePath = "";
                if (zipFile.Contains("/"))
                {
                    archivePath = zipFile.Substring(0, zipFile.LastIndexOf("/"));
                }
                
                //add file to archive
                addFile (file, archivePath);
            }


        }

        //gets a file from the archive
        public void getFile(string archivePath, string destinationPath)
        {
            ZipArchiveEntry entry = this.archiveStream.GetEntry(archivePath);
            string fileName = Path.GetFileName(destinationPath);
            raiseNewEvent("Stelle wieder her: " + fileName + "... 0.0KB", false, false);
            string lastProgress = "";

            //open streams
            Stream sourceStream = entry.Open();
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
            FileStream destStream = new FileStream(destinationPath, FileMode.OpenOrCreate, FileAccess.Write);

            byte[] buffer = new byte[4096];
            long totalReadBytes = 0;
            int readBytes = -1;
            //read source and write destination
            while (readBytes != 0)
            {               
                //transfer one block
                readBytes = sourceStream.Read(buffer, 0, buffer.Length);
                if (readBytes > 0)
                {
                    destStream.Write(buffer, 0, readBytes);
                    totalReadBytes += readBytes;
                }

                //show progress
                string progress = Common.PrettyPrinter.prettyPrintBytes(totalReadBytes);
                if (progress != lastProgress)
                {
                    raiseNewEvent("Stelle wieder her: " + fileName + "... " + progress, false, true);
                    lastProgress = progress;
                }


            }
            destStream.Close();
            sourceStream.Close();

        }


        //lists all archive entries
        public List<string> listEntries()
        {
            List<string> retVal = new List<string>();

            System.Collections.ObjectModel.ReadOnlyCollection<ZipArchiveEntry> entries = this.archiveStream.Entries;
            
            //iterate all entries
            foreach(ZipArchiveEntry entry in entries)
            {
                retVal.Add(entry.FullName);
            }

            return retVal;
        }

        //deletes a single file from the archive
        public void delete(string archivePath)
        {
            ZipArchiveEntry entry = this.archiveStream.GetEntry(archivePath);
            if (entry != null)
            {
                entry.Delete();
            }
        }

        //builds a EventProperties object and raises the "newEvent" event
        public void raiseNewEvent(string text, bool setDone, bool isUpdate)
        {
            Common.EventProperties props = new Common.EventProperties();
            props.text = text;
            props.setDone = setDone;
            props.isUpdate = isUpdate;

            if (this.newEvent != null)
            {
                this.newEvent(props);
            }
        }

    }
}
