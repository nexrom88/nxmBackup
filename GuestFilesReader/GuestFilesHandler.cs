using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management;
using System.IO;

namespace GuestFilesReader
{
    public class GuestFilesHandler
    {
        private string vhdPath;
        private HyperVBackupRCT.VirtualDiskHandler diskHandler;
        public event Common.Job.newEventDelegate newEvent;

        public GuestFilesHandler(string vhdPath)
        {
            this.vhdPath = vhdPath;
        }

        //mounts vhdx file without driveletter
        public void mountVHD()
        {
            diskHandler = new HyperVBackupRCT.VirtualDiskHandler(this.vhdPath);
            diskHandler.open(HyperVBackupRCT.VirtualDiskHandler.VirtualDiskAccessMask.AttachReadOnly);
            diskHandler.attach(HyperVBackupRCT.VirtualDiskHandler.ATTACH_VIRTUAL_DISK_FLAG.ATTACH_VIRTUAL_DISK_FLAG_NO_DRIVE_LETTER | HyperVBackupRCT.VirtualDiskHandler.ATTACH_VIRTUAL_DISK_FLAG.ATTACH_VIRTUAL_DISK_FLAG_READ_ONLY);

        }

        //gets all current mounted drives with no drive letter
        public List<GuestVolume> getMountedDrives()
        {
            List<GuestVolume> drives = new List<GuestVolume>();
            
            string scopeStr = @"\\.\root\cimv2";


            ManagementScope scope = new ManagementScope(scopeStr);
            scope.Connect();

            string queryString = "SELECT * FROM Win32_Volume WHERE DriveLetter IS NULL";
            SelectQuery query = new SelectQuery(queryString);
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(scope, query))
            {
                foreach (ManagementObject disk in searcher.Get())
                {
                    GuestVolume volume = new GuestVolume();
                    volume.path = disk["Name"].ToString();

                    if (disk["Label"] == null)
                    {
                        volume.caption = "Unbenanntes Laufwerk";
                    }
                    else
                    {
                        volume.caption = disk["Label"].ToString();
                    }
                    drives.Add(volume);

                }
            }
            return drives;

        }

        //detaches the vhd
        public void detach()
        {
            this.diskHandler.detach();
        }

        //performs the restore of a single file to local storage
        public void restoreFiles2Local(List<string> files, string destination, string baseSourcePath)
        {

            //prepare EventProperties for progress events
            Common.EventProperties props = new Common.EventProperties();
            props.elementsCount = (uint)files.Count;
            props.currentElement = 1;
            props.progress = 0.0;

            //iterate all files
            foreach (string file in files)
            {
                //create folder
                string folder = file.Substring(0, file.LastIndexOf("\\"));
                folder = folder.Replace(baseSourcePath, "");

                //create folder
                System.IO.Directory.CreateDirectory(destination + "\\" + folder);

                restoreFile2Local(file, destination + "\\" + folder + "\\" + file.Substring(file.LastIndexOf("\\")), props);

                props.currentElement++;

                
            }

            //raise event for indication restore completion
            props.currentElement--;
            props.setDone = true;
            newEvent(props);

        }


        //performs the restore of a single file to local storage
        private void restoreFile2Local(string source, string destination, Common.EventProperties props)
        {

            //open both streams
            try
            {
                FileStream sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read);
                FileStream destinationStream = new FileStream(destination, FileMode.CreateNew);

                //transfer all bytes
                long bytesRead = 0;
                int bufferSize = 4096;
                byte[] buffer = new byte[bufferSize];
                double lastProgress = 0.0f;

                while (bytesRead < sourceStream.Length)
                {
                    //can read full buffer?
                    if (bytesRead + bufferSize <= sourceStream.Length)
                    {
                        bytesRead += sourceStream.Read(buffer, 0, bufferSize);
                        destinationStream.Write(buffer, 0, bufferSize);
                    }
                    else //read last, smaller block
                    {
                        buffer = new byte[sourceStream.Length - bytesRead];
                        bytesRead += sourceStream.Read(buffer, 0, buffer.Length);
                        destinationStream.Write(buffer, 0, buffer.Length);

                    }

                    //calculate progress
                    double currentProgress = Math.Round(((float)bytesRead / (float)sourceStream.Length) * 1000.0f) / 10.0f;
                    if (currentProgress != lastProgress)
                    {
                        props.progress = currentProgress;
                        this.newEvent(props);
                        lastProgress = currentProgress;
                    }
                    
                }


                //transfer completed
                sourceStream.Close();
                destinationStream.Close();

            }catch (IOException ex)
            {
                //io exception
                props.progress = -1.0f; //-1.0f for error
                props.text = ex.ToString();
                this.newEvent(props);
                return;
            }
        }

    }

    public struct GuestVolume
    {
        public string path;
        public string caption;
    }
}
