using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management;

namespace Common
{
    public class WMIHelper
    {

        //lists all active HyperV VMss
        public static List<OneVM> listVMs(string host, ManagementScope scope = null)
        {
            try
            {
                string query;

                if (UseWMIV2NameSpace)
                {
                    query =
                        "SELECT * FROM Msvm_VirtualSystemSettingData WHERE VirtualSystemType = 'Microsoft:Hyper-V:System:Realized'";
                }
                else
                {
                    query = "SELECT * FROM Msvm_VirtualSystemSettingData WHERE SettingType = 3";
                }

                if (scope == null)
                {
                    scope = new ManagementScope(GetWMIScope(host));
                }

                List<OneVM> vms = new List<OneVM>();
                using (var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(query)))
                {
                    using (var moc = searcher.Get())
                        foreach (var mo in moc)
                            using (mo)
                            {
                                OneVM vm = new OneVM();
                                vm.name = (string)mo["ElementName"];
                                vm.id = (string)mo["VirtualSystemIdentifier"];
                                vm.hdds = getHDDs((ManagementObject)mo);
                                vms.Add(vm);

                            }
                }

                return vms;

            }catch(Exception ex) //error occured, hyperv role not installed?
            {
                Common.DBQueries.addLog("error on listVMs", Environment.StackTrace, ex);
                return null;
            }
        }

        //gets the connected hdds for a given vm (ManagementObject)
        private static List<OneVMHDD> getHDDs(ManagementObject vm)
        {
            List<OneVMHDD> hdds = new List<OneVMHDD>();
            var iterator = vm.GetRelated("Msvm_StorageAllocationSettingData").GetEnumerator();
            List<ManagementObject> hddsMo = new List<ManagementObject>();
            
            //build hdd ManagementObject list
            while (iterator.MoveNext())
            {
                hddsMo.Add((ManagementObject)iterator.Current);
            }

            //retrieve details from ManagementObject list
            foreach (ManagementObject hdd in hddsMo)
            {
                OneVMHDD oneVMHDD = new OneVMHDD();
                string[] hddPath = (string[])hdd["HostResource"];
                if (!hddPath[0].EndsWith(".vhdx")) //ignore non-vhdx files
                {
                    continue;
                }

                //does hdd exist?
                if (!System.IO.File.Exists(hddPath[0]))
                {
                    oneVMHDD.name = "";
                    oneVMHDD.path = "";
                    hdds.Add(oneVMHDD);
                    continue;
                }

                //get hdd name from vhdx parser
                byte[] idBytes = vhdxParser.getVHDXIDFromFile(hddPath[0]);
                if (idBytes == null)
                {
                    oneVMHDD.name = "";
                    oneVMHDD.path = "";
                    hdds.Add(oneVMHDD);
                    continue;
                }

                string hddName = Convert.ToBase64String(idBytes);

                oneVMHDD.name = hddName;
                oneVMHDD.path = hddPath[0];
                hdds.Add(oneVMHDD);

            }

                return hdds;
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
                return version.Major >= 10 || (version.Major >= 6 && version.Minor >= 2);
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
            public List<OneVMHDD> hdds;
        }

        public struct OneVMHDD
        {
            public string name;
            public string path;
        }

        
    }
}
