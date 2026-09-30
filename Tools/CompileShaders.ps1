$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path -Parent $PSScriptRoot
$compilerSource = Join-Path $projectDirectory 'Complier'
$stagingDirectory = Join-Path $projectDirectory 'obj\ShaderCompiler'
$effectsDirectory = Join-Path $projectDirectory 'Assets\Effects'
New-Item -ItemType Directory -Path $stagingDirectory -Force | Out-Null

# Keep the supplied compiler/config untouched. Run a staged copy with effects as its working directory.
Get-ChildItem -LiteralPath $compilerSource -File | Where-Object {
    $_.Extension -in '.dll', '.exe'
} | Copy-Item -Destination $stagingDirectory -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ShaderCompiler.config') `
    -Destination (Join-Path $stagingDirectory 'EasyXnb.exe.config') -Force

$logPath = Join-Path $stagingDirectory 'compile.log'
$errorPath = Join-Path $stagingDirectory 'compile-errors.log'
$started = [DateTime]::UtcNow
$process = Start-Process -FilePath (Join-Path $stagingDirectory 'EasyXnb.exe') `
    -WorkingDirectory $effectsDirectory -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput $logPath -RedirectStandardError $errorPath
# Windows PowerShell 5 needs an open process handle to retain ExitCode after exit.
$compilerHandle = $process.Handle
if (-not $process.WaitForExit(60000)) {
    $process.Kill()
    throw 'Shader compiler timed out. Check obj/ShaderCompiler/compile.log.'
}
$process.WaitForExit()
Get-Content -LiteralPath $logPath
Get-Content -LiteralPath $errorPath
if ($null -eq $process.ExitCode -or $process.ExitCode -ne 0) {
    throw "Shader compiler exited with code $($process.ExitCode)."
}

# Some versions return exit code 0 on failure: require a freshly generated, valid XNB for every effect.
foreach ($source in Get-ChildItem -LiteralPath $effectsDirectory -Filter '*.fx' -File) {
    $outputPath = [IO.Path]::ChangeExtension($source.FullName, '.xnb')
    if (-not (Test-Path -LiteralPath $outputPath)) { throw "Missing compiled shader: $outputPath" }
    $output = Get-Item -LiteralPath $outputPath
    if ($output.LastWriteTimeUtc -lt $started -or $output.Length -lt 32) {
        throw "Shader was not freshly compiled: $outputPath"
    }
    $stream = [IO.File]::OpenRead($outputPath)
    try {
        if ($stream.ReadByte() -ne 88 -or $stream.ReadByte() -ne 78 -or $stream.ReadByte() -ne 66) {
            throw "Invalid XNB header: $outputPath"
        }
    }
    finally { $stream.Dispose() }
}
