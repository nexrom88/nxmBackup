using System;
using System.Xml;
using System.IO;
using System.Collections.Generic;

namespace ConfigHandler
{
    public class BackupConfigHandler
    {
        //adds a newly created backup to the config file
        public static bool addBackup(string basePath, bool encryption, string uuid, string type, string newInstanceID, string parentInstanceID, bool prepend, string jobExecutionId)
        {
            //check whether config file already exists
            if (!File.Exists(Path.Combine(basePath, "config.xml")))
            {
                if (!initConfigFile(basePath, encryption))
                {
                    return false;
                }
            }

            try
            {
                //file exists, open it
                FileStream baseStream = new FileStream(Path.Combine(basePath, "config.xml"), FileMode.Open, FileAccess.ReadWrite);
                XmlDocument xml = new XmlDocument();
                xml.Load(baseStream);
                baseStream.Close();

                //add a "backup" node
                string timeStamp = DateTime.Now.ToString("yyyyMMddHHmmssfff"); //gets the current timestamp
                XmlElement rootElement = (XmlElement)xml.SelectSingleNode("VMBackup");
                XmlElement backupsElement = (XmlElement)rootElement.SelectSingleNode("BackupChain");
                XmlElement newElement = xml.CreateElement(String.Empty, "Backup", String.Empty);
                newElement.SetAttribute("uuid", uuid);
                newElement.SetAttribute("timestamp", timeStamp);
                newElement.SetAttribute("type", type);
                newElement.SetAttribute("InstanceId", newInstanceID);
                newElement.SetAttribute("ParentInstanceId", parentInstanceID);
                newElement.SetAttribute("JobExecutionId", jobExecutionId);

                //prepend new node?
                if (prepend)
                {
                    backupsElement.PrependChild(newElement);
                }
                else
                {
                    backupsElement.AppendChild(newElement);
                }

                //close xml file
                baseStream = new FileStream(Path.Combine(basePath, "config.xml"), FileMode.Create, FileAccess.ReadWrite);
                xml.Save(baseStream);
                baseStream.Close();
            }catch(Exception ex)
            {
                Common.DBQueries.addLog("error on writing config file", Environment.StackTrace, ex);
                return false;
            }

            return true;

        }

        //creates a new xml config file
        private static bool initConfigFile(string basePath, bool encryption)
        {
            try
            {
                XmlDocument doc = new XmlDocument();

                //(1) the xml declaration is recommended, but not mandatory
                XmlDeclaration xmlDeclaration = doc.CreateXmlDeclaration("1.0", "UTF-8", null);
                XmlElement root = doc.DocumentElement;
                doc.InsertBefore(xmlDeclaration, root);

                //generate root node
                XmlElement bodyElement = doc.CreateElement(string.Empty, "VMBackup", string.Empty);
                doc.AppendChild(bodyElement);

                //generate attributes node
                XmlElement attrElement = doc.CreateElement(string.Empty, "Attributes", string.Empty);
                attrElement.SetAttribute("encryption", encryption.ToString());
                bodyElement.AppendChild(attrElement);

                //generate BackupChain node
                XmlElement chainElement = doc.CreateElement(string.Empty, "BackupChain", string.Empty);
                bodyElement.AppendChild(chainElement);


                doc.Save(Path.Combine(basePath, "config.xml"));
            }
            catch (Exception ex)
            {
                Common.DBQueries.addLog("error on creating config file", Environment.StackTrace, ex);
                return false;
            }
            return true;
        }

        //sets to lb end time for a given backup uuid
        public static void setLBEndTime(string basePath, string uuid)
        {
            //open xml
            FileStream baseStream = new FileStream(Path.Combine(basePath, "config.xml"), FileMode.Open, FileAccess.Read);
            XmlDocument xml = new XmlDocument();
            xml.Load(baseStream);
            baseStream.Close();

            //open required nodes
            //open VMBackup
            XmlElement rootElement = (XmlElement)xml.SelectSingleNode("VMBackup");
            XmlElement backupsElement = (XmlElement)rootElement.SelectSingleNode("BackupChain");

            //read backups
            for (int i = 0; i < backupsElement.ChildNodes.Count; i++)
            {
                XmlNode currentNode = backupsElement.ChildNodes[i];
                XmlElement currentBackup = (XmlElement)currentNode;
                
                //lb backup found?
                if (currentBackup.GetAttribute("uuid") == uuid)
                {
                    currentBackup.SetAttribute("lbEndTime", DateTime.Now.ToString("yyyyMMddHHmmssfff"));
                    backupsElement.ReplaceChild(currentBackup, currentNode);
                    break;
                }
            }

            //save changed xml
            baseStream = new FileStream(Path.Combine(basePath, "config.xml"), FileMode.Create, FileAccess.Write);
            xml.Save(baseStream);
            baseStream.Close();

        }

