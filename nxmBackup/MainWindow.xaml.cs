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
using HyperVBackupRCT;
using RestoreHelper;

namespace nxmBackup
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private delegate void UpdateEvents(List<Dictionary<string, object>> events);
        JobEngine.JobHandler jobHandler;
        List<ConfigHandler.OneJob> jobs = new List<ConfigHandler.OneJob>();
        ObservableCollection<ConfigHandler.OneJob> jobsObservable = new ObservableCollection<ConfigHandler.OneJob>();
        System.Threading.Timer eventRefreshTimer;
        int selectedJobId = -1;
        string selectedVMId = "";
        // event lock for vm's because timer thread calls the events and click on vm calls also the events.
        private readonly object eventLock = new object();

        public MainWindow()
        {
            InitializeComponent();

            initJobs();

            fillListViewJobs();

            //cleanUp(); //just for debugging purpose

        }

        //just for debugging purpose:
        //deletes every type of snapshot for a given vm
        private void cleanUp()
        {
            //SnapshotHandler h = new SnapshotHandler("94921741-1567-4C42-84BF-4385F7E4BF9E", -1);
            //h.cleanUp();
        }

        //init jobs
        private void initJobs()
        {
            //start job engine
            this.jobHandler = new JobEngine.JobHandler();

            if (!jobHandler.startJobEngine())
            {
                //db error occured while starting job engine
                MessageBox.Show("Jobsystem kann nicht geladen werden. Datenbankfehler.", "Datenbankfehler", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
                return;
            }

            jobs = ConfigHandler.JobConfigHandler.Jobs;

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

        private void btnRestore_Click(object sender, RoutedEventArgs e)
        {
            if (lvJobs.SelectedItem == null)
            {
                MessageBox.Show("Es wurde kein Job ausgewählt.", "Fehlgeschlagen", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            RestoreOptions restoreOptionsWindows = new RestoreOptions((ConfigHandler.OneJob)lvJobs.SelectedItem);
            restoreOptionsWindows.ShowDialog();
        }

        private void btnDeleteJob_Click(object sender, RoutedEventArgs e)
        {
            if (lvJobs.SelectedIndex != -1)
            {
                ConfigHandler.OneJob job = this.jobsObservable[lvJobs.SelectedIndex];
                bool result = ConfigHandler.JobConfigHandler.deleteJob(job.DbId);
                if (!result) Common.DBQueries.addLog("error on deleting job", Environment.StackTrace, null);
                initJobs();
            }
        }

        //gets triggered when a job gets selected
        private void lvJobs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //a job is selected?
            if (lvJobs.SelectedItem == null)
            {
                this.selectedJobId = -1;
                //nothing selected, stop timer if necessary
                if (this.eventRefreshTimer != null)
                {
                    this.eventRefreshTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    this.eventRefreshTimer.Dispose();
                    this.eventRefreshTimer = null;
                }

                //clear lists
                lvVMs.Items.Clear();
                lvEvents.Items.Clear();
                return;
            }

            //start event refresh timer if not already done
            if (this.eventRefreshTimer == null)
            {
                this.eventRefreshTimer = new System.Threading.Timer(_ => loadEvents(), null, 3000, 3000);
            }

            //clear lists
            lvVMs.Items.Clear();
            lvEvents.Items.Clear();

            int dbId = ((ConfigHandler.OneJob)lvJobs.SelectedItem).DbId;

            ConfigHandler.OneJob currentJob = (ConfigHandler.OneJob)lvJobs.SelectedItem;
            List<Common.JobVM> vms = currentJob.JobVMs;

            //iterate through all vms
            foreach (Common.JobVM vm in vms)
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
            lock (eventLock)
            {
                //just load events if a job is selected
                if (this.selectedJobId > -1)
                {
                    List<Dictionary<string, object>> events = Common.DBQueries.getEvents(this.selectedJobId, "backup");

                    lvEvents.Dispatcher.Invoke(new UpdateEvents(this.UpdateEventList), new object[] { events });

                }
            }
        }

        //gets triggered when the VM selection changes
        private void lvVMs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

            ListViewItem selectedItem = (ListViewItem)lvVMs.SelectedItem;
            if (selectedItem != null)
            {
                this.selectedVMId = selectedItem.Tag.ToString();
                loadEvents();
            }
            else
            {
                this.selectedVMId = "";
            }
        }

        //updates the event ListView within GUI thread
        private void UpdateEventList(List<Dictionary<string, object>> events)
        {
            lvEvents.Items.Clear();
            foreach (Dictionary<string, object> oneEvent in events)
            {
                if (oneEvent["vmid"].ToString() == this.selectedVMId)
                {
                    EventListEntry ele = new EventListEntry();
                    ele.Text = oneEvent["info"].ToString();

                    //select icon
                    switch (oneEvent["status"])
                    {
                        case "successful":
                            ele.Icon = "Graphics/success.png";
                            break;
                        case "inProgress":
                            ele.Icon = "Graphics/inProgress.png";
                            break;
                        case "error":
                            ele.Icon = "Graphics/error.png";
                            break;
                        case "warning":
                        case "info":
                            ele.Icon = "Graphics/warning.png";
                            break;
                    }


                    lvEvents.Items.Insert(0, ele);
                }
            }
        }

        private class EventListEntry
        {
            public string Icon { get; set; }
            public string Text { get; set; }
        }

        private void MainGUI_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            
        }

        private void lvJobs_MouseUp(object sender, MouseButtonEventArgs e)
        {
            this.selectedVMId = "";
        }
    }
}