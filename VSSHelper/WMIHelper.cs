﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management;

namespace VSSHelper
{
    public class WMIHelper
    {

        //lists all active HyperV VMs
        public static List<string> listVMs()
        {
            string query;

            if (UseWMIV2NameSpace)
            {
                query =
                    "SELECT VirtualSystemIdentifier, ElementName FROM Msvm_VirtualSystemSettingData WHERE VirtualSystemType = 'Microsoft:Hyper-V:System:Realized'";
            }
            else
            {
                query = "SELECT SystemName, ElementName FROM Msvm_VirtualSystemSettingData WHERE SettingType = 3";
            }


            var scope = new ManagementScope(GetWMIScope());

            List<string> vms = new List<string>();
            using (var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(query)))
            {
                using (var moc = searcher.Get())
                    foreach (var mo in moc)
                        using (mo)
                        {
                            
                            vms.Add ((string)mo["ElementName"]);

                        }
            }

            return vms;
        }

        //gets the system information of the requested VM
        public static OneVM GetVMData(string vmName)
        {
            string query;
            string vmIdField;

            if (UseWMIV2NameSpace)
            {
                query =
                    "SELECT VirtualSystemIdentifier, ElementName FROM Msvm_VirtualSystemSettingData WHERE VirtualSystemType = 'Microsoft:Hyper-V:System:Realized'";
                vmIdField = "VirtualSystemIdentifier";
            }
            else
            {
                query = "SELECT SystemName, ElementName FROM Msvm_VirtualSystemSettingData WHERE SettingType = 3";
                vmIdField = "SystemName";
            }


            var scope = new ManagementScope(GetWMIScope());

            if (vmName != null)
                query += $" AND (ElementName='" + vmName + "')";

            OneVM vmData = new OneVM();
            using (var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(query)))
            {
                using (var moc = searcher.Get())
                    foreach (var mo in moc)
                        using (mo)
                        {
                            vmData.id = (string)mo[vmIdField];
                            vmData.name = (string)mo["ElementName"];

                        }
            }

            return vmData;
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

        private static bool UseWMIV2NameSpace
        {
            get
            {
                var version = Environment.OSVersion.Version;
                return version.Major >= 6 && version.Minor >= 2;
            }
        }

        private static string GetWMIScope(string host = "localhost")
        {
            string scopeFormatStr;
            if (UseWMIV2NameSpace)
                scopeFormatStr = "\\\\{0}\\root\\virtualization\\v2";
            else
                scopeFormatStr = "\\\\{0}\\root\\virtualization";

            return (string.Format(scopeFormatStr, host));
        }

        public struct OneVM
        {
            public string id;
            public string name;
        }

        
    }
}
