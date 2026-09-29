function Get-Version {
    $project = Join-Path (Split-Path -Parent $PSScriptRoot) "src/HostPlugin/PmxEditorMcp.HostPlugin.csproj"
    $said = dotnet msbuild $project -getProperty:Version
    if ($LASTEXITCODE -ne 0) { throw "バージョンを読めない。" }

    $version = $said.Trim()
    if (-not $version) { throw "バージョンが空である。" }

    $version
}
