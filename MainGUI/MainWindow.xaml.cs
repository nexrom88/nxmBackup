using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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

namespace MainGUI
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        JobEngine.JobHandler jobHandler;

        public MainWindow()
        {
            InitializeComponent();

            //start job engine
            this.jobHandler = new JobEngine.JobHandler();
            jobHandler.startJobEngine(new Common.Job.newEventDelegate(newEvent));

            FillDataGridJobs();

        }

        //
        private void FillDataGridJobs()
        {
            List<ConfigHandler.OneJob> jobs = ConfigHandler.JobConfigHandler.readJobs();

            foreach (ConfigHandler.OneJob job in jobs)
            {
                string interval = "";
                switch (job.interval.intervalBase)
                {
                    case ConfigHandler.IntervalBase.daily:
                        interval = "täglich";
                        break;
                    case ConfigHandler.IntervalBase.hourly:
                        interval = "stündlich";
                        break;
                    case ConfigHandler.IntervalBase.weekly:
                        interval = "wöchentlich";
                        break;
                    default:
                        interval = "";
                        break;
                }

                dgJobs.Items.Add(new Job() { Name = job.name, Type = interval, CurrentStatus = "angehalten", LastRunSuccessful = false, NextRun = Convert.ToDateTime("10.12.2019"), LastRun = Convert.ToDateTime("03.12.2019") });

            }


           
        }

        //
        private void btnStartJob_Click(object sender, RoutedEventArgs e)
        {
            // Manually trigger the selected job.
            string name = ((Job)dgJobs.SelectedItem).Name;
            Thread jobThread = new Thread(() => this.jobHandler.startManually(name));
            jobThread.Start();
        }

        //
        private void newEvent(Common.EventProperties props)
        {
            Console.WriteLine(props.text);
        }
    }
}
