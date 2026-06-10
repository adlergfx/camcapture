# Reserve HTTP URL — run once as Administrator

param(
    [string]$Url = "http://+:8083/"
)

#-- require admin
$isAdmin = ([Security.Principal.WindowsPrincipal]
    [Security.Principal.WindowsIdentity]::GetCurrent())
    .IsInRole([Security.Principal.WindowsBuiltInRole]::"Administrator")
if (-not $isAdmin) {
    Write-Error "Run this script as Administrator."
    exit 1
}

#-- resolve current user (with fallback)
$Username = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
if (-not $Username) {
    $Username = "$env:USERDOMAIN\$env:USERNAME"
}
Write-Host "User : $Username"
Write-Host "URL  : $Url"

#-- remove stale reservation if present
netsh http show urlacl url=$Url 2>&1 | Out-Null
if ($LASTEXITCODE -eq 0) {
    Write-Host "Removing existing reservation..."
    netsh http delete urlacl url=$Url 2>&1 | ForEach-Object { Write-Host $_ }
}

#-- add reservation, force immediate output
Write-Host "Adding reservation..."
netsh http add urlacl url=$Url user=$Username 2>&1 | ForEach-Object { Write-Host $_ }

#-- verify by querying back
$check = netsh http show urlacl url=$Url 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "Verified — reservation is active." -ForegroundColor Green
} else {
    Write-Error "Reservation not found after adding. Check output above."
    exit 1
}