#remove webpage within iis
Remove-WebSite -Name "nxmBackup"

#remove iis apppool
Remove-WebAppPool -Name "nxm"

#stop minifilter
fltmc unload nxmmf

#remove minifilter
Get-CimInstance Win32_SystemDriver -Filter "name='nxmmf'" | Invoke-CimMethod -MethodName Delete

#remove local user
Remove-LocalUser -Name "nxmUser"