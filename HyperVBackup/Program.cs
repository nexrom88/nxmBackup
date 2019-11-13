

namespace HyperVBackup
{
    class Backup
    {
        static void Main(string[] args)
        {
            string vmName = args[0];
            string destination = args[1];
            BackupHandler.doBackup(vmName, destination);
        }
    }
}