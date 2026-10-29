@echo off
rem Remove o SPK de todas as versoes do Revit. Feche o Revit antes.
for %%V in (2025 2026 2027) do (
  if exist "%APPDATA%\Autodesk\Revit\Addins\%%V\Spk.AddIn.addin" (
    echo Removendo do Revit %%V...
    del /Q "%APPDATA%\Autodesk\Revit\Addins\%%V\Spk.AddIn.addin"
    rmdir /S /Q "%APPDATA%\Autodesk\Revit\Addins\%%V\Spk.AddIn"
  )
)
echo Pronto.
pause
