@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

set ROOT_DIR=%~dp0..
set TESTS_PROJ=%~dp0ServerMain.BDDTests\ServerMain.BDDTests.csproj
set REPORT_DIR=%~dp0reports\coverage
set COBERTURA_XML=%REPORT_DIR%\coverage.cobertura.xml
set HTML_DIR=%REPORT_DIR%\html

:START_LOOP
cls
echo ==============================================================================
echo [BDD 測試程式碼覆蓋率分析腳本 - 跨進程伺服器端覆蓋率]
echo ==============================================================================

taskkill /F /IM server.exe >nul 2>&1

echo.
echo [步驟 1/4] 正在編譯 伺服器 (Debug 模式包含完整 PDB 符號)...
cd /d "%ROOT_DIR%"
dotnet publish "%ROOT_DIR%\src\ServerMain.csproj" -o "%ROOT_DIR%\bin\publish" -c Debug
if %errorlevel% neq 0 (
    echo [錯誤] 伺服器編譯失敗！
    goto CLEANUP
)
echo [成功] 伺服器編譯完成！

echo.
echo [步驟 2/4] 正在編譯 C# .NET 10 BDD 測試專案...
cd /d "%~dp0"
dotnet build "%TESTS_PROJ%" -c Debug
if %errorlevel% neq 0 (
    echo [錯誤] 測試專案編譯失敗！
    goto CLEANUP
)
echo [成功] 測試專案編譯完成！

if not exist "%REPORT_DIR%" mkdir "%REPORT_DIR%"

echo.
echo [步驟 3/4] 啟動 BDD 測試並透過 dotnet-coverage 跨進程收集代碼覆蓋率...
dotnet-coverage collect --output "%COBERTURA_XML%" --output-format cobertura --nologo "dotnet test \"%TESTS_PROJ%\" --no-build --logger \"console;verbosity=normal\""
set TEST_EXIT_CODE=%errorlevel%

echo.
echo [步驟 4/4] 正在產出 HTML 覆蓋率視覺化報告 (排除第三方套件與測試本身)...
reportgenerator -reports:"%COBERTURA_XML%" -targetdir:"%HTML_DIR%" -reporttypes:Html -assemblyfilters:"+server;-ServerMain.BDDTests;-MySqlConnector"

:CLEANUP
echo.
echo [清理] 確保關閉所有殘留 伺服器行程...
taskkill /F /IM server.exe >nul 2>&1

echo.
if "%TEST_EXIT_CODE%"=="0" (
    echo ========================================================
    echo  恭喜！所有 BDD 測試全數通過！
    echo ========================================================
) else (
    echo ========================================================
    echo  警告：部分 BDD 測試未通過，請檢查錯誤記錄。
    echo ========================================================
)

if exist "%HTML_DIR%\index.html" (
    echo.
    echo 覆蓋率報告已產出:
    echo %HTML_DIR%\index.html
    echo.
    echo 正在自動開啟覆蓋率報告網頁...
    start "" "%HTML_DIR%\index.html"
)

echo.
echo 請按 Enter / 任意鍵重新執行測試，或關閉視窗 (Ctrl+C) 結束...
pause >nul
goto START_LOOP
