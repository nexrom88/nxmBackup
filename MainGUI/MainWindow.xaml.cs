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
        JobEngine.JobHandler jobHandler;
        List<ConfigHandler.OneJob> jobs = new List<ConfigHandler.OneJob>();
        ObservableCollection<ConfigHandler.OneJob> jobsObservable = new ObservableCollection<ConfigHandler.OneJob>();

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
                if (!result) Common.ErrorHandler.writeToLog("job delete failed", new System.Diagnostics.StackTrace());
                initJobs();
            }
        }
    }
}