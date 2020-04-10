using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MainGUI
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private delegate void UpdateEvents(List<Dictionary<string,string>> events);
        JobEngine.JobHandler jobHandler;
        List<ConfigHandler.OneJob> jobs = new List<ConfigHandler.OneJob>();
        ObservableCollection<ConfigHandler.OneJob> jobsObservable = new ObservableCollection<ConfigHandler.OneJob>();
        System.Threading.Timer eventRefreshTimer;
        int selectedJobId = -1;
        string selectedVMId = "";

        public MainWindow()
        {
            InitializeComponent();

            initJobs();

            fillListViewJobs();

        }

        //init jobs
        private void initJobs()
        {
            //start job engine
            this.jobHandler = new JobEngine.JobHandler();
            jobHandler.startJobEngine();
            jobs = ConfigHandler.JobConfigHandler.readJobs();

            jobsObservable.Clear();

            //build observable job list for GUI
            foreach (ConfigHandler.OneJob job in jobs)
            {
                this.jobsObservable.Add(job);
            }


        }

        //
        private void fillListViewJobs()
        {
            lvJobs.ItemsSource = this.jobsObservable;

        }

        //
        private void btnStartJob_Click(object sender, RoutedEventArgs e)
        {
            // Manually trigger the selected job.
            int dbId = ((ConfigHandler.OneJob)lvJobs.SelectedItem).DbId;

            Thread jobThread = new Thread(() => this.jobHandler.startManually(dbId));
            jobThread.Start();
        } 

        private void btnNewJob_Click(object sender, RoutedEventArgs e)
        {
            AddJobWindow addJobWindow = new AddJobWindow();
            addJobWindow.ShowDialog();
            initJobs();
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {

        }

        private void btnDeleteJob_Click(object sender, RoutedEventArgs e)
        {
            if (lvJobs.SelectedIndex != -1)
            {
                ConfigHandler.OneJob job = this.jobsObservable[lvJobs.SelectedIndex];
                bool result = ConfigHandler.JobConfigHandler.deleteJob(job.DbId);
                if (!result) Common.EventHandler.writeToLog("job delete failed", new System.Diagnostics.StackTrace());
                initJobs();
            }
        }

        //gets triggered when a job gets selected
        private void lvJobs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //start event refresh timer if not laready done
            if (this.eventRefreshTimer == null)
            {
                this.eventRefreshTimer = new System.Threading.Timer(_ => loadEvents(), null, 3000, 3000);
            }

            //clear list
            lvVMs.Items.Clear();

            int dbId = ((ConfigHandler.OneJob)lvJobs.SelectedItem).DbId;
            
            ConfigHandler.OneJob currentJob = (ConfigHandler.OneJob)lvJobs.SelectedItem;
            List<ConfigHandler.JobVM> vms = currentJob.JobVMs;

            //iterate through all vms
            foreach(ConfigHandler.JobVM vm in vms)
            {
                ListViewItem newItem = new ListViewItem();
                newItem.Content = vm.vmName;
                newItem.Tag = vm.vmID;
                lvVMs.Items.Add(newItem);
            }

            this.selectedJobId = dbId;
        }

        //callback for refreshing job events
        private void loadEvents()
        {
            //just load events if a job is selected
            if (this.selectedJobId > -1)
            {
                List<Dictionary<string, string>> events = Common.DBQueries.getEvents(this.selectedJobId.ToString());

                lvEvents.Dispatcher.Invoke(new UpdateEvents(this.UpdateEventList), new object[] { events });

            }
        }

        //gets triggered when the VM selection changes
        private void lvVMs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.selectedVMId = ((ListViewItem)lvVMs.SelectedItem).Tag.ToString();
        }

        //updates the event ListView within GUI thread
        private void UpdateEventList(List<Dictionary<string, string>> events)
        {
            lvEvents.Items.Clear();
            foreach (Dictionary<string, string> oneEvent in events)
            {
                if (oneEvent["vmId"] == this.selectedVMId)
                {
                    lvEvents.Items.Add(oneEvent["info"]);
                }
            }
        }

    }
}