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

namespace nxmBackupGUI
{
    /// <summary>
    /// Interaktionslogik für JobDetailsWindow.xaml
    /// </summary>
    public partial class JobDetailsWindow : Window
    {

        bool windowLoaded = false;
        bool incremental;
        uint blockSize;
        string rotationType;
        uint maxElements;

        public JobDetailsWindow(bool incremental, uint blockSize, string rotationType, uint maxElements)
        {
            InitializeComponent();

            //initialize default values
            this.incremental = incremental;
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
            cbIncrements.IsChecked = this.incremental;
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

        private void cbIncrements_Checked(object sender, RoutedEventArgs e)
        {
            cbBlockSize.IsEnabled = true;
            cbRotationType.IsEnabled = true;
            if (((ComboBoxItem)cbRotationType.SelectedItem).Uid == "blockrotation")
            {
                lblBlocksCaption.Content = "Anzahl aufzubewahrender Blöcke:";
            }
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

    }
}
