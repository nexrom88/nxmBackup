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

$installPath = "c:\users\administrator\Desktop\Release"

#add registry key for base path
$registryPath = "HKLM:\Software\nxmBackup"
$key = try {
    Get-Item -Path $registryPath -ErrorAction Stop
}
catch {
    New-Item -Path $registryPath -Force
}
$valueName = "BasePath"
New-ItemProperty -Path $registryPath -Name $valueName -Value $installPath -PropertyType STRING -Force

#add firewall rule
if (!(Get-NetFirewallRule -DisplayName "nxmBackup").Enabled){
    New-NetFirewallRule -DisplayName "nxmBackup" -Direction inbound -Profile Any -Action Allow -LocalPort 8008 -Protocol TCP
}

#add new user if necessary
$password = Get-RandomCharacters -length 5 -characters 'abcdefghiklmnoprstuvwxyz'
$password += Get-RandomCharacters -length 1 -characters 'ABCDEFGHKLMNOPRSTUVWXYZ'
$password += Get-RandomCharacters -length 1 -characters '1234567890'
$password += Get-RandomCharacters -length 1 -characters '!"§$%&/()=?}][{@#*+'
$password = Randomize-String $password
$PasswordEnc = $password | ConvertTo-SecureString -AsPlainText -Force
if(!(Get-LocalUser 'nxmUser').Enabled){
    New-LocalUser "nxmUser" -Password $PasswordEnc -Description "nxm service account"
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
$appPool = New-WebAppPool -Name "nxm"
$appPool.processModel.userName = "nxmUser"
$appPool.processModel.password = $password
$appPool.processModel.identityType = 3
$appPool | Set-Item

#import site
Import-Module WebAdministration
New-WebSite -Name "nxmbackup" -PhysicalPath $installPath -ApplicationPool "nxm" -Port 8008

Set-WebConfigurationProperty -filter /system.WebServer/security/authentication/AnonymousAuthentication -name enabled -value "True" -Location "IIS:\Sites\nxmbackup"
Set-WebConfigurationProperty -filter /system.WebServer/security/authentication/AnonymousAuthentication -name username -value "nxmUser" -Location "IIS:\Sites\nxmbackup"
Set-WebConfigurationProperty -filter /system.WebServer/security/authentication/AnonymousAuthentication -name password -value $password -Location "IIS:\Sites\nxmbackup"

Start-WebSite -Name "nxmbackup"