        //removes a backup from the chain identified by uuid
        public static void removeBackup(string basePath, string uuid)
        {
            //open xml
            FileStream baseStream = new FileStream(Path.Combine(basePath, "config.xml"), FileMode.Open, FileAccess.ReadWrite);
            XmlDocument xml = new XmlDocument();
            xml.Load(baseStream);
            baseStream.Close();

            //open required nodes
            //open VMBackup
            XmlElement rootElement = (XmlElement)xml.SelectSingleNode("VMBackup");
            XmlElement backupsElement = (XmlElement)rootElement.SelectSingleNode("BackupChain");

            //iterate through all backups
            for (int i = 0; i < backupsElement.ChildNodes.Count; i++)
            {
                //found item to be deleted?
                if (backupsElement.ChildNodes.Item(i).Attributes.GetNamedItem("uuid").Value == uuid)
                {
                    backupsElement.RemoveChild(backupsElement.ChildNodes.Item(i));
                }
            }

            //write changed xml
            baseStream = new FileStream(Path.Combine(basePath, "config.xml"), FileMode.Create, FileAccess.ReadWrite);
            xml.Save(baseStream);
            baseStream.Close();

        }

        //reads all backups from the backup chain
        public static List<BackupInfo> readChain(string basePath)
        {
            List<BackupInfo> backupChain = new List<BackupInfo>();

            //check whether config file exists
            if (!File.Exists(Path.Combine(basePath, "config.xml"))) { return backupChain; }

            try
            {
                //file exists, open it
                FileStream baseStream = new FileStream(Path.Combine(basePath, "config.xml"), FileMode.Open, FileAccess.Read);
                XmlDocument xml = new XmlDocument();
                xml.Load(baseStream);
                baseStream.Close();

                //open VMBackup
                XmlElement rootElement = (XmlElement)xml.SelectSingleNode("VMBackup");
                XmlElement backupsElement = (XmlElement)rootElement.SelectSingleNode("BackupChain");

                //iterate through all backups
                for (int i = 0; i < backupsElement.ChildNodes.Count; i++)
                {
                    //build structure
                    BackupInfo backup = new BackupInfo();
                    backup.uuid = backupsElement.ChildNodes.Item(i).Attributes.GetNamedItem("uuid").Value;
                    backup.type = backupsElement.ChildNodes.Item(i).Attributes.GetNamedItem("type").Value;
                    backup.timeStamp = backupsElement.ChildNodes.Item(i).Attributes.GetNamedItem("timestamp").Value;
                    backup.instanceID = backupsElement.ChildNodes.Item(i).Attributes.GetNamedItem("InstanceId").Value;
                    backup.parentInstanceID = backupsElement.ChildNodes.Item(i).Attributes.GetNamedItem("ParentInstanceId").Value;
                    backup.jobExecutionId = backupsElement.ChildNodes.Item(i).Attributes.GetNamedItem("JobExecutionId").Value;

                    if (backup.type == "lb")
                    {
                        XmlNode endTimeNode = backupsElement.ChildNodes.Item(i).Attributes.GetNamedItem("lbEndTime");

                        if (endTimeNode != null)
                        {
                            backup.lbEndTime = backupsElement.ChildNodes.Item(i).Attributes.GetNamedItem("lbEndTime").Value;
                        }
                    }


                    backupChain.Add(backup);
                }

                return backupChain;

            }
            catch
            {
                //config.xml is not accesible, return empty list
                return new List<BackupInfo>();
            }
        }

