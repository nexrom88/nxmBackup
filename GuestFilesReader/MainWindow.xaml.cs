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
using System.Security.Principal;

namespace GuestFilesReader
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        GuestFilesHandler gfHandler;

        public MainWindow()
        {
            InitializeComponent();
        }

        //checks whether the current user is an administrator
        private static bool isAdministrator()
        {
           WindowsIdentity identity = WindowsIdentity.GetCurrent();
           WindowsPrincipal principal = new WindowsPrincipal(identity);
           bool isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
           return isAdmin;
        }



        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            isAdministrator();

            string vhdFile = "H:\\Win10.vhdx";

            gfHandler = new GuestFilesHandler(vhdFile);

            List<GuestVolume> drives = gfHandler.getMountedDrives();

            gfHandler.mountVHD();

            List<GuestVolume> newDrives = gfHandler.getMountedDrives();
            List<GuestVolume> mountedDrives = new List<GuestVolume>();

            foreach (GuestVolume drive in newDrives)
            {
                bool driveFound = false;

                foreach (GuestVolume oldDrive in drives)
                {
                    if (oldDrive.path == drive.path)
                    {
                        driveFound = true;
                        break;
                    }
                }

                if (!driveFound)
                {
                    mountedDrives.Add(drive);

                    //add to combobox
                    ComboBoxItem cbItem = new ComboBoxItem();
                    cbItem.Uid = drive.path;
                    cbItem.Content = drive.caption;
                    cbVolumes.Items.Add(cbItem);
                }

            }
            
            //select first item if available
            if (cbVolumes.Items.Count > 0)
            {
                cbVolumes.SelectedIndex = 0;
            }


        }

        //sets the view to a given path
        private void setPath(string path, TreeViewItem baseItem)
        {
            //build tv with folders and lb with files
            string[] entries;
            try
            {
                //try to read files and folders
                entries = System.IO.Directory.GetFileSystemEntries(path, "*", System.IO.SearchOption.TopDirectoryOnly);
            }
            catch
            {
                entries = new string[0];
            }
            lbFiles.Items.Clear();

            foreach (string entry in entries)
            {
                if (System.IO.File.Exists(entry)) //file, set lb
                {
                    //remove volume name first
                    string fileName = System.IO.Path.GetFileName(entry);

                    ListBoxItem newItem = new ListBoxItem();
                    newItem.Content = fileName;
                    newItem.Uid = entry;
                    lbFiles.Items.Add(newItem);

                }
                else //folder, set tv
                {
                    //remove volume name first
                    string folderName = System.IO.Path.GetFileName(entry);

                    TreeViewItem newItem = new TreeViewItem();
                    newItem.Header = folderName;
                    newItem.Uid = entry;
                    newItem.Expanded += TreeViewItem_Expanded;
                    
                    //add entry to an existing node?
                    if (baseItem != null)
                    {
                        baseItem.Items.Add(newItem);
                    }
                    else //add entry to tv root
                    {
                        tvDirectories.Items.Add(newItem);
                    }
                    
                }
            }
        }


        //user selected another volume
        private void cbVolumes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

            //get volume
            ComboBoxItem selectedVolume = (ComboBoxItem)cbVolumes.SelectedItem;
            string rootPath = selectedVolume.Uid;
            tvDirectories.Items.Clear();
            lbFiles.Items.Clear();

            setPath(selectedVolume.Uid, null);


        }

        //treeview item expanded
        private void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
        {
            TreeViewItem item = (TreeViewItem)(e.OriginalSource);
            setPath(item.Uid, item);
        }

            private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            gfHandler.detach();
        }
    }
}
