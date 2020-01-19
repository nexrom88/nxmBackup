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

        public MainWindow()
        {
            InitializeComponent();

            //start job engine
            this.jobHandler = new JobEngine.JobHandler();
            jobHandler.startJobEngine(new Common.Job.newEventDelegate(newEvent));
            jobs = ConfigHandler.JobConfigHandler.readJobs();
 
            FillListViewJobs();
            StartTimerForJobStatusCheck();

        }

        //
        private void FillListViewJobs()
        {
            foreach (ConfigHandler.OneJob job in jobs)
            {
                ListViewItem item = new ListViewItem(); item.sub
                lvJobs.Items.Add($"{job.Name},{job.IntervalBaseForGUI}");
            }

        }

        //
        private void btnStartJob_Click(object sender, RoutedEventArgs e)
        {
            // Manually trigger the selected job.
            string name = ((Job)lvJobs.SelectedItem).Name;
            Thread jobThread = new Thread(() => this.jobHandler.startManually(name));
            jobThread.Start();
        }

        //
        private void newEvent(Common.EventProperties props)
        {
            Console.WriteLine(props.text);
        }

        private void btnNewJob_Click(object sender, RoutedEventArgs e)
        {
            AddJobWindow addJobWindow = new AddJobWindow();
            addJobWindow.ShowDialog();


        }

        //
        private void StartTimerForJobStatusCheck()
        {
            System.Timers.Timer t1 = new System.Timers.Timer();
            t1.Interval = 5000;
            t1.Elapsed += new ElapsedEventHandler(JobStatusTimerEvent);
            t1.Start();
        }

        private void JobStatusTimerEvent(object sender, EventArgs e)
        {

        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            var Name = jobs[0];
            Name.Name = "Penis";
            jobs[0] = Name;

        }
    }
}
