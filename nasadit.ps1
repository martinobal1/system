# Nasadenie jedným krokom: commit + push na GitHub. Zvyšok (build, testy, nasadenie na server) spraví GitHub Actions.
# Použitie: dvojklik na nasadit.bat, alebo v PowerShelli:  .\nasadit.ps1 "popis zmeny"
param([string]$Sprava = "")
$ErrorActionPreference = "Stop"
Set-Location -Path $PSScriptRoot

git add -A
$staged = git diff --cached --name-only
if (-not $staged) {
    Write-Host "Nie sú žiadne zmeny na nasadenie." -ForegroundColor Yellow
    exit 0
}
# Poistka: tajné súbory nesmú ísť na GitHub
$secret = $staged | Where-Object { $_ -match '(^|/)\.env$' -or $_ -match 'tajne' -or $_ -match '\.(pem|key)$' }
if ($secret) {
    git reset -q
    Write-Host "ZASTAVENÉ — medzi zmenami je tajný súbor: $($secret -join ', ')" -ForegroundColor Red
    exit 1
}
Write-Host "Zmenené súbory ($(@($staged).Count)):" -ForegroundColor Cyan
$staged | Select-Object -First 30 | ForEach-Object { Write-Host "  $_" }

if (-not $Sprava) { $Sprava = Read-Host "Popis zmeny (Enter = automatický)" }
if (-not $Sprava) { $Sprava = "Aktualizacia " + (Get-Date -Format "yyyy-MM-dd HH:mm") }

git commit -q -m $Sprava
git push
Write-Host ""
Write-Host "Odoslané na GitHub. Nasadenie na server beží automaticky:" -ForegroundColor Green
Write-Host "  https://github.com/martinobal1/system/actions"
