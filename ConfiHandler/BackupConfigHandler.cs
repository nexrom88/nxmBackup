using System;
using System.Xml;
using System.IO;
using System.Collections.Generic;

namespace ConfigHandler
{
    public class BackupConfigHandler
    {
        //adds a newly created backup to the config file
        public static void addBackup(string basePath, string uuid, string type, string newInstanceID, string parentInstanceID, bool prepend)
        {
            //check whether config file already exists
            if (!File.Exists(Path.Combine(basePath, "config.xml")))
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
                bodyElement.AppendChild(attrElement);

                //generate BackupChain node
                XmlElement chainElement = doc.CreateElement(string.Empty, "BackupChain", string.Empty);
                bodyElement.AppendChild(chainElement);
                

                doc.Save(Path.Combine(basePath, "config.xml"));
            }

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
                if(backupsElement.ChildNodes.Item(i).Attributes.GetNamedItem("uuid").Value == uuid)
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
            if (!File.Exists(Path.Combine(basePath, "config.xml"))) { return null; }

            //file exists, open it
            FileStream baseStream = new FileStream(Path.Combine(basePath, "config.xml"), FileMode.Open, FileAccess.ReadWrite);
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
                backupChain.Add(backup);
            }

            return backupChain;
        }

        public struct BackupInfo
        {
            public string uuid;
            public string timeStamp;
            public string type;
            public string instanceID;
            public string parentInstanceID;
        }

    }
}
