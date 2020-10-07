using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace RestoreHelper
{
    /// <summary>
    /// Interaktionslogik für RestoreOptions.xaml
    /// </summary>
    public partial class RestoreOptions : Window
    {
        private ConfigHandler.OneJob job;
        List<RestorePointForGUI> restorePoints;

        public RestoreOptions(ConfigHandler.OneJob job)
        {
            InitializeComponent();
            this.job = job;
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //form not loaded yet? cancel
            if (cbVMs == null)
            {
                return;
            }

            //load restore points
            //build source path
            string sourcePath = this.job.BasePath + "\\" + this.job.Name + "\\" + ((ComboBoxItem)cbVMs.SelectedItem).Tag.ToString();
            if (!System.IO.Directory.Exists(sourcePath))
            {
                MessageBox.Show("Backups können nicht geladen werden!", "Fehler beim Laden", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            //read backup chain
            List<ConfigHandler.BackupConfigHandler.BackupInfo> backups = ConfigHandler.BackupConfigHandler.readChain(sourcePath);

            this.restorePoints = new List<RestorePointForGUI>();
            //fill backup list
            foreach(ConfigHandler.BackupConfigHandler.BackupInfo backup in backups)
            {
                string timeStamp = DateTime.ParseExact(backup.timeStamp, "yyyyMMddHHmmssfff", null).ToString("dd.MM.yyy HH:mm");
                RestorePointForGUI restorePoint = new RestorePointForGUI();
                restorePoint.Date = timeStamp;
                restorePoint.InstanceId = backup.instanceID;
                
                switch (backup.type)
                {
                    case "full":
                        restorePoint.Type = "Vollsicherung";
                        break;
                    case "rct":
                        restorePoint.Type = "Inkrementiell";
                        break;
                    case "lb":
                        restorePoint.Type = "LiveBackup";
                        break;
                }
                
                restorePoints.Add(restorePoint);
            }

            lvRestorePoints.ItemsSource = restorePoints;

            //select first item if possible
            if (lvRestorePoints.Items.Count > 0)
            {
                lvRestorePoints.SelectedIndex = 0;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //load vms within the given job
            foreach (Common.JobVM vm in this.job.JobVMs)
            {
                ComboBoxItem newItem = new ComboBoxItem();
                newItem.Content = vm.vmName;
                newItem.Tag = vm.vmID;
                cbVMs.Items.Add(newItem);
            }
            cbVMs.SelectedIndex = 0;
        }


        //start restore process
        private void btStartRestore_Click(object sender, RoutedEventArgs e)
        {
            if (cbVMs.SelectedItem == null)
            {
                return;
            }

            //get selected restorepoint
            RestorePointForGUI restorePoint = (RestorePointForGUI)lvRestorePoints.SelectedItem;
            

            string sourcePath = this.job.BasePath + "\\" + this.job.Name + "\\" + ((ComboBoxItem)cbVMs.SelectedItem).Tag.ToString();

            //get requested restore type
            string restoreType = ((ComboBoxItem)cbRestoreType.SelectedItem).Tag.ToString();

            switch (restoreType)
            {
                case "full":
                case "fullImport":
                    //look for selected job object
                    string vmId = ((ComboBoxItem)cbVMs.SelectedItem).Tag.ToString();
                    Common.JobVM targetVM = new Common.JobVM();
                    foreach(Common.JobVM vm in this.job.JobVMs)
                    {
                        if (vm.vmID == vmId)
                        {
                            targetVM = vm;
                            break;
                        }
                    }

                    //import to HyperV?
                    bool importToHyperV = false;
                    if (restoreType == "fullImport")
                    {
                        importToHyperV = true;
                    }

                    int jobExecutionId = Common.DBQueries.addJobExecution(this.job.DbId, "restore");
                    RestoreHelper.FullRestoreHandler fullRestoreHandler = new RestoreHelper.FullRestoreHandler(new Common.EventHandler(targetVM, jobExecutionId));
                    fullRestoreHandler.performFullRestoreProcess(sourcePath, "f:\\target", restorePoint.InstanceId, importToHyperV);
                    break;
                case "flr":
                    RestoreHelper.FileLevelRestoreHandler flrHandler = new RestoreHelper.FileLevelRestoreHandler();
                    flrHandler.performGuestFilesRestore(sourcePath, restorePoint.InstanceId);
                    break;
            }

            
            
        }

        //backup structure for listview
        private class RestorePointForGUI
        {
            public string Date { get; set; }
            public string Type { get; set; }
            public string InstanceId { get; set; }
        }
    }
}
