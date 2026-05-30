$ErrorActionPreference = "Stop"

$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$exeCandidates = @(
    Join-Path $projectDir "bin\Release\net8.0-windows10.0.18362.0\win-x64\publish\PinMaster.exe"
    Join-Path $projectDir "bin\Release\net8.0-windows10.0.18362.0\PinMaster.exe"
    Join-Path $projectDir "bin\Debug\net8.0-windows10.0.18362.0\PinMaster.exe"
)

$exe = $exeCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if ($exe) {
    Start-Process -FilePath $exe -ArgumentList "--open" -WorkingDirectory (Split-Path -Parent $exe)
    exit 0
}

$localDotnet = Join-Path $projectDir ".dotnet\dotnet.exe"
$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
$dotnetPath = if (Test-Path $localDotnet) { $localDotnet } elseif ($dotnetCommand) { $dotnetCommand.Source } else { $null }
$sdks = if ($dotnetPath) { & $dotnetPath --list-sdks 2>$null } else { @() }
if ($dotnetPath -and $sdks.Count -gt 0) {
    Start-Process -FilePath $dotnetPath -ArgumentList "run --project `"$projectDir\PinMaster.csproj`" -c Release -- --open" -WorkingDirectory $projectDir -WindowStyle Hidden
    exit 0
}

[void][System.Reflection.Assembly]::LoadWithPartialName("PresentationFramework")
[System.Windows.MessageBox]::Show(
    "Pin Master is not built yet, and no .NET SDK was found. Install the .NET SDK or build the app once, then open this shortcut again.",
    "Pin Master",
    "OK",
    "Information"
) | Out-Null
