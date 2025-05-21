rem build for windows
dotnet build -c Release
if %errorlevel% neq 0 exit /b %errorlevel%
cd Installer
dotnet build -c Release
cd ..