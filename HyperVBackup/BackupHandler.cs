using System;
using System.IO;
using System.Management;
using Alphaleonis.Win32.Vss;
using System.Collections.Generic;
using System.Linq;

namespace HyperVBackup
{
    class BackupHandler
    {
        private static readonly object syncLock = new object();

        public static void doBackup(string vmName, string destination)
        {
            lock (syncLock)
            {

                BackupHelpers helper = new BackupHelpers();
                VSSHelper.WMIHelper.OneVM vmData = VSSHelper.WMIHelper.GetVMData(vmName);

                var vssImpl = VssUtils.LoadImplementation();
                var vss = vssImpl.CreateVssBackupComponents();
                Guid hypervVSSGuid = new Guid("66841cd4-6ded-4f4b-8f17-fd23f8ddc3de");

                vss.InitializeForBackup(null);
                vss.SetBackupState(true, true, VssBackupType.Full, false);
                vss.SetContext(VssSnapshotContext.Backup);

                // Add Hyper-V writer
                var hyperVwriterGuid = new Guid("66841cd4-6ded-4f4b-8f17-fd23f8ddc3de");
                vss.EnableWriterClasses(new Guid[] { hyperVwriterGuid });

                vss.GatherWriterMetadata();

                IList<IVssWMComponent> components = new List<IVssWMComponent>();
                // key: volumePath, value: volumeName. These values are equivalent on a standard volume, but differ in the CSV case  
                // StringComparer.InvariantCultureIgnoreCase requiered to fix duplicate Keys with different case error
                IDictionary<string, string> volumeMap =
                    new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

                var wm = vss.WriterMetadata.FirstOrDefault(o => o.WriterId.Equals(hyperVwriterGuid));
                foreach (var component in wm.Components)
                {
                    if (vmData.id == component.ComponentName)
                    {
                        components.Add(component);
                        vss.AddComponent(wm.InstanceId, wm.WriterId, component.Type, component.LogicalPath,
                            component.ComponentName);
                        foreach (var file in component.Files)
                        {
                            string volumeName;
                            string volumePath;

                            volumePath = Path.GetPathRoot(file.Path).ToUpper();
                            volumeName = volumePath;


                            if (!volumeMap.ContainsKey(volumePath))
                            {
                                volumeMap.Add(volumePath, volumeName);
                            }
                        }
                    }
                }

                if (components.Count > 0)
                {
                    var vssSet = vss.StartSnapshotSet();

                    // Key: volumeName, value: snapshotGuid
                    IDictionary<string, Guid> snapshots = new Dictionary<string, Guid>();

                    foreach (var volumeName in volumeMap.Values)
                        snapshots.Add(volumeName, vss.AddToSnapshotSet(volumeName, Guid.Empty));


                    vss.PrepareForBackup();

                    Console.Write("creating snapshot...");
                    vss.DoSnapshotSet();
                    Console.WriteLine("done");

                    // key: volumeName, value: snapshotVolumePath 
                    //IDictionary<string, string> snapshotVolumeMap = new Dictionary<string, string>();

                    //foreach (var kv in snapshots)
                    //  snapshotVolumeMap.Add(kv.Key, vss.GetSnapshotProperties(kv.Value).SnapshotDeviceObject);

                    Console.WriteLine("starting backup transfer");
                    helper.copyBackup(components, destination);

                    foreach (var component in components)
                        vss.SetBackupSucceeded(wm.InstanceId, wm.WriterId, component.Type, component.LogicalPath,
                            component.ComponentName, true);

                    vss.BackupComplete();

                    //RaiseEvent(EventAction.DeletingSnapshotSet, components, volumeMap);
                    vss.DeleteSnapshotSet(vssSet, true);
                }

                Console.WriteLine("backup successfull");
                Console.Read();
            }
        }

    }
}
