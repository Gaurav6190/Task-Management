<#
.SYNOPSIS
    Configures a TEMPORARY test login for local development (task.md T8, D16).

.DESCRIPTION
    Stores the user in dotnet user-secrets for src/TaskManagement.Api, OUTSIDE the repository
    (%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json). Nothing is written to the repo:
      - Auth:Users:<Index>:Username      the login (email)
      - Auth:Users:<Index>:PasswordHash  ASP.NET Core Identity v3 hash (PBKDF2-HMACSHA512, 100,000 iterations)
      - Jwt:SigningKey                   random 64-byte key, created only if not already set
    The plaintext password is never stored. Configured users only work in the Development environment.

.EXAMPLE
    ./scripts/Set-DevAuthUser.ps1 -Username "someone@example.com" -Password "<test password>"
#>
param(
    [Parameter(Mandatory)][string]$Username,
    [Parameter(Mandatory)][string]$Password,
    [int]$Index = 0
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..\src\TaskManagement.Api'

function ConvertTo-BigEndian([uint32]$value) {
    $bytes = [BitConverter]::GetBytes($value)
    if ([BitConverter]::IsLittleEndian) { [Array]::Reverse($bytes) }
    return ,$bytes
}

# Same format as Microsoft.AspNetCore.Identity.PasswordHasher (V3), so the API can verify it:
# [0x01][PRF uint32][iterations uint32][salt length uint32][salt][subkey], big-endian, base64.
function New-IdentityV3Hash([string]$plain) {
    $saltLength = 16; $subkeyLength = 32; $iterations = 100000; $prfHmacSha512 = 2
    $salt = New-Object byte[] $saltLength
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $rng.GetBytes($salt); $rng.Dispose()

    $kdf = New-Object System.Security.Cryptography.Rfc2898DeriveBytes(
        $plain, $salt, $iterations, [System.Security.Cryptography.HashAlgorithmName]::SHA512)
    $subkey = $kdf.GetBytes($subkeyLength); $kdf.Dispose()

    $output = New-Object byte[] (13 + $saltLength + $subkeyLength)
    $output[0] = 0x01
    [Array]::Copy((ConvertTo-BigEndian $prfHmacSha512), 0, $output, 1, 4)
    [Array]::Copy((ConvertTo-BigEndian $iterations), 0, $output, 5, 4)
    [Array]::Copy((ConvertTo-BigEndian $saltLength), 0, $output, 9, 4)
    [Array]::Copy($salt, 0, $output, 13, $saltLength)
    [Array]::Copy($subkey, 0, $output, 13 + $saltLength, $subkeyLength)
    return [Convert]::ToBase64String($output)
}

$existing = dotnet user-secrets list --project $project 2>$null
if (-not ($existing -match '^Jwt:SigningKey\s*=')) {
    $key = New-Object byte[] 64
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create(); $rng.GetBytes($key); $rng.Dispose()
    dotnet user-secrets set 'Jwt:SigningKey' ([Convert]::ToBase64String($key)) --project $project | Out-Null
    Write-Host 'Created a new random Jwt:SigningKey in user-secrets.'
}

dotnet user-secrets set "Auth:Users:${Index}:Username" $Username.Trim() --project $project | Out-Null
dotnet user-secrets set "Auth:Users:${Index}:PasswordHash" (New-IdentityV3Hash $Password) --project $project | Out-Null

Write-Host "Configured test user '$($Username.Trim())' (slot $Index) in user-secrets. Password stored only as a hash."
