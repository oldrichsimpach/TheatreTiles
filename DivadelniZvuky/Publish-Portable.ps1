param([string]$OutputRoot = (Join-Path $PSScriptRoot '../dist'))
$ErrorActionPreference = 'Stop'
$projectPath = Join-Path $PSScriptRoot 'DivadelniZvuky.csproj'
[xml]$project = Get-Content -LiteralPath $projectPath
$version = [string]$project.Project.PropertyGroup.Version
$outputFolder = [IO.Path]::GetFullPath((Join-Path $OutputRoot "TheatreTiles-$version-win-x64"))
$archive = "$outputFolder.zip"
if ((Test-Path -LiteralPath $outputFolder) -or (Test-Path -LiteralPath $archive)) {
    throw "Výstup už existuje: $outputFolder. Použijte jiný OutputRoot; starší balíček se nepřepisuje."
}
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '../.dotnet-home'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
& dotnet publish $projectPath -p:PublishProfile=Portable -o $outputFolder --source https://api.nuget.org/v3/index.json
if ($LASTEXITCODE -ne 0) { throw 'Publikování selhalo; ZIP nebyl vytvořen.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../LICENSE') -Destination $outputFolder
@'
THEATRETILES — SPUŠTĚNÍ

1. Rozbalte celý ZIP (pravé tlačítko → Extrahovat vše).
2. V rozbalené složce otevřete TheatreTiles.exe — aplikaci se zelenou ikonou T.

Tento balíček je pro 64bitové Windows (x64). Obsahuje vlastní .NET;
nepotřebujete instalovat SDK ani .NET Desktop Runtime.

Na GitHubu stahujte balíček TheatreTiles-...-win-x64.zip v Releases.
„Code → Download ZIP“ a „Source code“ obsahují zdrojové kódy, ne hotový program.

Pokud nevidíte příponu .exe, hledejte TheatreTiles typu „Aplikace“.
Pro zástupce na ploše klikněte na TheatreTiles.exe pravým tlačítkem
a zvolte Odeslat → Plocha (vytvořit zástupce).

Zvukové formáty závisejí také na kodecích Windows. Balení vlastního .NETu
nenahrazuje chybějící multimediální součásti Windows N.
'@ | Set-Content -LiteralPath (Join-Path $outputFolder 'NEJDRIV-CTI.txt') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination (Join-Path $outputFolder 'Navod.md')
Compress-Archive -LiteralPath $outputFolder -DestinationPath $archive
Get-FileHash -LiteralPath $archive -Algorithm SHA256 | Format-List
Write-Output "Balíček: $archive"
