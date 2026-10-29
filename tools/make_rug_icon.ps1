# 生成「碰一下地毯」按钮用的小地毯图标（96x72 PNG）
# 用法：pwsh -File tools/make_rug_icon.ps1
# 生成后会把 base64 写到 tools/rug_icon.b64，需要手动同步到 Scripts/RugIconData.cs
Add-Type -AssemblyName System.Drawing

$w = 96; $h = 72
$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

# 投影
$shadow = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(90, 0, 0, 0))
$g.FillEllipse($shadow, 12, 52, 64, 12)

# 地毯主体（暗红棕 + 金色描边）
$body = New-Object System.Drawing.Rectangle(10, 14, 64, 40)
$r = 6
$path = New-Object System.Drawing.Drawing2D.GraphicsPath
$path.AddArc($body.X, $body.Y, $r * 2, $r * 2, 180, 90)
$path.AddArc($body.Right - $r * 2, $body.Y, $r * 2, $r * 2, 270, 90)
$path.AddArc($body.Right - $r * 2, $body.Bottom - $r * 2, $r * 2, $r * 2, 0, 90)
$path.AddArc($body.X, $body.Bottom - $r * 2, $r * 2, $r * 2, 90, 90)
$path.CloseFigure()

$fill = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 84, 38, 32))
$g.FillPath($fill, $path)
$gold = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 201, 162, 39), 3)
$g.DrawPath($gold, $path)

# 金色菱形花纹
$brushG = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 214, 178, 90))
for ($ix = 0; $ix -lt 3; $ix++) {
    for ($iy = 0; $iy -lt 2; $iy++) {
        $cx = [float](26 + $ix * 18); $cy = [float](26 + $iy * 15)
        $pts = [System.Drawing.PointF[]]@(
            [System.Drawing.PointF]::new($cx, $cy - 4),
            [System.Drawing.PointF]::new($cx + 4, $cy),
            [System.Drawing.PointF]::new($cx, $cy + 4),
            [System.Drawing.PointF]::new($cx - 4, $cy)
        )
        $dp = New-Object System.Drawing.Drawing2D.GraphicsPath
        $dp.AddPolygon($pts)
        $g.FillPath($brushG, $dp)
        $dp.Dispose()
    }
}

# 右侧流苏
$fringe = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 201, 162, 39), 2)
for ($fy = 18; $fy -le 54; $fy += 6) { $g.DrawLine($fringe, 78, $fy, 88, $fy + 2) }

$out = Join-Path $PSScriptRoot "rug_icon.png"
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()

$b64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($out))
$b64 | Out-File -Encoding ascii (Join-Path $PSScriptRoot "rug_icon.b64")
Write-Host "written: $out ($((Get-Item $out).Length) bytes), base64 $($b64.Length) chars"
