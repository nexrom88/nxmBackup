# nxmBackup
nxmBackup is a backup software to backup HyperV virtual machines.
nxmBackup is developed and continuously improved by private individuals.
<p align="center">
  <img src="https://nxmbackup.com/logo.png">
</p>

## Feature-Highlights
- HyperV virtual machine backups on Windows Server 2016, Windows Server 2019 and Windows Server 2022 platforms
- Recovery: VMs can be started temporarily directly from the backup without a full recovery. In addition, individual files or the entire VM can be restored
- Application-Awareness: The backups are "application-aware" if the guest operating system supports this. This is to prevent possible data loss when restoring from backups
- Backups can be carried out fully automatically at freely definable intervals. The web interface gives you an overview of all your backup jobs
- Backups can be stored either unencrypted or encrypted using AES256 algorithm to protect them from unauthorized access
- Backups are stored in a space-saving manner using a modern compression method without sacrificing performance
- Backups can be stored on any common NAS storage using SMB or on a local drive
- Agentless: No additional software needs to be installed on the VMs to be backed up. The backups are only created by communicating with the HyperV host

The code compiled and packaged into a setup can be found under the "Releases" section: [Releases](https://github.com/nexrom88/nxmBackup/releases)

For more information, visit us at: [nxmbackup.com](https://nxmbackup.com)

Our wiki with numerous help topics can be found here: [https://nxmbackup.com/wiki_de/doku.php](https://nxmbackup.com/wiki_en/doku.php)
