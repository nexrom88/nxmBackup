// Helper Code from:
//https://github.com/microsoft/Windows-classic-samples/blob/master/Samples/Hyper-V/Pvm/cs/ImportUtilities.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management;
using Common;
using System.Threading;

namespace HVRestoreCore
{
    public class VMImporter
    {

        //imports a given vm, sets vm name nd returns vm ID
        public static string importVM(string vmDefinitionPath, string basePath, bool newId, string name)
        {
            ManagementScope scope = new ManagementScope(@"root\virtualization\v2");

            //
            // Retrieve the Virtual Machine Management Service.
            //
            using (ManagementObject managementService = WmiUtilities.GetVirtualMachineManagementService(scope))
            using (ManagementBaseObject inParams =
                managementService.GetMethodParameters("ImportSystemDefinition"))
            {
                //
                // Call the import method using the supplied arguments.
                //

                inParams["SystemDefinitionFile"] = vmDefinitionPath;
                inParams["SnapshotFolder"] = "";
                inParams["GenerateNewSystemIdentifier"] = newId;

                using (ManagementBaseObject outParams =
                    managementService.InvokeMethod("ImportSystemDefinition", inParams, null))
                {
                    //get the imported planned vm
                    ManagementObject vm =  GetImportedVM(outParams);

                    //rename the vm
                    renameVM(vm, name);

                    //set vhdx
                    setVHDX(vm, basePath);

                    //disconnect ethernet
                    disconnectEthernet(vm);

                    string vmID = vm["Name"].ToString();

                    //realize the planned vm
                    ManagementObject realizedVM =  realizePlannedVM(vmID);

                    return vmID;

                }
            }
        }

        //disconnects the current ethernet adapters. When vSwitch is not available it removes nic completely
        private static void disconnectEthernet(ManagementObject vm)
        {
            ManagementScope scope = new ManagementScope(@"root\virtualization\v2");
            ManagementObject managementService = WmiUtilities.GetVirtualMachineManagementService(scope);
            ManagementBaseObject outParams;

            ManagementObjectCollection settingDatas = vm.GetRelated("Msvm_VirtualSystemSettingData");
            
            //iterate settings
            foreach (ManagementObject settingData in settingDatas)
            {
                ManagementObjectCollection ethernetSettingData = settingData.GetRelated("Msvm_SyntheticEthernetPortSettingData", "Msvm_VirtualSystemSettingDataComponent", null, null, "PartComponent", "GroupComponent", false, null);

                //get virtual switches on local system
                ManagementObject[] vSwitches = WmiUtilities.GetVirtualSwitches(scope);

                //iterate nics
                foreach(ManagementObject nic in ethernetSettingData)
                {
                    ManagementObjectCollection nicAllocations = nic.GetRelated("Msvm_EthernetPortAllocationSettingData");

                    foreach (ManagementObject nicAllocation in nicAllocations)
                    {
                        //get connected ethernet switch
                        string switchName = nicAllocation["LastKnownSwitchName"].ToString();

                        bool switchAvailable = checkSwitch(switchName, vSwitches); //ignore result atm

                        nicAllocation["EnabledState"] = 3;
                        ManagementBaseObject inParams = managementService.GetMethodParameters("ModifyResourceSettings");
                        inParams["ResourceSettings"] = new string[] {nicAllocation.GetText(TextFormat.WmiDtd20) };
                        outParams = managementService.InvokeMethod("ModifyResourceSettings", inParams, null);
                        WmiUtilities.ValidateOutput(outParams, scope);
                    }

                }
            }

              

        }

        //checks whether a vswitch with the given name exists
        private static bool checkSwitch(string name, ManagementObject[] switches)
        {
            foreach (ManagementObject vSwitch in switches)
            {
                if (vSwitch["ElementName"].ToString() == name)
                {
                    return true;
                }
            }
            return false;
        }

