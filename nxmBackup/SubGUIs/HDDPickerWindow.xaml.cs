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

namespace nxmBackup.SubGUIs
{
    /// <summary>
    /// Interaktionslogik für HDDPickerWindow.xaml
    /// </summary>
    public partial class HDDPickerWindow : Window
    {
        private string[] baseHDDs;

        private string userPickedHDD;

        public string[] BaseHDDs { get => baseHDDs; set => baseHDDs = value; }
        public string UserPickedHDD { get => userPickedHDD; }

        public HDDPickerWindow()
        {
            InitializeComponent();
        }

        private void btStartRestore_Click(object sender, RoutedEventArgs e)
        {
            this.userPickedHDD = ((ComboBoxItem)(cbHDDs.SelectedItem)).Uid;
            this.Close();
        }

        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            //build hdd combobox
            foreach (string hdd in this.baseHDDs)
            {
                ComboBoxItem item = new ComboBoxItem();
                item.Content = System.IO.Path.GetFileName(hdd);
                item.Uid = hdd;
                cbHDDs.Items.Add(item);
            }

            cbHDDs.SelectedIndex = 0;
        }
    }
}