        //builds an array of hdd files from a given backup chain
        public static string[] getHDDFilesFromChain(List<BackupInfo> restoreChain, string basePath, string userSelectedHDD)
        {
            string[] retVal = new string[restoreChain.Count];
            string targetHDD = "";

            //iterate through all backups within chain in reverse to read full backup first
            for (int i = restoreChain.Count - 1; i >= 0; i--)
            {
                if (restoreChain[i].type == "full")
                {
                    //did user select an HDD?
                    if (userSelectedHDD != null)
                    {
                        retVal[i] = userSelectedHDD;
                        targetHDD = System.IO.Path.GetFileName(userSelectedHDD);
                    }
                    else //no user-selected HDD
                    {
                        //get all vhdx files
                        string vmBasePath = System.IO.Path.Combine(basePath, restoreChain[i].uuid + ".nxm\\" + "Virtual Hard Disks");
                        string[] entries = System.IO.Directory.GetFiles(vmBasePath, "*.vhdx");
                        retVal[i] = entries[0]; //take first found file. OK here because otherwise user would have chosen one
                        targetHDD = System.IO.Path.GetFileName(entries[0]);
                    }
                }
                else if (restoreChain[i].type == "rct")
                {
                    string vmBasePath = System.IO.Path.Combine(basePath, restoreChain[i].uuid + ".nxm\\");
                    retVal[i] = System.IO.Path.Combine(vmBasePath, targetHDD + ".cb");
                }
                else if (restoreChain[i].type == "lb")
                {
                    string vmBasePath = System.IO.Path.Combine(basePath, restoreChain[i].uuid + ".nxm\\");
                    retVal[i] = System.IO.Path.Combine(vmBasePath, targetHDD + ".lb");
                }
            }

            return retVal;

        }

        //builds an array of hdd files from a given backup chain for lr
        public static LRBackupChains getHDDFilesFromChainForLR(List<BackupInfo> restoreChain, string basePath)
        {
            LRBackupChains retVal = new LRBackupChains();

            
            string[] targetHDDs = null;

            //iterate through all backups within chain in reverse to read full backup first
            for (int i = restoreChain.Count - 1; i >= 0; i--)
            {
                if (restoreChain[i].type == "full") 
                {

                    //get all vhdx files
                    string vmBasePath = System.IO.Path.Combine(basePath, restoreChain[i].uuid + ".nxm\\" + "Virtual Hard Disks");
                    string[] entries = System.IO.Directory.GetFiles(vmBasePath, "*.vhdx");

                    //init chains
                    retVal.chains = new LRBackupChain[entries.Length];
                    targetHDDs = new string[entries.Length];
                    for(int j = 0; j < retVal.chains.Length; j++)
                    {
                        retVal.chains[j].files = new string[restoreChain.Count];
                    }
                    
                    //iterate entries
                    for (int j = 0; j < entries.Length; j++)
                    {
                        retVal.chains[j].files[i] = entries[j];
                        targetHDDs[j] = System.IO.Path.GetFileName(entries[j]);
                    }

                    

                }
                else if (restoreChain[i].type == "rct")
                {
                    string vmBasePath = System.IO.Path.Combine(basePath, restoreChain[i].uuid + ".nxm\\");

                    for (int j = 0; j < targetHDDs.Length; j++)
                    {
                        retVal.chains[j].files[i] = System.IO.Path.Combine(vmBasePath, targetHDDs[j] + ".cb");
                    }
                    
                }
                else if (restoreChain[i].type == "lb")
                {
                    string vmBasePath = System.IO.Path.Combine(basePath, restoreChain[i].uuid + ".nxm\\");
                    for (int j = 0; j < targetHDDs.Length; j++)
                    {
                        retVal.chains[j].files[i] = System.IO.Path.Combine(vmBasePath, targetHDDs[j] + ".lb");
                    }
                }
            }

            return retVal;

        }

        public struct LRBackupChains
        {
            public LRBackupChain[] chains;
        }

        public struct LRBackupChain
        {
            public string[] files;
        }


        public struct BackupInfo
        {
            public string uuid;
            public string timeStamp;
            public string lbEndTime;
            public string type;
            public string instanceID;
            public string parentInstanceID;
            public string jobExecutionId;
        }

    }
}
