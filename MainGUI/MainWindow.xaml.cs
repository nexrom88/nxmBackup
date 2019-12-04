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

namespace MainGUI
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            FillDataGridJobs();
        }

        //
        private void FillDataGridJobs()
        {
            List<Job> jobList = new List<Job>();

            jobList.Add(new Job() { Name = "Job 1", Type = "täglich", CurrentStatus = "angehalten", LastRunSuccessful = false, NextRun = Convert.ToDateTime("10.12.2019"), LastRun = Convert.ToDateTime("03.12.2019") });
            jobList.Add(new Job() { Name = "Job 2", Type = "wöchentlich", CurrentStatus = "läuft", LastRunSuccessful = false, NextRun = Convert.ToDateTime("12.12.2019"), LastRun = Convert.ToDateTime("01.12.2019") });
            dgJobs.ItemsSource = jobList;
           
        }   

    }
}
