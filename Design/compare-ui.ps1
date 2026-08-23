param(
    [string]$Reference = "$PSScriptRoot\main_ui.png",
    [string]$OutputDirectory = "$PSScriptRoot\ui-compare",
    [string]$ProcessName = "Bend",
    [int]$Tolerance = 8
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$nativeSource = @"
using System;
using System.Runtime.InteropServices;
public static class BendUiNative {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int command);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
}
"@
if (-not ("BendUiNative" -as [type])) { Add-Type -TypeDefinition $nativeSource }
[BendUiNative]::SetThreadDpiAwarenessContext([IntPtr](-4)) | Out-Null

if (-not (Test-Path -LiteralPath $Reference)) { throw "Reference image not found: $Reference" }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$referenceBitmap = [System.Drawing.Bitmap]::FromFile((Resolve-Path -LiteralPath $Reference))
$process = Get-Process -Name $ProcessName -ErrorAction Stop | Where-Object MainWindowHandle -ne 0 | Select-Object -First 1
if (-not $process) { throw "No visible $ProcessName window was found." }

$handle = $process.MainWindowHandle
[BendUiNative]::ShowWindow($handle, 9) | Out-Null
[BendUiNative]::SetWindowPos($handle, [IntPtr]::Zero, 0, 0, $referenceBitmap.Width, $referenceBitmap.Height, 0x0014) | Out-Null
[BendUiNative]::SetForegroundWindow($handle) | Out-Null
Start-Sleep -Milliseconds 350

