# Repo hygiene check. Usage:  powershell -NoProfile -ExecutionPolicy Bypass -File tools\check-hygiene.ps1
# Output: one line per violation ("R# path:line message"). Zero lines = clean.

$root = Split-Path -Parent $PSScriptRoot
$issues = @()
$scanDirs = @("scripts")

function SrcFiles {
    Get-ChildItem (Join-Path $root "scripts") -Recurse -Filter *.cs |
        Where-Object { $_.FullName -notmatch '\\(bin|obj|\.godot)\\' }
}

# R1: file length <= 400
foreach ($f in SrcFiles) {
    $n = (Get-Content -LiteralPath $f.FullName).Count
    if ($n -gt 400) { $issues += "R1  $($f.FullName.Replace("$root\",''))  $n lines (limit 400)" }
}

# R2: method length <= 60 (brace counting from signature lines)
foreach ($f in SrcFiles) {
    $text = Get-Content -LiteralPath $f.FullName
    for ($i = 0; $i -lt $text.Count; $i++) {
        $line = $text[$i]
        if ($line -match '^\s{4}(public|private|protected|internal)\s' -and $line -match '\)\s*$') {
            $depth = 0; $end = $i; $opened = $false
            for ($j = $i; $j -lt [Math]::Min($i + 250, $text.Count); $j++) {
                $depth += ([regex]::Matches($text[$j], '\{')).Count
                $depth -= ([regex]::Matches($text[$j], '\}')).Count
                if ($depth -gt 0) { $opened = $true }
                if ($opened -and $depth -le 0) { $end = $j; break }
            }
            $len = $end - $i + 1
            if ($len -gt 60) {
                $issues += "R2  $($f.FullName.Replace("$root\",'')):$($i+1)  method $len lines (limit 60)"
            }
            $i = $end
        }
    }
}

# R3: lower layers must not reference upper-layer types
$upper = "Player|EquipmentPanel|Hud|ModifyPanel|GridPanel|ResultPanel|MainMenu"
foreach ($dir in @("scripts\item","scripts\world","scripts\weapon","scripts\enemy")) {
    $p = Join-Path $root $dir
    if (-not (Test-Path $p)) { continue }
    foreach ($f in (Get-ChildItem $p -Recurse -Filter *.cs)) {
        $hits = Select-String -LiteralPath $f.FullName -Pattern "\b($upper)\b"
        foreach ($h in $hits) {
            $t = $h.Line.Trim()
            if ($t.StartsWith("//") -or $t.StartsWith("///")) { continue }
            $issues += "R3  $($f.FullName.Replace("$root\",'')):$($h.LineNumber)  lower->upper: $t"
        }
    }
}

# R4: no new global mutable state (frozen whitelist)
$allowed = @("EventBus.cs","ItemDatabase.cs","NavigationManager.cs","SceneManager.cs","WeaponMods.cs")
foreach ($f in SrcFiles) {
    if ($allowed -contains $f.Name) { continue }
    foreach ($h in (Select-String -LiteralPath $f.FullName -Pattern 'public static .*\{ get; set; \}')) {
        $issues += "R4  $($f.FullName.Replace("$root\",'')):$($h.LineNumber)  new global mutable state"
    }
}

# R5: string-based node group lookup (legacy allowlist: GroundItemsLayer)
foreach ($f in SrcFiles) {
    if ($f.Name -eq "GroundItemsLayer.cs") { continue }
    foreach ($h in (Select-String -LiteralPath $f.FullName -Pattern 'GetFirstNodeInGroup')) {
        $issues += "R5  $($f.FullName.Replace("$root\",'')):$($h.LineNumber)  string group lookup"
    }
}

# R6: swallowed exceptions
foreach ($f in SrcFiles) {
    foreach ($h in (Select-String -LiteralPath $f.FullName -Pattern 'catch\s*(\([^)]*\))?\s*\{\s*\}')) {
        $issues += "R6  $($f.FullName.Replace("$root\",'')):$($h.LineNumber)  empty catch"
    }
}

# R7: world sprite x2 scaling (limit 5 sites)
$scale = @()
foreach ($f in SrcFiles) {
    foreach ($h in (Select-String -LiteralPath $f.FullName -Pattern 'Scale = new Vector2\(2')) {
        $scale += "$($f.Name):$($h.LineNumber)"
    }
}
if ($scale.Count -gt 5) { $issues += "R7  scripts/  $($scale.Count) scaling sites (limit 5): $($scale -join ', ')" }

# R11: hardcoded CellPx = 40 outside GridPanel
foreach ($f in (Get-ChildItem (Join-Path $root "scripts\ui") -Filter *.cs)) {
    if ($f.Name -eq "GridPanel.cs") { continue }
    foreach ($h in (Select-String -LiteralPath $f.FullName -Pattern 'CellPx = 40')) {
        $issues += "R11  $($f.FullName.Replace("$root\",'')):$($h.LineNumber)  hardcoded CellPx = 40"
    }
}

# R12: literal backslash-n used to join code lines
foreach ($f in SrcFiles) {
    foreach ($h in (Select-String -LiteralPath $f.FullName -Pattern '\\n\s\s+[_a-zA-Z]')) {
        $issues += "R12  $($f.FullName.Replace("$root\",'')):$($h.LineNumber)  literal \n in source"
    }
}

if ($issues.Count -eq 0) {
    Write-Output "OK: 0 violations"
} else {
    $issues | ForEach-Object { Write-Output $_ }
    Write-Output ""
    Write-Output ("TOTAL: {0}" -f $issues.Count)
}
