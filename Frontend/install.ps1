function Get-RandomCharacters($length, $characters) { 
    $random = 1..$length | ForEach-Object { Get-Random -Maximum $characters.length } 
    $private:ofs="" 
    return [String]$characters[$random]
}

function Randomize-String([string]$inputString){     
    $characterArray = $inputString.ToCharArray()   
    $scrambledStringArray = $characterArray | Get-Random -Count $characterArray.Length     
    $outputString = -join $scrambledStringArray
    return $outputString 
}

$installPath = (Get-Location).Path


#add firewall rule
if (!(Get-NetFirewallRule -DisplayName "nxmBackup").Enabled){
    New-NetFirewallRule -DisplayName "nxmBackup" -Direction inbound -Profile Any -Action Allow -LocalPort 8008 -Protocol TCP
}

#add new user if necessary
$password = Get-RandomCharacters -length 5 -characters 'abcdefghiklmnoprstuvwxyz'
$password += Get-RandomCharacters -length 1 -characters 'ABCDEFGHKLMNOPRSTUVWXYZ'
$password += Get-RandomCharacters -length 1 -characters '1234567890'
$password += Get-RandomCharacters -length 1 -characters '!"�$%&/()=?}][{@#*+'
$password = Randomize-String $password
$PasswordEnc = $password | ConvertTo-SecureString -AsPlainText -Force
if(!(Get-LocalUser 'nxmUser').Enabled){
    New-LocalUser "nxmUser" -Password $PasswordEnc -Description "nxm service account" -PasswordNeverExpires
}else{
    #user already exists, change password
    $UserAccount = Get-LocalUser -Name "nxmUser"
    $UserAccount | Set-LocalUser -Password $PasswordEnc
}


#add user to admin group
Add-LocalGroupMember -Group "Administratoren" -Member "nxmUser"
Add-LocalGroupMember -Group "Administrators" -Member "nxmUser"

#install iis features
Install-WindowsFeature -Name Web-Server
Install-WindowsFeature -Name Web-Mgmt-Tools
Install-WindowsFeature Web-Asp-Net45
Install-WindowsFeature Web-AppInit

$appPool = New-WebAppPool -Name "nxm"
$appPool.processModel.userName = "nxmUser"
$appPool.processModel.password = $password
$appPool.processModel.identityType = 3
$appPool.processModel.idleTimeout = "00:00:00"
$appPool.Recycling.periodicRestart.time = "0.00:00:00"
$appPool.startMode = 1
$appPool | Set-Item

#import site
Import-Module WebAdministration
New-WebSite -Name "nxmbackup" -PhysicalPath $installPath -ApplicationPool "nxm" -Port 8008

Set-WebConfigurationProperty -filter /system.WebServer/security/authentication/AnonymousAuthentication -name enabled -value "True" -PSPath IIS:\ -location nxmbackup
Set-WebConfigurationProperty -filter /system.webServer/security/authentication/AnonymousAuthentication -name username -value "nxmUser" -PSPath IIS:\ -location nxmbackup
Set-WebConfigurationProperty -filter /system.WebServer/security/authentication/AnonymousAuthentication -name password -value $password -PSPath IIS:\ -location nxmbackup

Import-Module WebAdministration
Set-ItemProperty "IIS:\Sites\nxmBackup" -Name applicationDefaults.preloadEnabled -Value True

Start-WebSite -Name "nxmbackup"