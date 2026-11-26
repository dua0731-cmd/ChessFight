# Draws the skill tree's skill pictures again: headless Edge shoots skill-icons.html on a clear background at twice its
# size, and slice_icons.py cuts the sheet into Assets/Resources/PawnRushSkillIcons/<Name>.png (see the page's head).
# Edge needs its own profile folder here, or it writes no file.
$here = $PSScriptRoot
$sheet = Join-Path $env:TEMP 'pawnrush-skill-icons.png'
$edgeProfile = Join-Path $env:TEMP 'pawnrush-skill-icons-edge'
$edge = Join-Path ${env:ProgramFiles(x86)} 'Microsoft\Edge\Application\msedge.exe'
$count = ([regex]::Matches((Get-Content (Join-Path $here 'skill-icons.html') -Raw), "(?m)^\s*(?:def|alias)\('")).Count
$height = [math]::Ceiling($count / 10) * 128
$url = 'file:///' + ($here -replace '\\', '/') + '/skill-icons.html'
if (Test-Path $sheet) { Remove-Item $sheet }
Start-Process $edge -Wait -WindowStyle Hidden -ArgumentList '--headless=new', '--disable-gpu', '--hide-scrollbars', '--no-first-run',
    "--user-data-dir=$edgeProfile", '--force-device-scale-factor=2', '--default-background-color=00000000',
    '--allow-file-access-from-files', "--window-size=1280,$height", "--screenshot=$sheet", $url
if (-not (Test-Path $sheet)) { throw "Edge wrote no sheet ($sheet)" }
python -I (Join-Path $here 'slice_icons.py') $sheet
