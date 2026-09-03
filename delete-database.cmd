@echo off
setlocal

rem Wipes the local development database so the next app launch starts from a
rem clean first-run state. Close StageFright Community before running this.
rem
rem The app writes its SQLite database to FileSystem.AppDataDirectory\stagefright.db
rem (see src\StageFright.App\MauiProgram.cs). On the unpackaged Windows head that
rem MAUI path resolves to %LOCALAPPDATA%\<ApplicationTitle>\<ApplicationId>\Data,
rem i.e. the folder below. SQLite's WAL journal adds -wal / -shm sidecar files.
set "DB_DIR=%LOCALAPPDATA%\StageFright Community\com.stagefright.community\Data"

set "DELETED="
for %%F in (
    "%DB_DIR%\stagefright.db"
    "%DB_DIR%\stagefright.db-wal"
    "%DB_DIR%\stagefright.db-shm"
) do (
    if exist "%%~F" (
        del /f /q "%%~F" && echo Deleted %%~F
        set "DELETED=1"
    )
)

rem Design-time database created by "dotnet ef" commands (StageFrightDbContextFactory).
if exist "%~dp0design_time.db" (
    del /f /q "%~dp0design_time.db" && echo Deleted %~dp0design_time.db
    set "DELETED=1"
)

echo.
if defined DELETED (
    echo Database has been deleted - it will be recreated on the next app launch.
) else (
    echo No database files found under "%DB_DIR%".
    echo Nothing to delete ^(already clean, or the app has not been run yet^).
)

endlocal
