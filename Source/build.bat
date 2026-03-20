@echo off
echo Building StyleExpand for RimWorld 1.5...
dotnet build -c Release /p:GameVersion=1.5

echo.
echo Building StyleExpand for RimWorld 1.6...
dotnet build -c Release /p:GameVersion=1.6

echo.
echo Copying common files...
copy /Y "..\1.5\Assemblies\Newtonsoft.Json.dll" "..\1.6\Assemblies\"

echo.
echo Build complete!
pause