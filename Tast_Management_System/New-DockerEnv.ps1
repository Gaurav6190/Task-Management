<#
.SYNOPSIS
    Creates or updates .env for docker compose. Sets the login user (password stored only as an
    ASP.NET Core Identity v3 hash). A missing SQL Server SA password or JWT signing key is generated;
    existing ones are kept, so the running database keeps working. Use this to change the login too.

.EXAMPLE
    ./New-DockerEnv.ps1 -Email "admin@taskmanagement.local" -Password "<your password>"
    ./New-DockerEnv.ps1 -Email "admin@taskmanagement.local" -Password "<password>" -RegenerateSecrets   # new SA password + JWT key
#>
param(
    [Parameter(Mandatory)][string]$Email,
    [Parameter(Mandatory)][string]$Password,
    # New SA password and JWT key. The existing database volume keeps the old SA password: run `docker compose down -v` first.
    [switch]$RegenerateSecrets
)

$ErrorActionPreference = 'Stop'
$out = Join-Path $PSScriptRoot '.env'
$template = if (Test-Path $out) { $out } else { Join-Path $PSScriptRoot '.env.example' }
$existing = @{}
Get-Content $template | Where-Object { $_ -match '^([A-Z_]+)=(.+)$' } | ForEach-Object { $existing[$Matches[1]] = $Matches[2] }

function RandBytes([int]$n) { $b = New-Object byte[] $n; $r = [Security.Cryptography.RandomNumberGenerator]::Create(); $r.GetBytes($b); $r.Dispose(); ,$b }
function RandText([int]$n) { $a = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789'; -join ((RandBytes $n) | ForEach-Object { $a[$_ % $a.Length] }) }
function ConvertTo-BigEndian([uint32]$v) { $b = [BitConverter]::GetBytes($v); if ([BitConverter]::IsLittleEndian) { [Array]::Reverse($b) }; ,$b }

# Same format as Api/scripts/Set-DevAuthUser.ps1 (Microsoft.AspNetCore.Identity.PasswordHasher V3).
function New-IdentityV3Hash([string]$plain) {
    $saltLength = 16; $subkeyLength = 32; $iterations = 100000; $prfHmacSha512 = 2
    $salt = RandBytes $saltLength
    $kdf = New-Object Security.Cryptography.Rfc2898DeriveBytes($plain, $salt, $iterations, [Security.Cryptography.HashAlgorithmName]::SHA512)
    $subkey = $kdf.GetBytes($subkeyLength); $kdf.Dispose()
    $o = New-Object byte[] (13 + $saltLength + $subkeyLength); $o[0] = 0x01
    [Array]::Copy((ConvertTo-BigEndian $prfHmacSha512), 0, $o, 1, 4)
    [Array]::Copy((ConvertTo-BigEndian $iterations), 0, $o, 5, 4)
    [Array]::Copy((ConvertTo-BigEndian $saltLength), 0, $o, 9, 4)
    [Array]::Copy($salt, 0, $o, 13, $saltLength)
    [Array]::Copy($subkey, 0, $o, 13 + $saltLength, $subkeyLength)
    [Convert]::ToBase64String($o)
}

$values = @{
    AUTH_USER_EMAIL         = $Email.Trim()
    AUTH_USER_PASSWORD_HASH = New-IdentityV3Hash $Password
}
if ($RegenerateSecrets -or -not $existing['MSSQL_SA_PASSWORD']) { $values.MSSQL_SA_PASSWORD = 'Sa!' + (RandText 20) + 'x9' }  # meets SQL Server complexity, no ';'
if ($RegenerateSecrets -or -not $existing['JWT_SIGNING_KEY'])   { $values.JWT_SIGNING_KEY = [Convert]::ToBase64String((RandBytes 64)) }
$lines = Get-Content $template | ForEach-Object {
    if ($_ -match '^([A-Z_]+)=' -and $values.ContainsKey($Matches[1])) { "$($Matches[1])=$($values[$Matches[1]])" } else { $_ }
}
[IO.File]::WriteAllLines($out, $lines, (New-Object Text.UTF8Encoding $false))
Write-Host "Saved .env. Run 'docker compose up -d', then log in at http://localhost:8080 as '$($Email.Trim())' with the password you supplied."
