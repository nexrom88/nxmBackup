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
using ConfigHandler;

namespace nxmBackup.SubGUIs
{
    /// <summary>
    /// Interaktionslogik für LBDatePickerWindow.xaml
    /// </summary>
    public partial class LBDatePickerWindow : Window
    {
        private ConfigHandler.BackupConfigHandler.BackupInfo targetBackup;
        private string basePath;
        private string targetHDD;

        public BackupConfigHandler.BackupInfo TargetBackup { get => targetBackup; set => targetBackup = value; }
        public string BasePath { get => basePath; set => basePath = value; }
        public string TargetHDD { get => targetHDD; set => targetHDD = value; }

        public LBDatePickerWindow()
        {
            InitializeComponent();
        }


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

            //get lb file
            string vmBasePath = System.IO.Path.Combine(this.basePath, this.targetBackup.uuid + ".nxm\\");
            string lbFile = System.IO.Path.Combine(vmBasePath, this.targetHDD + ".lb");
        }
    }
}
