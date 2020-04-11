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

namespace MainGUI.SubGUIs
{
    /// <summary>
    /// Interaktionslogik für RestoreOptions.xaml
    /// </summary>
    public partial class RestoreOptions : Window
    {
        private ConfigHandler.OneJob job;

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
            List<ConfigHandler.BackupConfigHandler.BackupInfo> backups = ConfigHandler.BackupConfigHandler.readChain(this.job.BasePath + "\\" + this.job.Name + "\\" + ((ComboBoxItem)cbVMs.SelectedItem).Tag.ToString());
            backups = null;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //load vms within the given job
            foreach (ConfigHandler.JobVM vm in this.job.JobVMs)
            {
                ComboBoxItem newItem = new ComboBoxItem();
                newItem.Content = vm.vmName;
                newItem.Tag = vm.vmID;
                cbVMs.Items.Add(newItem);
            }
            cbVMs.SelectedIndex = 0;
        }
    }
}
