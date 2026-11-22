[CmdletBinding(DefaultParameterSetName = 'Azure')]
param(
    [Parameter(Mandatory)][string]$Server,
    [Parameter(Mandatory)][string]$Database,
    [Parameter(Mandatory)][string]$PublicCertificate,
    [Parameter(Mandatory)][string]$PackageDirectory,
    [string]$SqlBackupDirectory,
    [Parameter(Mandatory, ParameterSetName = 'Azure')][ValidatePattern('^[a-z0-9]{3,24}$')][string]$StorageAccount,
    [Parameter(Mandatory, ParameterSetName = 'Azure')][ValidatePattern('^[a-z0-9](?:[a-z0-9]|-(?=[a-z0-9])){1,61}[a-z0-9]$')][string]$Container,
    [Parameter(Mandatory, ParameterSetName = 'Local')][switch]$PackageOnly,
    [string]$OpenSslPath = 'openssl',
    [switch]$TrustServerCertificate
)

$ErrorActionPreference = 'Stop'
$openssl = Get-Command $OpenSslPath -CommandType Application -ErrorAction Stop | Select-Object -First 1
$certificate = Get-Item -LiteralPath $PublicCertificate
$directory = Get-Item -LiteralPath $PackageDirectory
if ($certificate.PSIsContainer -or !$directory.PSIsContainer) {
    throw 'Provide a PEM public certificate file and an existing, restricted package directory.'
}
$certificatePath = $certificate.FullName
if (!$PackageOnly) { $azure = Get-Command az -CommandType Application -ErrorAction Stop | Select-Object -First 1 }

& $openssl.Source x509 -in $certificatePath -noout -checkend 0 | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'The backup certificate is invalid or expired.' }

$backupPath = & (Join-Path $PSScriptRoot 'Backup-AcademicLedger.ps1') `
    -Server $Server -Database $Database -SqlBackupDirectory $SqlBackupDirectory `
    -TrustServerCertificate:$TrustServerCertificate -PassThru
if (!(Test-Path -LiteralPath $backupPath -PathType Leaf -ErrorAction SilentlyContinue)) {
    throw 'The verified .bak must be readable by the job account. Run this script on the SQL Server host.'
}

$packagePath = Join-Path $directory.FullName ((Split-Path $backupPath -Leaf) + '.cms')
if (Test-Path -LiteralPath $packagePath) { throw 'The encrypted package already exists.' }
$partialPath = $packagePath + '.partial'
try {
    # Stream the backup and authenticate the encrypted contents. Only the public certificate is needed.
    & $openssl.Source cms -encrypt -binary -stream -outform DER -aes-256-gcm `
        -in $backupPath -out $partialPath $certificatePath
    if ($LASTEXITCODE -ne 0) { throw 'Local encryption failed. The verified SQL backup is still available.' }
    Move-Item -LiteralPath $partialPath -Destination $packagePath
}
finally {
    if (Test-Path -LiteralPath $partialPath -PathType Leaf) { Remove-Item -LiteralPath $partialPath }
}

$sha256 = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host "Encrypted package: $packagePath"
Write-Host "SHA-256: $sha256"
if ($PackageOnly) { return }

$blob = "$Database/$(Split-Path $packagePath -Leaf)"
& $azure.Source storage blob upload --account-name $StorageAccount --container-name $Container `
    --name $blob --file $packagePath --auth-mode login --type block --overwrite false `
    --validate-content --metadata "sha256=$sha256" --no-progress --only-show-errors --output none
if ($LASTEXITCODE -ne 0) { throw 'Azure upload failed. The encrypted package is available locally for retry.' }
Write-Host "Uploaded encrypted backup: $StorageAccount/$Container/$blob"
