# Version of a build on a release/<major>.<minor> branch: <major>.<minor>.<patch>, patch = the highest existing
# v<major>.<minor>.<n> tag + 1 (0 when there is none). A commit that already has such a tag keeps its version, so a rerun
# does not skip a number. The build publishes only when the newest section of the English history.txt is this version,
# i.e. the release notes were written. Prints name=<version> and publish=<true|false> lines.
param(
    [Parameter(Mandatory)] [string] $Branch,
    [string] $Head = 'HEAD',
    [string] $Notes = 'fastlane/metadata/android/en-US/changelogs/history.txt'
)

if ($Branch -notmatch '^release/(\d+)\.(\d+)$') {
    Write-Host "::error::Branch $Branch is not release/<major>.<minor>"
    exit 1
}
$line = "$($Matches[1]).$($Matches[2])"

$tagged = @(git tag --points-at $Head -l "v$line.*") | Where-Object { $_ -match "^v$([regex]::Escape($line))\.\d+$" }
if ($tagged) {
    $name = ($tagged | Select-Object -First 1).Substring(1)
} else {
    $patches = @(git tag -l "v$line.*") |
        ForEach-Object { if ($_ -match "^v$([regex]::Escape($line))\.(\d+)$") { [int]$Matches[1] } }
    $next = if ($patches) { ($patches | Measure-Object -Maximum).Maximum + 1 } else { 0 }
    $name = "$line.$next"
}

$top = Select-String -Path $Notes -Pattern '^## (\S+)' | Select-Object -First 1
$publish = ($top -and $top.Matches[0].Groups[1].Value -eq $name)
if (-not $publish) {
    Write-Host "::notice::Not published: the newest section of $Notes is not $name. Add the release notes for $name to publish it."
}

"name=$name"
"publish=$("$publish".ToLower())"
