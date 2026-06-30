#Copy necessary files from the util-linux package into the bin directory of the msysgit installation
#Runs gitflow\contrib\msysgit-install.cmd
#
# NOTE:This script must be executed with elevated priviledges, in case Git is installed below program files
#
Param
(
	[Parameter(Mandatory=$True)] [string] $gitInstallPath
)

$installationPath = Split-Path $MyInvocation.MyCommand.Path
$targetFolder = $installationPath
$binaries = Join-Path $installationPath "binaries"
$gitFlowFolder = Join-Path $installationPath "gitflow"
$gitLocation = Join-Path $gitInstallPath "bin"

# Modern Git for Windows (2.x) uses usr/bin; fall back to bin for older installs
$usrBinLocation = Join-Path $gitInstallPath "usr\bin"
$gitFlowTargetDir = if (Test-Path $usrBinLocation) { $usrBinLocation } else { $gitLocation }

Write-Host "Copy binaries to Git installation directory " + $gitLocation

Copy-Item -Path "$binaries\*.*" -Destination "$gitLocation" -Force -Verbose

#Check if gitflow need to be installed
if(Test-Path (Join-Path $gitLocation "git-flow"))
{
    Write-Host "GitFlow already installed in $gitLocation — checking usr/bin for missing scripts..."
    # Even when git-flow is already installed, ensure git-flow-bugfix is present
    # (older installations may be missing it)
    $bugfixScript = Join-Path $gitFlowFolder "git-flow-bugfix"
    $bugfixTarget = Join-Path $gitFlowTargetDir "git-flow-bugfix"
    if (-not (Test-Path $bugfixTarget))
    {
        Write-Host "Copying missing git-flow-bugfix to $gitFlowTargetDir"
        Copy-Item -Path $bugfixScript -Destination $bugfixTarget -Force -Verbose
    }
    exit
}

#Run gitflow install script
$installScript = Join-Path $installationPath "gitflow\contrib\msysgit-install.cmd"
$pinfo = New-Object System.Diagnostics.ProcessStartInfo
$pinfo.FileName = $installScript
$pinfo.UseShellExecute = $true
$pinfo.Arguments = """$gitInstallPath"""

$p = New-Object System.Diagnostics.Process
$p.StartInfo = $pinfo
$p.Start() | Out-Null
$p.WaitForExit()

# After base install, also copy git-flow-bugfix to usr/bin if needed
$bugfixScript = Join-Path $gitFlowFolder "git-flow-bugfix"
$bugfixTarget = Join-Path $gitFlowTargetDir "git-flow-bugfix"
if (-not (Test-Path $bugfixTarget))
{
    Write-Host "Copying git-flow-bugfix to $gitFlowTargetDir"
    Copy-Item -Path $bugfixScript -Destination $bugfixTarget -Force -Verbose
}

Write-Host "Installation done!"



