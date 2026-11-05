param([switch]$Apply)
$ErrorActionPreference = 'Stop'
$saveProject = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$packageRoot = Join-Path $saveProject 'Assets/Packages'
$packagePins = @(
    'MessagePack.3.1.12', 'MessagePack.Annotations.3.1.12', 'MessagePackAnalyzer.3.1.12',
    'Microsoft.NET.StringTools.17.11.4', 'System.Buffers.4.5.1', 'System.Collections.Immutable.8.0.0',
    'System.Memory.4.5.5', 'System.Numerics.Vectors.4.4.0', 'System.Runtime.CompilerServices.Unsafe.6.0.0'
)
$runtimeDlls = @(
    'MessagePack.3.1.12/lib/netstandard2.1/MessagePack.dll',
    'MessagePack.Annotations.3.1.12/lib/netstandard2.0/MessagePack.Annotations.dll',
    'Microsoft.NET.StringTools.17.11.4/lib/netstandard2.0/Microsoft.NET.StringTools.dll'
)
# Unity 6000.6 .NET Standard 2.1 supplies these framework assemblies to Editor and Player.
# This repairs a prior raw NuGet extraction; a normal NuGetForUnity restore already selects one framework and skips ref/.
foreach ($required in $runtimeDlls) {
    if (!(Test-Path -LiteralPath (Join-Path $packageRoot $required))) { throw "Restore the pinned package first: $required" }
}
$changes = 0
foreach ($packagePin in $packagePins) {
    $directory = Join-Path $packageRoot $packagePin
    if (!(Test-Path -LiteralPath $directory)) { continue }
    foreach ($dll in Get-ChildItem -LiteralPath $directory -Recurse -File -Filter '*.dll') {
        $relative = $dll.FullName.Substring($packageRoot.Length + 1).Replace('\', '/')
        $metaPath = $dll.FullName + '.meta'
        if (!(Test-Path -LiteralPath $metaPath)) { throw "Import metadata missing: $relative" }
        $original = [IO.File]::ReadAllText($metaPath)
        $enabled = if ($runtimeDlls -contains $relative) { '1' } else { '0' }
        $updated = [regex]::Replace($original, '(?m)^(\s+enabled:) [01]', '$1 0')
        $updated = [regex]::Replace($updated, '(?m)^(\s+Any:\r?\n\s+enabled:) [01]', ('$1 ' + $enabled))
        if ($updated -ne $original) {
            $changes++
            Write-Output "AnyPlatform=$enabled $relative"
            if ($Apply) { [IO.File]::WriteAllText($metaPath, $updated, [Text.UTF8Encoding]::new($false)) }
        }
    }
}
Write-Output "Import changes: $changes; applied: $Apply. DLL bytes, GUIDs and package versions are unchanged."
