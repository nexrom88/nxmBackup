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
        private GuestFilesHandler gfHandler;
        private string currentPath;
        private delegate void setProgressDelegate(double progress);
        private delegate void setLabelDelegate(string text);
        private bool restoreInProgress = false;

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

            string vhdFile = "C:\\restore\\Virtual Hard Disks\\Windows 10.vhdx";

            gfHandler = new GuestFilesHandler(vhdFile);

            //set callback for restore progress
            gfHandler.newEvent += new Common.Job.newEventDelegate(newEvent);

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

            this.currentPath = path;

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

        //saves a selected file to the local computer
        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            //cancel if restore already in progress
            if (this.restoreInProgress)
            {
                return;
            }

            MenuItem clickedItem = (MenuItem)e.OriginalSource;

            switch (clickedItem.Uid)
            {
                case "saveas": //save as
                    //open path picker dialog
                    using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
                    {
                        //show folder browser dialog
                        dialog.Description = "Wählen Sie einen lokalen Wiederherstellungspfad aus:";
                        System.Windows.Forms.DialogResult result = dialog.ShowDialog();
                        string targetPath = dialog.SelectedPath;

                        //get current path
                        ListBoxItem selectedFile = (ListBoxItem)lbFiles.SelectedItem;
                        string sourcePath = System.IO.Path.Combine(this.currentPath, selectedFile.Content.ToString());

                        string destinationPath = System.IO.Path.Combine(targetPath, selectedFile.Content.ToString());

                        //start restore thread
                        this.restoreInProgress = true;
                        gridProgress.Visibility = Visibility.Visible;

                        System.Threading.Thread restoreThread = new System.Threading.Thread(() => this.gfHandler.restoreFile2Local(sourcePath, destinationPath, true));
                        restoreThread.Start();
                    }
                    break;
            }
        }

        //progress callback
        private void newEvent(Common.EventProperties props)
        {
            if (props.progress < 0.0)
            {
                MessageBox.Show("Wiederherstellung fehlgeschlagen:\r\n" + props.text);
            }
            else
            {
                //no error, show progress
                pbProgress.Dispatcher.Invoke(new setProgressDelegate(setProgress), new object[] { props.progress });

                //refresh label if necessary
                if (props.elementsCount > 0)
                {
                    lblProgress.Dispatcher.Invoke(new setLabelDelegate(setLabel), new object[] { "Fortschritt (Datei " + props.currentElement + " von " + props.elementsCount + "):" });
                }
                else
                {
                    lblProgress.Dispatcher.Invoke(new setLabelDelegate(setLabel), new object[] { "Fortschritt (Datei 1 von 1):" });
                }
            }

            //restore done?
            if (props.setDone)
            {
                this.restoreInProgress = false;
            }
        }

        //sets the current progress
        private void setProgress(double progress)
        {
            pbProgress.Value = progress;
        }

        //sets the current progress label
        private void setLabel(string text)
        {
            lblProgress.Content = text;
        }

        private void TreeViewMenuItem_Click(object sender, RoutedEventArgs e)
        {
            //cancel if restore already in progress
            if (this.restoreInProgress)
            {
                return;
            }

            MenuItem clickedItem = (MenuItem)e.OriginalSource;

            switch (clickedItem.Uid)
            {
                case "savefolderas": //save as
                    //open path picker dialog
                    using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
                    {
                        //get clicked TreeViewItem
                        MenuItem mnu = (MenuItem)(sender);
                        TreeViewItem selectedFolder = ((ContextMenu)mnu.Parent).PlacementTarget as TreeViewItem;

                        //get current path
                        string sourcePath = System.IO.Path.Combine(this.currentPath, selectedFolder.Header.ToString());

                        //set waiting mouse cursor
                        Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

                        //count files within folder
                        List<string> files = getFiles(sourcePath);

                        //reset default mouse cursor
                        Mouse.OverrideCursor = System.Windows.Input.Cursors.Arrow;

                        if (MessageBox.Show("Dieses Verzeichnis enthält " + files.Count + " Dateien.\r\nSollen diese lokal wiederhergestellt werden?", "Verzeichniswiederherstellung", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                        {
                            //user canceled restore
                            return;
                        }

                        //show folder browser dialog
                        dialog.Description = "Wählen Sie einen lokalen Wiederherstellungspfad aus:";
                        System.Windows.Forms.DialogResult result = dialog.ShowDialog();
                        string targetPath = dialog.SelectedPath;

                        

                        string destinationPath = System.IO.Path.Combine(targetPath, selectedFolder.Header.ToString());

                        System.IO.Directory.CreateDirectory(destinationPath);

                        //start restore thread
                        this.restoreInProgress = true;
                        gridProgress.Visibility = Visibility.Visible;

                        System.Threading.Thread restoreThread = new System.Threading.Thread(() => this.gfHandler.restoreFiles2Local(files, destinationPath, sourcePath));
                        restoreThread.Start();
                    }
                    break;
            }
        }

        //gets the files within a given folder recursively
        List<string> getFiles(string directory)
        {
            try
            {

                List<string> files = new List<string>();

                //get folders
                string[] folders = System.IO.Directory.GetDirectories(directory, "*", System.IO.SearchOption.TopDirectoryOnly);

                //iterate folders
                foreach (string folder in folders)
                {
                    files.AddRange(getFiles(folder));
                }

                //get and add files
                files.AddRange(System.IO.Directory.GetFiles(directory, "*", System.IO.SearchOption.TopDirectoryOnly));

                return files;

            }
            catch (Exception ex) //return empty list when error occurs
            {
                return new List<string>();
            }
        }

        private void pbProgress_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }
    }
}
