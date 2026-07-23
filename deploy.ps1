param (
    [string]$Configuration = "Release"
)

$timestamp = Get-Date -Format "HHmmss"

$strategyFile2 = "C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Plugin\DcaGridLiveTestStrategy.cs"
$content2 = Get-Content $strategyFile2
$content2 = $content2 -replace 'Name = "DCAnt2 Grid Test \d{6}";', "Name = `"DCAnt2 Grid Test $timestamp`";"
Set-Content -Path $strategyFile2 -Value $content2 -Encoding UTF8

Write-Host "=== Building DCAnt 2 Plugin ($Configuration) ==="
dotnet build -c $Configuration

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "=== Copying DLLs to Quantower ==="
$qtStrategiesPath = "C:\Users\dzam\Desktop\APPs\Quantower\Settings\Scripts\Strategies"

$pluginDll = "C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Plugin\bin\$Configuration\net10.0\DCAnt2.Plugin.dll"
$quantowerDll = "C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Quantower\bin\$Configuration\net10.0\DCAnt2.Quantower.dll"
$coreDll = "C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Core\bin\$Configuration\net10.0\DCAnt2.Core.dll"
$infraDll = "C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Infrastructure\bin\$Configuration\net10.0\DCAnt2.Infrastructure.dll"

Copy-Item -Path $pluginDll -Destination $qtStrategiesPath -Force
Copy-Item -Path $quantowerDll -Destination $qtStrategiesPath -Force
Copy-Item -Path $coreDll -Destination $qtStrategiesPath -Force
Copy-Item -Path $infraDll -Destination $qtStrategiesPath -Force

Write-Host "Deployment Successful. Strategy time suffix updated automatically!" -ForegroundColor Green
