@echo off
setlocal EnableDelayedExpansion
rem Instala o AutoEletrica nas versoes do Revit deste computador (2025, 2026, 2027), com o Revit em qualquer disco (C:, A:, D:...).
rem O plugin vai para a pasta de add-ins do usuario (%APPDATA%), que e a mesma onde quer que o Revit esteja instalado.
rem Feche o Revit antes.
set "ORIGEM=%~dp0"
set "ACHOU="
for %%V in (2025 2026 2027) do (
  call :procurar %%V
  if defined ONDE (
    echo Revit %%V encontrado em !ONDE!
    call :instalar %%V
    set "ACHOU=1"
  )
)

if not defined ACHOU (
  echo Nenhum Revit 2025, 2026 ou 2027 foi encontrado nos discos deste computador.
  echo Se o Revit esta instalado numa pasta diferente, o AutoEletrica pode ser instalado para as tres versoes:
  echo cada versao do Revit so carrega a sua.
  choice /C SN /M "Instalar para o Revit 2025, 2026 e 2027"
  if errorlevel 2 goto fim
  for %%V in (2025 2026 2027) do call :instalar %%V
  set "ACHOU=1"
)

echo.
echo Pronto. Abra o Revit: deve aparecer a aba AutoEletrica.
:fim
pause
exit /b

rem Procura o Revit.exe da versao em todos os discos, nas pastas de instalacao comuns. Tambem vale a pasta de add-ins
rem da versao ja existir (o Revit ja foi aberto neste usuario).
:procurar
set "ONDE="
for %%D in (A B C D E F G H I J K L M N O P Q R S T U V W X Y Z) do (
  if not defined ONDE if exist "%%D:\" (
    if exist "%%D:\Program Files\Autodesk\Revit %1\Revit.exe" set "ONDE=%%D:\Program Files\Autodesk\Revit %1"
    if not defined ONDE if exist "%%D:\Autodesk\Revit %1\Revit.exe" set "ONDE=%%D:\Autodesk\Revit %1"
    if not defined ONDE if exist "%%D:\Revit %1\Revit.exe" set "ONDE=%%D:\Revit %1"
  )
)
if not defined ONDE if exist "%APPDATA%\Autodesk\Revit\Addins\%1\" set "ONDE=a pasta de add-ins do Revit %1 deste usuario"
exit /b

:instalar
echo Instalando no Revit %1...
if not exist "%APPDATA%\Autodesk\Revit\Addins\%1" mkdir "%APPDATA%\Autodesk\Revit\Addins\%1"
xcopy /E /I /Y /Q "%ORIGEM%Revit %1\*" "%APPDATA%\Autodesk\Revit\Addins\%1\" >nul
exit /b
