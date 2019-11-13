using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management;
using Alphaleonis.Win32.Vss;
using System.IO;

namespace HyperVBackup
{
    class BackupHelpers
    {

        //reads the created backup and brings it to destination
        public void copyBackup(IList<IVssWMComponent> components, string destinationPath)
        {
            //files available
            if (components.Count > 0)
            {
                List<FileComponents> files = new List<FileComponents>();
                //iterate files
                foreach (var file in components[0].Files)
                {
                    //just take necessary files
                    //bitmask explanation see http://alphavss.alphaleonis.com/doc/1.4.0/api/html/797597BD.htm
                    int var1 = (int)file.BackupTypeMask;
                    int var2 = (int)VssFileSpecificationBackupType.FullBackupRequired;

                    if ((int)file.BackupTypeMask != 66821 && ((int)file.BackupTypeMask & (int)VssFileSpecificationBackupType.FullBackupRequired) == (int)VssFileSpecificationBackupType.FullBackupRequired)
                    {
                        FileComponents backupFile = new FileComponents();
                        backupFile.path = file.Path;
                        backupFile.file = file.FileSpecification;
                        files.Add(backupFile);
                    }
                }

                //copy files to the destination
                foreach (FileComponents file in files)
                {
                    string[] buffer = file.path.Split("\\".ToCharArray());                    
                    Console.WriteLine("Backing up file " + file.file);
                    string sourceFile = Path.Combine(file.path, file.file);
                    string destinationFile = Path.Combine(destinationPath, file.file);
                    FileTransferHelper transferHelper = new FileTransferHelper(sourceFile, destinationFile);
                    transferHelper.startTransfer();
                    
                    }
            }


        }

       

        private static string GetORStr(string fieldName, IEnumerable<string> vmNames)
        {
            var sb = new StringBuilder();
            foreach (var vmName in vmNames)
            {
                if (sb.Length > 0)
                    sb.Append(" OR ");
                sb.Append($"{fieldName} = '{EscapeWMIStr(vmName)}'");
            }
            return sb.ToString();
        }

        private static string EscapeWMIStr(string str)
        {
            return str?.Replace("'", "''");
        }

        private bool UseWMIV2NameSpace
        {
            get
            {
                var version = Environment.OSVersion.Version;
                return version.Major >= 6 && version.Minor >= 2;
            }
        }

        private string GetWMIScope(string host = "localhost")
        {
            string scopeFormatStr;
            if (UseWMIV2NameSpace)
                scopeFormatStr = "\\\\{0}\\root\\virtualization\\v2";
            else
                scopeFormatStr = "\\\\{0}\\root\\virtualization";

            return (string.Format(scopeFormatStr, host));
        }

        public enum VmNameType
        {
            ElementName,
            SystemName
        }

        public struct FileComponents
        {
            public string path;
            public string file;
        }
    }
}
