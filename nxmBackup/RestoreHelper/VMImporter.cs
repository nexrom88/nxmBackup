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

namespace RestoreHelper
{
    public class VMImporter
    {
        public static void importVM(string vmDefinitionPath, string snapshotFolderPath, bool newId)
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
                inParams["SnapshotFolder"] = snapshotFolderPath;
                inParams["GenerateNewSystemIdentifier"] = newId;

                using (ManagementBaseObject outParams =
                    managementService.InvokeMethod("ImportSystemDefinition", inParams, null))
                {
                    //get the imported planned vm
                    ManagementObject vm =  GetImportedVM(outParams);
                    string vmID = vm["Name"].ToString();

                    //realize the planned vm
                    ManagementObject realizedVM =  realizePlannedVM(vmID);

                    //rename the vm
                    renameVM(realizedVM);
                }
            }
        }

        //renames a virtual machine
        private static void renameVM(ManagementObject realizedVM)
        {
            ManagementObject settings = WmiUtilities.GetVirtualMachineSettings(realizedVM);
            settings["ElementName"] = "Testo";

            ManagementObject vm = null;
            ManagementScope scope = new ManagementScope(@"root\virtualization\v2");

            using (ManagementObject VMsettings = WmiUtilities.GetVirtualSystemManagementService(scope))
            {

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
