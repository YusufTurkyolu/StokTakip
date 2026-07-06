@echo off
rem =====================================================================
rem  Stok Takip - Masaustu Kisayolu Olusturucu
rem
rem  Bu dosyayi her istemci bilgisayarda BIR KEZ calistirin:
rem  masaustune "Stok Takip" kisayolu olusturur. Kisayol, bu dosyanin
rem  yanindaki StokTakip.UI.exe'yi hedefler; program sunucudaki hangi
rem  paylasimda olursa olsun dogru yeri gosterir.
rem =====================================================================

powershell -NoProfile -ExecutionPolicy Bypass -Command "$ws = New-Object -ComObject WScript.Shell; $desktop = [Environment]::GetFolderPath('Desktop'); $lnkPath = Join-Path $desktop 'Stok Takip.lnk'; $lnk = $ws.CreateShortcut($lnkPath); $lnk.TargetPath = '%~dp0StokTakip.UI.exe'; $lnk.WorkingDirectory = '%~dp0'; $lnk.IconLocation = '%~dp0StokTakip.UI.exe,0'; $lnk.Description = 'Stok Takip Uygulamasi'; $lnk.Save(); Write-Host ''; Write-Host ('Kisayol olusturuldu: ' + $lnkPath)"

echo.
echo Pencereyi kapatmak icin bir tusa basin...
pause >nul
