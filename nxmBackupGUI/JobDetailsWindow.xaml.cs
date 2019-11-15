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

        public JobDetailsWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //build block size combobox
            for (int i = 1; i <= 20; i++)
            {
                cbBlockSize.Items.Add(i);
            }
            cbBlockSize.SelectedIndex = 6;
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
