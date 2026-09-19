# その相手の子孫を、木を辿って終わらせる。相手自身は終わらせない。
# 番号は使い回されるので、その相手より後に始まったものだけを数える。
[CmdletBinding()]
param(
    # 木の根とするプロセスの番号。
    [Parameter(Mandatory)][int]$Root
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$standing = Get-Process -Id $Root -ErrorAction Ignore
if (-not $standing) { exit 0 }

$since = $standing.StartTime
$all = @(Get-CimInstance Win32_Process | Where-Object { $_.CreationDate -ge $since })

$want = @($Root)
do {
    $more = @($all |
        Where-Object { $want -contains $_.ParentProcessId -and $want -notcontains $_.ProcessId } |
        ForEach-Object { $_.ProcessId })
    $want += $more
} while ($more.Count -gt 0)

# この1本も根の子孫なので、木には自分が入る。既に終わっている相手への始末は失敗を書き出すので、
# 止まらずに残りを終わらせる。
foreach ($one in @($want | Where-Object { $_ -ne $Root -and $_ -ne $PID })) {
    try { & taskkill /F /PID $one 2>$null | Out-Null } catch { }
}

exit 0
