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

namespace nxmBackup
{
    /// <summary>
    /// Interaktionslogik für JobDetailsWindow.xaml
    /// </summary>
    public partial class JobDetailsWindow : Window
    {

        bool windowLoaded = false;
        bool lb;
        int blockSize;
        string rotationType;
        int maxElements;

        public JobDetailsWindow(int blockSize, string rotationType, int maxElements)
        {
            InitializeComponent();

            //initialize default values
            this.lb = false;
            this.blockSize = blockSize;
            this.rotationType = rotationType;
            this.maxElements = maxElements;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //build block size combobox
            for (int i = 1; i <= 20; i++)
            {
                ComboBoxItem item = new ComboBoxItem();
                item.Content = i;
                cbBlockSize.Items.Add(item);
            }

            //set default settings
            cbLB.IsChecked = false;
            cbBlockSize.SelectedIndex = (int)this.blockSize - 1;

            for (int i = 0; i < cbRotationType.Items.Count; i++)
            {
                if ( ((ComboBoxItem)cbRotationType.Items[i]).Uid == this.rotationType)
                {
                    cbRotationType.SelectedIndex = i;
                    break;
                }
            }

            slMaxElements.Value = this.maxElements;

            windowLoaded = true;
        }

        private void cbIncrements_Unchecked(object sender, RoutedEventArgs e)
        {
            cbBlockSize.IsEnabled = false;
            cbRotationType.IsEnabled = false;
            lblBlocksCaption.Content = "Anzahl aufzubewahrender Backups:";
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void cbRotationType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!windowLoaded)
            {
                return;
            }

            switch (((ComboBoxItem)cbRotationType.SelectedItem).Uid)
            {
                case "merge":
                    lblBlocksCaption.Content = "Anzahl aufzubewahrender Backups:";
                    break;
                case "blockrotation":
                    lblBlocksCaption.Content = "Anzahl aufzubewahrender Blöcke:";
                    break;
            }
        }

        private void cbEncryption_Click(object sender, RoutedEventArgs e)
        {
            txtEncKey.IsEnabled = (bool)cbEncryption.IsChecked;

        }
    }
}
