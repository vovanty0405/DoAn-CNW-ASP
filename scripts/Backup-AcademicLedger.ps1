[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Server,
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9_-]{1,100}$')][string]$Database,
    [switch]$TrustServerCertificate
)

$ErrorActionPreference = 'Stop'
$stamp = [DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss')
$unique = [Guid]::NewGuid().ToString('N').Substring(0, 8)
$fileName = "${Database}_${stamp}_${unique}.bak"
$query = @"
SET NOCOUNT ON;
DECLARE @database sysname = N'$Database';
IF DB_ID(@database) IS NULL OR DB_ID(@database) <= 4
    THROW 50000, 'Select an existing user database.', 1;

DECLARE @directory nvarchar(4000) = CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultBackupPath'));
IF NULLIF(@directory, '') IS NULL
    THROW 50001, 'The SQL Server instance has no default backup directory.', 1;

DECLARE @file nvarchar(4000) = @directory
    + CASE WHEN RIGHT(@directory, 1) IN ('\', '/') THEN '' ELSE '\' END
    + N'$fileName';

BACKUP DATABASE @database TO DISK = @file WITH COPY_ONLY, CHECKSUM;
RESTORE VERIFYONLY FROM DISK = @file WITH CHECKSUM;
PRINT N'Verified backup: ' + @file;
"@
$sqlcmd = Get-Command sqlcmd -CommandType Application -ErrorAction Stop
$arguments = @('-S', $Server, '-E', '-b', '-l', '15', '-Q', $query)
if ($TrustServerCertificate) { $arguments += '-C' }
& $sqlcmd.Source @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Backup or verification failed (sqlcmd exit code $LASTEXITCODE). Check SQL Server permissions and the backup directory."
}
