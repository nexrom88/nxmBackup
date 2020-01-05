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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Management;
using System.Threading;

namespace nxmBackupGUI
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private delegate void setTextDelegate(string text);
        private delegate void setUpdateDelegate(string text);
        private delegate void setDoneDelegate(string text);
        JobEngine.JobHandler jobHandler;

        public MainWindow()
        {
            InitializeComponent();

        }

        private void BtStart_Click(object sender, RoutedEventArgs e)
        {
            ////Common.Job.newEventDelegate newEventDel = newEvent;
            //HyperVBackupRCT.SnapshotHandler ssHandler = new HyperVBackupRCT.SnapshotHandler(cbVMs.Items[cbVMs.SelectedIndex].ToString());
            //ssHandler.newEvent += new Common.Job.newEventDelegate(newEvent);

            //System.IO.Compression.CompressionLevel compressionLevel;

            ////read compression level
            //switch (cbCompression.SelectedIndex)
            //{
            //    case 0:
            //        compressionLevel = System.IO.Compression.CompressionLevel.NoCompression;
            //        break;
            //    case 1:
            //        compressionLevel = System.IO.Compression.CompressionLevel.Fastest;
            //        break;
            //    case 2:
            //        compressionLevel = System.IO.Compression.CompressionLevel.Optimal;
            //        break;
            //    default:
            //        compressionLevel = System.IO.Compression.CompressionLevel.NoCompression;
            //        break;
            //}

            //Thread snapshotThread = new Thread(() => ssHandler.performFullBackupProcess(HyperVBackupRCT.ConsistencyLevel.ApplicationAware, true, "c:\\nxm", true, compressionLevel, 2, 5));

            //snapshotThread.Start();

            
        }

        private void newEvent(Common.EventProperties props)
        {
            if (props.isUpdate) //is update?
            {
                lbEvents.Dispatcher.Invoke(new setUpdateDelegate(setUpdate), new object[] { props.text });
            }else if (props.setDone) //set done?
            {
                lbEvents.Dispatcher.Invoke(new setDoneDelegate(setDone), new object[] { props.text });
            }
            else //just add an event
            {
                lbEvents.Dispatcher.Invoke(new setTextDelegate(setText), new object[] { props.text });
            }
                      
            
        }

        //updates the last event in the list
        private void setUpdate(string text)
        {
            lbEvents.Items[lbEvents.Items.Count - 1] = text;
        }


        //sets the last event in the list to "done"
        private void setDone(string text)
        {
            lbEvents.Items[lbEvents.Items.Count - 1] += text;
        }

        //adds a new event to the list
        private void setText(string text)
        {
            lbEvents.Items.Add(text);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //load the vms
            List<Common.WMIHelper.OneVM> vms = Common.WMIHelper.listVMs();
            
            //error occured?
            if (vms == null)
            {
                return;
            }

            foreach (Common.WMIHelper.OneVM vm in vms)
            {
                cbVMs.Items.Add(vm.name);
            }

            //select the first one
            if (cbVMs.Items.Count > 0)
            {
                cbVMs.SelectedIndex = 0;
            }

            //load jobs
            List<ConfigHandler.OneJob> jobs = ConfigHandler.JobConfigHandler.readJobs();
            
            foreach(ConfigHandler.OneJob job in jobs)
            {
                ComboBoxItem item = new ComboBoxItem();
                item.Content = job.name;
                cbJobs.Items.Add(item);
            }
            if(cbJobs.Items.Count > 0)
            {
                cbJobs.SelectedIndex = 0;
            }

            this.jobHandler = new JobEngine.JobHandler();

        }

        private void BtCleanUp_Click(object sender, RoutedEventArgs e)
        {
            Common.Job.newEventDelegate newEventDel = newEvent;
            HyperVBackupRCT.SnapshotHandler ssHandler = new HyperVBackupRCT.SnapshotHandler(cbVMs.Items[cbVMs.SelectedIndex].ToString());
            ssHandler.newEvent += new Common.Job.newEventDelegate(newEvent);
            ssHandler.cleanUp();
        }

        private void BtRestore_Click(object sender, RoutedEventArgs e)
        {
            HyperVBackupRCT.RestoreHandler restoreHandler = new HyperVBackupRCT.RestoreHandler();
            restoreHandler.newEvent += new Common.Job.newEventDelegate(newEvent);
            Thread restoreThread = new Thread(() => restoreHandler.performFullRestoreProcess(@"C:\Users\Administrator\Desktop\nxm\CentOS Sicherung\CentOS", @"c:\restore", "Microsoft:F4C19004-EAD8-4599-84C6-C8F1D52DB8BD", ConfigHandler.Compression.lz4));
            restoreThread.Start();
        }

        private void btAddJob_Click(object sender, RoutedEventArgs e)
        {
            AddJobWindow addJobWindow = new AddJobWindow();
            addJobWindow.ShowDialog();
        }

        private void btStartJobs_Click(object sender, RoutedEventArgs e)
        {
            //start job engine
            jobHandler.startJobEngine(new Common.Job.newEventDelegate(newEvent));
            btStartJobs.IsEnabled = false;
        }

        private void btStartJob_Click(object sender, RoutedEventArgs e)
        {
            //manually trigger the selected job
            string name = cbJobs.Text;
            Thread jobThread = new Thread(() => this.jobHandler.startManually(name));
            jobThread.Start();
        }
    }
}
