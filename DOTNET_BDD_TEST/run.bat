@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

set ROOT_DIR=%~dp0..
set TESTS_PROJ=%~dp0ServerMain.BDDTests\ServerMain.BDDTests.csproj

:START_LOOP
cls
echo ==============================================================================
echo [BDD 自動化執行腳本 - 獨立資料庫與併行測試
echo ==============================================================================

taskkill /F /IM server.exe >nul 2>&1

echo.
echo [步驟 1/3] 正在編譯 伺服器...
cd /d "%ROOT_DIR%"
go build -o server.exe main.go
if %errorlevel% neq 0 (
    echo [錯誤] 伺服器編譯失敗！
    goto CLEANUP
)
echo [成功] C# 伺服器編譯完成！

echo.
echo [步驟 2/3] 正在編譯 C# .NET 10 BDD 測試專案...
cd /d "%~dp0"
dotnet build "%TESTS_PROJ%"
if %errorlevel% neq 0 (
    echo [錯誤] 測試專案編譯失敗！
    goto CLEANUP
)
echo [成功] 測試專案編譯完成！

echo.
echo [步驟 3/3] 正在啟動 C# BDD 測試 (各 Feature 併發獨立資料庫與獨立伺服器)...
dotnet test "%TESTS_PROJ%" --no-build --logger "console;verbosity=normal"
set TEST_EXIT_CODE=%errorlevel%

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
    echo  警告：BDD 測試未完全通過，請檢查錯誤記錄。
    echo ========================================================
)

echo.
echo 請按 Enter / 任意鍵重新執行測試，或關閉視窗 (Ctrl+C) 結束...
pause >nul
goto START_LOOP
