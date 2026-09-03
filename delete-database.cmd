@echo off
setlocal

rem Wipes the local development database AND the MAUI Preferences store so the next
rem app launch starts from a genuinely clean first-run state - including the spec 029
rem /language-select screen (and, in Debug builds, its "Load sample data" option).
rem Close StageFright Community before running this.
rem
rem The app writes its SQLite database to FileSystem.AppDataDirectory\stagefright.db
rem (see src\StageFright.App\MauiProgram.cs). On the unpackaged Windows head that
rem MAUI path resolves to %LOCALAPPDATA%\<ApplicationTitle>\<ApplicationId>\Data,
rem i.e. the folder below. SQLite's WAL journal adds -wal / -shm sidecar files.
set "DB_DIR=%LOCALAPPDATA%\StageFright Community\com.stagefright.community\Data"

rem The display-language choice (ILanguagePreferenceStore -> MauiLanguagePreferenceStore)
rem is stored OUTSIDE the database via Microsoft.Maui.Storage.Preferences, which on the
rem unpackaged Windows head is this preferences.dat file - a sibling of Data\, so deleting
rem the database alone leaves it behind and App.razor.cs then skips /language-select
rem straight to /setup. Removing it too restores the full first-run flow.
set "SETTINGS_DIR=%LOCALAPPDATA%\StageFright Community\com.stagefright.community\Settings"

set "DELETED="
for %%F in (
    "%DB_DIR%\stagefright.db"
    "%DB_DIR%\stagefright.db-wal"
    "%DB_DIR%\stagefright.db-shm"
    "%SETTINGS_DIR%\preferences.dat"
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
    echo No database or preferences files found.
    echo Nothing to delete ^(already clean, or the app has not been run yet^).
)

endlocal