        //sets the vhdx path for a planned vm
        private static void setVHDX(ManagementObject vm, string basePath)
        {
            ManagementScope scope = new ManagementScope(@"root\virtualization\v2");
            ManagementBaseObject outParams;

            //remove all current disks first
            List<ManagementObject> currentHDDs = wmiUtilitiesForHyperVImport.GetStorageAllocationsettingData(vm);

            //iterate through all current hdds
            foreach(ManagementObject currentHDD in currentHDDs)
            {
                //get parent (synthetic disk)
                ManagementObjectCollection parent = currentHDD.GetRelated("Msvm_ResourceAllocationsettingData");

                //ignore when there is no synthetic disk
                if (parent.Count == 0)
                {
                    continue;
                }

                using (ManagementObject VMsettings = WmiUtilities.GetVirtualSystemManagementService(scope))
                using (ManagementBaseObject inParams = VMsettings.GetMethodParameters("RemoveResourceSettings"))
                {
                    //delete disk
                    ManagementObject[] resources = new ManagementObject[1];
                    resources[0] = currentHDD;
                    inParams["ResourceSettings"] = resources;
                    outParams = VMsettings.InvokeMethod("RemoveResourceSettings", inParams, null);
      
                    WmiUtilities.ValidateOutput(outParams, scope);

                    //delete synthetic parent disk

                    //get enumerator and just take first element
                    var enumerator = parent.GetEnumerator();
                    enumerator.MoveNext();

                    resources[0] = (ManagementObject)enumerator.Current;
                    inParams["ResourceSettings"] = resources;
                    outParams = VMsettings.InvokeMethod("RemoveResourceSettings", inParams, null);

                    WmiUtilities.ValidateOutput(outParams, scope);
                }
                    
            }


            //get all vhdx files within basepath
            string hddPath = System.IO.Path.Combine(basePath, "Virtual Hard Disks");
            string[] vhdxFiles = System.IO.Directory.GetFiles(hddPath, "*.vhdx");

            
            ManagementObject settings = WmiUtilities.GetVirtualMachineSettings(vm);



            foreach (string vhdxFile in vhdxFiles)
            {

                //Build SyntheticDisk
                ManagementObject synthetic = wmiUtilitiesForHyperVImport.GetResourceAllocationsettingDataDefault(scope, ResourceSubType.DiskSynthetic);
                string syntheticDiskPath;

                //add synthetic disk to vm
                using (ManagementObject VMsettings = WmiUtilities.GetVirtualSystemManagementService(scope))
                using (ManagementBaseObject inParams = VMsettings.GetMethodParameters("AddResourceSettings"))
                {
                    string[] resources = new string[1];
                    resources[0] = synthetic.GetText(TextFormat.CimDtd20);

                    inParams["AffectedConfiguration"] = settings.Path.Path;

                    inParams["ResourceSettings"] = resources;
                    outParams = VMsettings.InvokeMethod("AddResourceSettings", inParams, null);
                    syntheticDiskPath = ((string[])outParams["ResultingResourceSettings"])[0];
                }

                //build VirtualHardDisk
                ManagementObject hardDisk = wmiUtilitiesForHyperVImport.GetResourceAllocationsettingDataDefault(scope, ResourceSubType.VirtualDisk);
                string[] hostResourcesArray = new string[1];
                hostResourcesArray[0] = vhdxFile;
                hardDisk["Parent"] = syntheticDiskPath;
                hardDisk["HostResource"] = hostResourcesArray;


                //add virtual hard disk to vm
                using (ManagementObject VMsettings = WmiUtilities.GetVirtualSystemManagementService(scope))
                using (ManagementBaseObject inParams = VMsettings.GetMethodParameters("AddResourceSettings"))
                {
                    string[] resourceSettingsArray = new string[1];
                    resourceSettingsArray[0] = hardDisk.GetText(TextFormat.CimDtd20);
                    inParams["AffectedConfiguration"] = settings.Path.Path;
                    inParams["ResourceSettings"] = resourceSettingsArray;
                    VMsettings.InvokeMethod("AddResourceSettings", inParams, null);
                }


                //ManagementObject[] hdds = WmiUtilities.GetVhdSettings(vm);
            }
            
        }

        //renames a virtual machine
        private static void renameVM(ManagementObject realizedVM, string newName)
        {
            ManagementObject settings = WmiUtilities.GetVirtualMachineSettings(realizedVM);
            settings["ElementName"] = newName;

            ManagementScope scope = new ManagementScope(@"root\virtualization\v2");

            using (ManagementObject VMsettings = WmiUtilities.GetVirtualSystemManagementService(scope))
            using (ManagementBaseObject inParams = VMsettings.GetMethodParameters("ModifySystemSettings"))
            {
                inParams["SystemSettings"] = settings.GetText(TextFormat.CimDtd20);
                VMsettings.InvokeMethod("ModifySystemSettings", inParams, null);
            }

        }


        //gets the management object from the import outputParameters
        private static ManagementObject GetImportedVM(ManagementBaseObject outputParameters)
        {
            ManagementObject pvm = null;
            ManagementScope scope = new ManagementScope(@"root\virtualization\v2");

            if (WmiUtilities.ValidateOutput(outputParameters, scope))
            {
                if ((uint)outputParameters["ReturnValue"] == 0)
                {
                    pvm = new ManagementObject((string)outputParameters["ImportedSystem"]);
                }

                if ((uint)outputParameters["ReturnValue"] == 4096)
                {
                    using (ManagementObject job = new ManagementObject((string)outputParameters["Job"]))
                    using (ManagementObjectCollection pvmCollection = job.GetRelated("Msvm_PlannedComputerSystem", "Msvm_AffectedJobElement", null, null, null, null, false, null))
                    {
                        pvm = WmiUtilities.GetFirstObjectFromCollection(pvmCollection);
                    }

                }
            }

            return pvm;
        }


        //realizes a planned VM
        private static ManagementObject realizePlannedVM(string vmID)
        {
            ManagementObject vm = null;
            ManagementScope scope = new ManagementScope(@"root\virtualization\v2");

            using (ManagementObject pvm = WmiUtilities.GetPlannedVirtualMachine(vmID, scope))
            using (ManagementObject managementService = WmiUtilities.GetVirtualMachineManagementService(scope))
            using (ManagementBaseObject inParams =
                managementService.GetMethodParameters("RealizePlannedSystem"))
            {
                inParams["PlannedSystem"] = pvm.Path;

                using (ManagementBaseObject outParams =
                    managementService.InvokeMethod("RealizePlannedSystem", inParams, null))
                {
                    if (WmiUtilities.ValidateOutput(outParams, scope, true, true))
                    {
                        using (ManagementObject job =
                            new ManagementObject((string)outParams["Job"]))
                        using (ManagementObjectCollection pvmCollection =
                            job.GetRelated("Msvm_ComputerSystem",
                                "Msvm_AffectedJobElement", null, null, null, null, false, null))
                        {
                            vm = WmiUtilities.GetFirstObjectFromCollection(pvmCollection);
                        }
                    }
                }
            }

            return vm;
        }

    }
}
