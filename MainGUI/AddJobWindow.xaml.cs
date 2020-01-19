﻿using System;
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

namespace MainGUI
{
    /// <summary>
    /// Interaktionslogik für AddJobWindow.xaml
    /// </summary>
    public partial class AddJobWindow : Window
    {
        private bool windowReady = false;
        private ConfigHandler.Rotation rotation = new ConfigHandler.Rotation();
        private bool incrementalActivated = true;
        private uint blockSize = 2;

        public AddJobWindow()
        {
            InitializeComponent();
        }

        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            //build minutes for combo box
            for (int i = 0; i < 60; i++)
            {
                cbMinutes.Items.Add(i);
            }
            cbMinutes.SelectedIndex = 0;

            //build hours for combo box
            for (int i = 0; i < 24; i++)
            {
                cbHours.Items.Add(i);
            }
            cbHours.SelectedIndex = 0;

            //load vms
            List<Common.WMIHelper.OneVM> vms = Common.WMIHelper.listVMs();

            if (vms != null)
            {
                foreach (Common.WMIHelper.OneVM vm in vms)
                {
                    ListBoxItem lbItem = new ListBoxItem();
                    lbItem.Content = vm.name;
                    lbItem.Uid = vm.id;
                    lbAvailableVMs.Items.Add(lbItem);
                }
            }

            windowReady = true;
        }

        private void cbInterval_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

            //interval changed -> enable/disbale inputs

            //cancel if window not yet ready
            if (!this.windowReady)
            {
                return;
            }

            switch(((ComboBoxItem)cbInterval.SelectedItem).Uid)
            {
                case "hourly":
                    cbMinutes.IsEnabled = true;
                    cbHours.IsEnabled = false;
                    cbDays.IsEnabled = false;
                    break;
                case "daily":
                    cbMinutes.IsEnabled = true;
                    cbHours.IsEnabled = true;
                    cbDays.IsEnabled = false;
                    break;
                case "weekly":
                    cbMinutes.IsEnabled = true;
                    cbHours.IsEnabled = true;
                    cbDays.IsEnabled = true;
                    break;
            }
        }

        private void btAdd_Click(object sender, RoutedEventArgs e)
        {
            //move vm to job
            ListBoxItem selectedItem = (ListBoxItem)lbAvailableVMs.SelectedItem;
            ListBoxItem item = new ListBoxItem();
            item.Content = selectedItem.Content;
            item.Uid = selectedItem.Uid;
            lbAvailableVMs.Items.Remove(selectedItem);
            lbSelectedVMs.Items.Add(item);
        }

        private void btRemove_Click(object sender, RoutedEventArgs e)
        {
            //remove vm from job
            ListBoxItem selectedItem = (ListBoxItem)lbSelectedVMs.SelectedItem;
            ListBoxItem item = new ListBoxItem();
            item.Content = selectedItem.Content;
            item.Uid = selectedItem.Uid;
            lbSelectedVMs.Items.Remove(selectedItem);
            lbAvailableVMs.Items.Add(item);
        }

        private void btAddJob_Click(object sender, RoutedEventArgs e)
        {
            //add the job to jobs xml
            lblError.Content = "";

            //first check that everything is ok
            if (lbSelectedVMs.Items.Count == 0) //no vms selected
            {
                lblError.Content = "Keine virtuelle Maschine ausgewählt!";
                return;
            }

            if(txtJobName.Text == "") //no job name defined
            {
                lblError.Content = "Es wurde kein Jobname vergeben!";
                return;
            }

            if (txtPath.Text == "") //no backup target specified
            {
                lblError.Content = "Es wurde kein Sicherungsziel ausgewählt!";
                return;
            }

            //check that jobname does not already exist
            List<ConfigHandler.OneJob> jobs = ConfigHandler.JobConfigHandler.readJobs();
            bool nameFound = false;
            foreach(ConfigHandler.OneJob j in jobs)
            {
                if (j.Name.ToLower() == txtJobName.Text.ToLower())
                {
                    nameFound = true;
                    break;
                }
            }
            if (nameFound) //job found
            {
                lblError.Content = "Dieser Job existiert bereits!";
                return;
            }

            //build job structure
            ConfigHandler.OneJob job = new ConfigHandler.OneJob();
            job.BasePath = txtPath.Text;
            job.Name = txtJobName.Text;
            job.BlockSize = this.blockSize;
            job.Rotation = this.rotation;

            //build compression var
            switch (((ComboBoxItem)cbCompression.SelectedItem).Uid)
            {
                case "zip":
                    job.Compression = ConfigHandler.Compression.zip;
                    break;
                case "lz4":
                    job.Compression = ConfigHandler.Compression.lz4;
                    break;
            }

            //build interval structure
            ComboBoxItem cbI = (ComboBoxItem)cbInterval.SelectedItem;
            ConfigHandler.Interval jobInterval = new ConfigHandler.Interval();
            switch (cbI.Uid)
            {
                case "hourly":
                    jobInterval.intervalBase = ConfigHandler.IntervalBase.hourly;
                    jobInterval.minute = cbMinutes.Text;
                    break;
                case "daily":
                    jobInterval.intervalBase = ConfigHandler.IntervalBase.daily;
                    jobInterval.minute = cbMinutes.Text;
                    jobInterval.hour = cbHours.Text;
                    break;
                case "weekly":
                    jobInterval.intervalBase = ConfigHandler.IntervalBase.weekly;
                    jobInterval.minute = cbMinutes.Text;
                    jobInterval.hour = cbHours.Text;
                    jobInterval.day = cbDays.Text;
                    break;
            }
            job.Interval = jobInterval;

            //build vm structure
            List<ConfigHandler.JobVM> jobVMs = new List<ConfigHandler.JobVM>();
            foreach (ListBoxItem vm in lbSelectedVMs.Items)
            {
                ConfigHandler.JobVM jobVM = new ConfigHandler.JobVM();
                jobVM.vmName = (string)vm.Content;
                jobVM.vmID = vm.Uid;
                jobVMs.Add(jobVM);
            }
            job.JobVMs = jobVMs;

            ConfigHandler.JobConfigHandler.addJob(job);
            this.Close();

        }

        private void btSelectPath_Click(object sender, RoutedEventArgs e)
        {
            //open path picker dialog
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Wählen Sie einen Sicherungspfad aus:";
                System.Windows.Forms.DialogResult result = dialog.ShowDialog();
                txtPath.Text = dialog.SelectedPath;
            }
        }

        private void btJobDetails_Click(object sender, RoutedEventArgs e)
        {
            JobDetailsWindow detailWindow = new JobDetailsWindow(this.incrementalActivated, this.blockSize, this.rotation.type.ToString().ToLower(), this.rotation.maxElementCount);
            detailWindow.ShowDialog();

            //read set values
            this.incrementalActivated = (bool)detailWindow.cbIncrements.IsChecked;
            this.blockSize = uint.Parse(((ComboBoxItem)detailWindow.cbBlockSize.SelectedItem).Content.ToString());


            switch (((ComboBoxItem)detailWindow.cbRotationType.SelectedItem).Uid)
            {
                case "merge":
                    this.rotation.type = ConfigHandler.RotationType.merge;
                    break;
                case "blockrotation":
                    this.rotation.type = ConfigHandler.RotationType.blockRotation;
                    break;
            }

            this.rotation.maxElementCount = (uint)detailWindow.slMaxElements.Value;

        }
    }
}
