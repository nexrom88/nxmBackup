// Helper Code from:
//https://docs.microsoft.com/en-us/previous-versions/windows/desktop/virtual/importing-virtual-machines

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management;

namespace RestoreHelper
{
    public class VMImporter
    {
        static ManagementObject CreateSwitch(ManagementScope scope, string name, string friendlyName, int learnableAddress)
        {
            ManagementObject switchService =  Common.wmiUtilitiesForHyperVImport.GetServiceObject(scope, "Msvm_VirtualSwitchManagementService");
            ManagementObject createdSwitch = null;

            ManagementBaseObject inParams = switchService.GetMethodParameters("CreateSwitch");
            inParams["FriendlyName"] = friendlyName;
            inParams["Name"] = name;
            inParams["NumLearnableAddresses"] = learnableAddress;
            inParams["ScopeofResidence"] = null;
            ManagementBaseObject outParams = switchService.InvokeMethod("CreateSwitch", inParams, null);
            if ((UInt32)outParams["ReturnValue"] == Common.ReturnCode.Completed)
            {
                Console.WriteLine("{0} was created successfully", inParams["Name"]);
                createdSwitch = new ManagementObject(outParams["CreatedVirtualSwitch"].ToString());
            }
            else
            {
                Console.WriteLine("Failed to create {0} switch.", inParams["Name"]);
            }
            return createdSwitch;
        }


        static ManagementBaseObject GetVirtualSystemImportSettingData(ManagementScope scope, string importDirectory, string rootDirectoryToCopy)
        {
            string targetVhdResourcePath = importDirectory + "\\Temp.vhd"; //Directories specified should exist
            ManagementObject virtualSystemService = Common.wmiUtilitiesForHyperVImport.GetServiceObject(scope, "Msvm_VirtualSystemManagementService");
            ManagementBaseObject importSettingData = null;
            ManagementBaseObject inParams = virtualSystemService.GetMethodParameters("GetVirtualSystemImportSettingData");
            inParams["ImportDirectory"] = importDirectory;

            ManagementBaseObject outParams = virtualSystemService.InvokeMethod("GetVirtualSystemImportSettingData", inParams, null);

            if ((UInt32)outParams["ReturnValue"] == Common.ReturnCode.Started)
            {
                if (Common.wmiUtilitiesForHyperVImport.JobCompleted(outParams, scope))
                {
                    importSettingData = (ManagementBaseObject)outParams["ImportSettingData"];
                    Console.WriteLine("Import Setting Data for the ImportDirectory '{0}' was retrieved successfully.", importDirectory);
                }
                else
                {
                    Console.WriteLine("Failed to get the Import Setting Data");
                }
            }
            else if ((UInt32)outParams["ReturnValue"] == Common.ReturnCode.Completed)
            {
                importSettingData = (ManagementBaseObject)outParams["ImportSettingData"];
                Console.WriteLine("Import Setting Data for the ImportDirectory '{0}' was retrieved successfully.", importDirectory);
            }
            else
            {
                Console.WriteLine("Failed to get the Import Setting Data for the Import Directory :{0}", (UInt32)outParams["ReturnValue"]);
            }

            inParams.Dispose();
            outParams.Dispose();
            virtualSystemService.Dispose();

            importSettingData["GenerateNewId"] = true;
            importSettingData["CreateCopy"] = true;
            importSettingData["Name"] = "NewSampleVM";
            importSettingData["TargetResourcePaths"] = new string[] { (targetVhdResourcePath) };
            //ManagementObject newSwitch = CreateSwitch(scope, "Switch_For_Import_Export_Sample", "Switch_For_Import_Export_Sample", 1024);
            //importSettingData["TargetNetworkConnections"] = new string[] { (newSwitch.GetPropertyValue("Name").ToString()) };

            return importSettingData;
        }

        public static bool ImportVirtualSystemEx(string importDirectory)
        {
            string importCopyDirectory = importDirectory + "\\NewCopy";
            ManagementScope scope = new ManagementScope(@"root\virtualization", null);
            ManagementObject virtualSystemService = Common.wmiUtilitiesForHyperVImport.GetServiceObject(scope, "Msvm_VirtualSystemManagementService");

            ManagementBaseObject importSettingData = GetVirtualSystemImportSettingData(scope, importDirectory, importCopyDirectory);

            ManagementBaseObject inParams = virtualSystemService.GetMethodParameters("ImportVirtualSystemEx");
            inParams["ImportDirectory"] = importDirectory;
            inParams["ImportSettingData"] = importSettingData.GetText(TextFormat.CimDtd20);

            ManagementBaseObject outParams = virtualSystemService.InvokeMethod("ImportVirtualSystemEx", inParams, null);


            inParams.Dispose();
            outParams.Dispose();
            virtualSystemService.Dispose();

            if ((UInt32)outParams["ReturnValue"] == Common.ReturnCode.Started)
            {
                if (Common.wmiUtilitiesForHyperVImport.JobCompleted(outParams, scope))
                {
                    return true;

                }
                else
                {
                    return false;
                }
            }
            else if ((UInt32)outParams["ReturnValue"] == Common.ReturnCode.Completed)
            {
                return true;
            }
            else
            {
                return false;
            }

        }

    }
}
