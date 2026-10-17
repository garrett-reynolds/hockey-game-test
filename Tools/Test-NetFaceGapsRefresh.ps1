param([Parameter(Mandatory = $true)][string]$OutputPath)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$nunit = 'C:\Users\garrett.reynolds\Projects\Personal\hockey-wt\111\Library\PackageCache\com.unity.ext.nunit@0198eae3b53e\net472\unity-custom\nunit.framework.dll'
Add-Type -Path $nunit
$compiler = 'C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Data\MonoBleedingEdge\lib\mono\msbuild\Current\bin\Roslyn\csc.exe'
$assembly = Join-Path ([System.IO.Path]::GetTempPath()) ('gap-refresh-' + [guid]::NewGuid().ToString('N') + '.dll')
$mono = 'C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Data\MonoBleedingEdge\bin\mono.exe'
$compileOutput = & $mono $compiler /nologo /target:library /out:$assembly /reference:$nunit (Join-Path $root 'Assets/Scripts/Core/NetFaceGaps.cs') (Join-Path $root 'Assets/Tests/EditMode/NetFaceGapsRefreshTests.cs') 2>&1
if ($LASTEXITCODE -ne 0) { $compileOutput | Set-Content -LiteralPath $OutputPath; throw ($compileOutput -join "`n") }
Add-Type -Path $assembly
$instance = New-Object HockeyGame.Tests.NetFaceGapsRefreshTests
$passed = 0
$failed = 0
$lines = @()
foreach ($method in $instance.GetType().GetMethods() | Where-Object { $_.GetCustomAttributes([NUnit.Framework.TestAttribute], $true).Count -gt 0 } | Sort-Object Name) {
    try {
        $method.Invoke($instance, @()) | Out-Null
        $lines += "PASS $($method.Name)"
        $passed++
    } catch {
        $lines += "FAIL $($method.Name): $($_.Exception.GetBaseException().Message)"
        $failed++
    }
}
$lines += "Passed: $passed; Failed: $failed; Worker count: 1"
$lines | Set-Content -LiteralPath $OutputPath
Write-Output "Passed: $passed; Failed: $failed"
if ($failed -gt 0) { exit 1 }
