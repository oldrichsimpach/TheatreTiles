Add-Type -AssemblyName System.Drawing
function New-RoundedPath([single]$x,[single]$y,[single]$width,[single]$height,[single]$radius) {
 $p = [System.Drawing.Drawing2D.GraphicsPath]::new(); $d = $radius * 2
 $p.AddArc($x,$y,$d,$d,180,90); $p.AddArc(($x+$width-$d),$y,$d,$d,270,90)
 $p.AddArc(($x+$width-$d),($y+$height-$d),$d,$d,0,90); $p.AddArc($x,($y+$height-$d),$d,$d,90,90); $p.CloseFigure(); return ,$p
}
$assets = Join-Path $PSScriptRoot 'Assets'
$images = @()
foreach ($size in @(16,24,32,48,64,128,256)) {
 $bitmap = [System.Drawing.Bitmap]::new($size,$size)
 $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
 $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
 $graphics.Clear([System.Drawing.Color]::Transparent); $graphics.ScaleTransform(($size/256.0),($size/256.0))
 $background = New-RoundedPath 0 0 256 256 48
 $black = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#080A0F'))
 $graphics.FillPath($black,$background); $background.Dispose(); $black.Dispose()
 for ($row=0; $row -lt 3; $row++) { for ($col=0; $col -lt 3; $col++) {
  $x=27+$col*70; $y=27+$row*70
  $tile = New-RoundedPath $x $y 62 62 13
  $green = $row -eq 0 -or $col -eq 1
  $first = if ($green) { '#65BE86' } else { '#242B32' }
  $last = if ($green) { '#286A49' } else { '#151B22' }
  $brush = [System.Drawing.Drawing2D.LinearGradientBrush]::new([System.Drawing.Rectangle]::new($x,$y,62,62),[System.Drawing.ColorTranslator]::FromHtml($first),[System.Drawing.ColorTranslator]::FromHtml($last),[single]45)
  $graphics.FillPath($brush,$tile); $brush.Dispose(); $tile.Dispose()
 } }
 $graphics.Dispose()
 $stream = [IO.MemoryStream]::new(); $bitmap.Save($stream,[System.Drawing.Imaging.ImageFormat]::Png)
 $images += ,@{Size=$size;Bytes=$stream.ToArray()}
 if ($size -eq 256) { $bitmap.Save((Join-Path $assets 'TheatreTiles.png'),[System.Drawing.Imaging.ImageFormat]::Png) }
 $stream.Dispose(); $bitmap.Dispose()
}
$writer = [IO.BinaryWriter]::new([IO.File]::Create((Join-Path $assets 'TheatreTiles.ico')))
try {
 $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$images.Count)
 $offset = 6+16*$images.Count
 foreach ($item in $images) {
  $dimension = if ($item.Size -eq 256) { 0 } else { $item.Size }
  $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
  $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$item.Bytes.Length); $writer.Write([uint32]$offset)
  $offset += $item.Bytes.Length
 }
 foreach ($item in $images) { $writer.Write([byte[]]$item.Bytes) }
} finally { $writer.Dispose() }