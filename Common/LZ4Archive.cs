using System;
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
        private Job.newEventDelegate newEvent;


        public LZ4Archive(string path, Job.newEventDelegate newEvent)
        {
            this.path = path;
            this.newEvent = newEvent;
        }

        void IArchive.addDirectory(string folder, CompressionLevel compressionLevel)
        {
            throw new NotImplementedException();
        }

        //adds a given file to the archive
        void IArchive.addFile(string file, string path, CompressionLevel compressionLevel)
        {
            path = path.Replace("/", "\\");
            string fileName = Path.GetFileName(file);

            //get base io Streams
            System.IO.FileStream baseSourceStream = new FileStream(file, FileMode.Open);

            //create dest path
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(this.path, path));
            
            //get dest stream
            System.IO.FileStream baseDestStream = new FileStream(System.IO.Path.Combine(this.path, path + "\\" + fileName) , FileMode.Create);

            //open LZ4 stream
            LZ4EncoderStream compressionStream = LZ4Stream.Encode(baseDestStream, null, false);

            //create buffer and read counter
            byte[] buffer = new byte[4096];
            long bytesRemaining = baseSourceStream.Length;
            int lastPercentage = -1;

            raiseNewEvent("Lese " + fileName + " - 0%", false, false);

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
                if (lastPercentage != (int)percentage)
                {
                    raiseNewEvent("Lese " + fileName + " - " + (int)percentage + "%", false, true);
                    lastPercentage = (int)percentage;
                }

            }

            //transfer completed
            raiseNewEvent("Lese " + fileName + " - 100%", false, true);
            baseSourceStream.Close();
            compressionStream.Close();

        }

        //not implemented here
        void IArchive.close()
        {
            return;
        }

        //creates the archive folder
        void IArchive.create()
        {
            System.IO.Directory.CreateDirectory(this.path);
        }

        Stream IArchive.createAndGetFileStream(string path, CompressionLevel compressionLevel)
        {
            throw new NotImplementedException();
        }

        void IArchive.getFile(string archivePath, string destinationPath)
        {
            throw new NotImplementedException();
        }

        List<string> IArchive.listEntries()
        {
            throw new NotImplementedException();
        }

        void IArchive.open(ZipArchiveMode mode)
        {
            throw new NotImplementedException();
        }

        Stream IArchive.openAndGetFileStream(string path)
        {
            throw new NotImplementedException();
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