$capture = New-Object System.Drawing.Bitmap($referenceBitmap.Width, $referenceBitmap.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($capture)
$hdc = $graphics.GetHdc()
$captured = [BendUiNative]::PrintWindow($handle, $hdc, 2)
$graphics.ReleaseHdc($hdc)
$graphics.Dispose()
if (-not $captured) { throw "PrintWindow failed to capture Bend." }

$capturePath = Join-Path $OutputDirectory "app.png"
$overlayPath = Join-Path $OutputDirectory "overlay.png"
$diffPath = Join-Path $OutputDirectory "diff.png"
$reportPath = Join-Path $OutputDirectory "report.json"
$capture.Save($capturePath, [System.Drawing.Imaging.ImageFormat]::Png)

$overlay = New-Object System.Drawing.Bitmap($referenceBitmap.Width, $referenceBitmap.Height)
$diff = New-Object System.Drawing.Bitmap($referenceBitmap.Width, $referenceBitmap.Height)
$overlayGraphics = [System.Drawing.Graphics]::FromImage($overlay)
$overlayGraphics.DrawImage($referenceBitmap, 0, 0)
$attributes = New-Object System.Drawing.Imaging.ImageAttributes
$matrix = New-Object System.Drawing.Imaging.ColorMatrix
$matrix.Matrix33 = 0.5
$attributes.SetColorMatrix($matrix)
$rect = New-Object System.Drawing.Rectangle(0, 0, $capture.Width, $capture.Height)
$overlayGraphics.DrawImage($capture, $rect, 0, 0, $capture.Width, $capture.Height, [System.Drawing.GraphicsUnit]::Pixel, $attributes)
$overlayGraphics.Dispose()
$attributes.Dispose()
$overlay.Save($overlayPath, [System.Drawing.Imaging.ImageFormat]::Png)

function Test-MaskedPixel([int]$x, [int]$y, [int]$width, [int]$height) {
    # Text editor content may differ, but its background is checked separately below.
    if ($x -ge 94 -and $y -ge 74 -and $y -lt ($height - 87)) { return $true }
    # Native window buttons may differ.
    if ($x -ge ($width - 205) -and $y -lt 74) { return $true }
    # Preserve native rounded corners.
    if (($x -lt 18 -or $x -ge ($width - 18)) -and ($y -lt 18 -or $y -ge ($height - 18))) { return $true }
    return $false
}

$different = 0L
$compared = 0L
$totalError = 0L
$maximumError = 0
$pixelFormat = [System.Drawing.Imaging.PixelFormat]::Format32bppArgb
$bitmapRect = New-Object System.Drawing.Rectangle(0, 0, $referenceBitmap.Width, $referenceBitmap.Height)
$referenceData = $referenceBitmap.LockBits($bitmapRect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, $pixelFormat)
$captureData = $capture.LockBits($bitmapRect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, $pixelFormat)
$diffData = $diff.LockBits($bitmapRect, [System.Drawing.Imaging.ImageLockMode]::WriteOnly, $pixelFormat)
$byteCount = [Math]::Abs($referenceData.Stride) * $referenceData.Height
$referenceBytes = New-Object byte[] $byteCount
$captureBytes = New-Object byte[] $byteCount
$diffBytes = New-Object byte[] $byteCount
[Runtime.InteropServices.Marshal]::Copy($referenceData.Scan0, $referenceBytes, 0, $byteCount)
[Runtime.InteropServices.Marshal]::Copy($captureData.Scan0, $captureBytes, 0, $byteCount)
for ($y = 0; $y -lt $referenceBitmap.Height; $y++) {
    for ($x = 0; $x -lt $referenceBitmap.Width; $x++) {
        $offset = ($y * $referenceData.Stride) + ($x * 4)
        if (Test-MaskedPixel $x $y $referenceBitmap.Width $referenceBitmap.Height) {
            $diffBytes[$offset] = 245; $diffBytes[$offset + 1] = 245; $diffBytes[$offset + 2] = 245; $diffBytes[$offset + 3] = 255
            continue
        }
        $blueError = [Math]::Abs($referenceBytes[$offset] - $captureBytes[$offset])
        $greenError = [Math]::Abs($referenceBytes[$offset + 1] - $captureBytes[$offset + 1])
        $redError = [Math]::Abs($referenceBytes[$offset + 2] - $captureBytes[$offset + 2])
        $channelError = [Math]::Max($redError, [Math]::Max($greenError, $blueError))
        $compared++
        $totalError += $channelError
        if ($channelError -gt $maximumError) { $maximumError = $channelError }
        if ($channelError -gt $Tolerance) {
            $different++
            $strength = [Math]::Min(255, 70 + ($channelError * 2))
            $diffBytes[$offset] = 0; $diffBytes[$offset + 1] = 0; $diffBytes[$offset + 2] = $strength; $diffBytes[$offset + 3] = 255
        } else {
            $gray = [int](($referenceBytes[$offset] + $referenceBytes[$offset + 1] + $referenceBytes[$offset + 2]) / 3)
            $diffBytes[$offset] = $gray; $diffBytes[$offset + 1] = $gray; $diffBytes[$offset + 2] = $gray; $diffBytes[$offset + 3] = 255
        }
    }
}
[Runtime.InteropServices.Marshal]::Copy($diffBytes, 0, $diffData.Scan0, $byteCount)
$referenceBitmap.UnlockBits($referenceData)
$capture.UnlockBits($captureData)
$diff.UnlockBits($diffData)
$diff.Save($diffPath, [System.Drawing.Imaging.ImageFormat]::Png)

$samplePoints = @(
    @(500, 500), @(900, 500), @(500, 900), @(1200, 900)
)
$backgroundErrors = foreach ($point in $samplePoints) {
    $expected = $referenceBitmap.GetPixel($point[0], $point[1])
    $actual = $capture.GetPixel($point[0], $point[1])
    [ordered]@{
        x = $point[0]; y = $point[1]
        expected = ('#{0:X2}{1:X2}{2:X2}' -f $expected.R, $expected.G, $expected.B)
        actual = ('#{0:X2}{1:X2}{2:X2}' -f $actual.R, $actual.G, $actual.B)
    }
}

$report = [ordered]@{
    reference = (Resolve-Path -LiteralPath $Reference).Path
    capture = $capturePath
    dimensions = "{0}x{1}" -f $referenceBitmap.Width, $referenceBitmap.Height
    tolerance = $Tolerance
    comparedPixels = $compared
    differentPixels = $different
    mismatchPercent = [Math]::Round(($different * 100.0) / [Math]::Max(1, $compared), 4)
    meanChannelError = [Math]::Round($totalError / [Math]::Max(1.0, $compared), 3)
    maximumChannelError = $maximumError
    editorBackgroundSamples = $backgroundErrors
    masks = @("editor content", "window controls", "18px rounded corners")
}
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $reportPath -Encoding UTF8
$report | ConvertTo-Json -Depth 5

$diff.Dispose()
$overlay.Dispose()
$capture.Dispose()
$referenceBitmap.Dispose()
