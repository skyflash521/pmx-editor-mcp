# ビューの写しと、ツールが返した画像を見比べて、明るさの平均の差を出す。
# 大きさも縦横の比も違う2枚を比べるので、同じ辺数の格子へ縮めてから比べる。
# 出力は組ごとに1行で、0から1までの小数を渡された並びの順に書く。0が同じ、1が正反対を指す。
[CmdletBinding()]
param(
    # ビューから写し取ったPNGのパス。-Candidate と同じ数を同じ並びで渡す。
    [Parameter(Mandatory = $true)][string[]]$Reference,

    # ツールが返した画像。PNGをBase64で書いたテキストファイルのパス。
    [Parameter(Mandatory = $true)][string[]]$Candidate,

    # 縮める先の格子の一辺。
    [ValidateRange(4, 256)][int]$Side = 32
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

function Get-Grid {
    <#
        .SYNOPSIS
        画像を一辺 $Side の格子へ縮め、各升の明るさを0から1で並べる。
    #>
    param([System.Drawing.Image]$Image)

    $small = New-Object System.Drawing.Bitmap($Side, $Side)
    try {
        $canvas = [System.Drawing.Graphics]::FromImage($small)
        try {
            $canvas.InterpolationMode =
                [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $canvas.DrawImage($Image, 0, 0, $Side, $Side)
        }
        finally {
            $canvas.Dispose()
        }

        $values = New-Object 'double[]' ($Side * $Side)
        for ($y = 0; $y -lt $Side; $y++) {
            for ($x = 0; $x -lt $Side; $x++) {
                $pixel = $small.GetPixel($x, $y)
                # 明るさの重みはITU-R BT.601が定める。
                $values[$y * $Side + $x] =
                    (0.299 * $pixel.R + 0.587 * $pixel.G + 0.114 * $pixel.B) / 255.0
            }
        }

        $values
    }
    finally {
        $small.Dispose()
    }
}

if ($Reference.Count -ne $Candidate.Count) {
    throw "写しと比べる画像の数が違う: $($Reference.Count) 対 $($Candidate.Count)"
}

for ($pair = 0; $pair -lt $Reference.Count; $pair++) {
    # 名前はパラメータと重ねない。PowerShellの変数名は大文字小文字を区別しないので、重ねると
    # 並びを受けたパラメータが1つ目の値で上書きされ、以降の組が消える。
    $shotPath = $Reference[$pair]
    $givenPath = $Candidate[$pair]
    if (-not (Test-Path -LiteralPath $shotPath)) { throw "写しが無い: $shotPath" }
    if (-not (Test-Path -LiteralPath $givenPath)) { throw "比べる画像が無い: $givenPath" }

    $shot = [System.Drawing.Image]::FromFile((Resolve-Path -LiteralPath $shotPath))
    try {
        $bytes = [Convert]::FromBase64String((Get-Content -LiteralPath $givenPath -Raw).Trim())
        $stream = New-Object System.IO.MemoryStream(, $bytes)
        try {
            $given = [System.Drawing.Image]::FromStream($stream)
            try {
                $left = Get-Grid -Image $shot
                $right = Get-Grid -Image $given
                $sum = 0.0
                for ($at = 0; $at -lt $left.Length; $at++) {
                    $sum += [Math]::Abs($left[$at] - $right[$at])
                }

                Write-Output ([Math]::Round($sum / $left.Length, 4))
            }
            finally {
                $given.Dispose()
            }
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        $shot.Dispose()
    }
}
