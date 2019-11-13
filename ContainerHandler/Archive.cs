using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Compression;
using System.IO;

namespace ContainerHandler
{
    public class Archive
    {
        string path;
        ZipArchive archiveStream;
        FileStream fileStream;

        public Archive (string path)
        {
            this.path = path;
        }

        //creates a new zip archive
        public void create()
        {
            FileStream fileStream = new FileStream(this.path, FileMode.Create);
            ZipArchive archiveStream = new ZipArchive(fileStream, ZipArchiveMode.Create);
            archiveStream.Dispose();
            fileStream.Close();
        }

        //opens an existing zip archive
        public void open(ZipArchiveMode mode)
        {
           //open necessary streams
           this.fileStream = new FileStream(this.path, FileMode.Open);
           this.archiveStream = new ZipArchive(fileStream, mode);          
        }

        //closes the archive
        public void close()
        {
            this.archiveStream.Dispose();
            this.fileStream.Close();
        }

        //creates a new entry and returns the io-strem
        public Stream getFileStream(string path)
        {
            //creaty zip entry
            ZipArchiveEntry entry = this.archiveStream.CreateEntry(path, CompressionLevel.NoCompression);
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
            ZipArchiveEntry entry = this.archiveStream.CreateEntry(path + fileName, CompressionLevel.NoCompression);
            Stream outStream = entry.Open();

            //create buffer and read counter
            byte[] buffer = new byte[4096];
            long bytesRemaining = sourceStream.Length;

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
            }

            //transfer completed
            outStream.Close();
            sourceStream.Close();

        }

        //adds a whole folder to the archive
        public void addDirectory(string folder)
        {
            string[] entries = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);

            //get last directory element
            string[] splitter = folder.Split("\\".ToCharArray());
            string topDirectory = splitter[splitter.Length - 1] + "/";

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


                string path = topDirectory + archivePath;

                //add file to archive
                addFile (file, path);
            }


        }

        //gets a file from the archive
        public void getFile(string archivePath, string destinationPath)
        {
            ZipArchiveEntry entry = this.archiveStream.GetEntry(archivePath);

            //open streams
            Stream sourceStream = entry.Open();
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
            FileStream destStream = new FileStream(destinationPath, FileMode.OpenOrCreate, FileAccess.Write);

            long bytesRemaining = sourceStream.Length;
            byte[] buffer = new byte[4096];

            //read source and write destination
            while (bytesRemaining > 0)
            {
                if (bytesRemaining >= buffer.Length) //can the buffer still be filled
                {
                    //transfer one block
                    sourceStream.Read(buffer, 0, buffer.Length);
                    destStream.Write(buffer, 0, buffer.Length);
                    bytesRemaining -= buffer.Length;
                }
                else //eof => read last block
                {
                    buffer = new byte[bytesRemaining];
                    sourceStream.Read(buffer, 0, buffer.Length);
                    destStream.Write(buffer, 0, buffer.Length);
                    bytesRemaining -= buffer.Length;
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

    }
}
