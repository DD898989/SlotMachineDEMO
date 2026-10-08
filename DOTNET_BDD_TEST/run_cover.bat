@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

set ROOT_DIR=%~dp0..
set TESTS_PROJ=%~dp0ServerMain.BDDTests\ServerMain.BDDTests.csproj
set COVERAGE_DIR=%ROOT_DIR%\coverage_data
set COVERAGE_OUT=%ROOT_DIR%\coverage.out
set COVERAGE_HTML=%ROOT_DIR%\coverage.html

:START_LOOP
cls
echo ==============================================================================
echo [BDD 自動化執行與程式碼覆蓋率分析 (.NET 10 ^& Go 1.20+ Cover)]
echo ==============================================================================

:: 清理既有 server 執行個體
taskkill /F /IM server.exe >nul 2>&1

:: 重設覆蓋率資料夾
if exist "%COVERAGE_DIR%" rd /s /q "%COVERAGE_DIR%"
mkdir "%COVERAGE_DIR%"

echo.
echo [步驟 1/5] 正在編譯 Go 伺服器 (包含代碼覆蓋率檢測 -cover)...
cd /d "%ROOT_DIR%"
go build -cover -o server.exe main.go
if %errorlevel% neq 0 (
    echo [錯誤] Go 伺服器編譯失敗！
    goto CLEANUP
)
echo [成功] Go 伺服器編譯完成！

echo.
echo [步驟 2/5] 正在啟動 Go 伺服器 (背景模式，收集覆蓋率)...
set GOCOVERDIR=%COVERAGE_DIR%
start "" /B server.exe

echo 等待伺服器連接資料庫並監聽 :8080...
timeout /t 3 /nobreak >nul

echo.
echo [步驟 3/5] 正在編譯 C# .NET 10 BDD 測試專案...
cd /d "%~dp0"
dotnet build "%TESTS_PROJ%"
if %errorlevel% neq 0 (
    echo [錯誤] C# 測試專案編譯失敗！
    goto CLEANUP
)
echo [成功] C# 測試專案編譯完成！

echo.
echo [步驟 4/5] 正在啟動 C# BDD 測試 (SpecFlow / xUnit)...
dotnet test "%TESTS_PROJ%" --no-build --logger "console;verbosity=normal"
set TEST_EXIT_CODE=%errorlevel%

:CLEANUP
echo.
echo [步驟 5/5] 優雅關閉 Go 伺服器以產生覆蓋率資料...
:: 透過 POST /shutdown 讓 Go 正常結束並儲存覆蓋率
curl -s -X POST http://localhost:8080/shutdown >nul 2>&1
timeout /t 2 /nobreak >nul
:: 若未正常終止則強制 taskkill 備援
taskkill /F /IM server.exe >nul 2>&1

echo.
echo ========================================================
echo  Go 程式碼覆蓋率分析報告
echo ========================================================
cd /d "%ROOT_DIR%"

if exist "%COVERAGE_DIR%" (
    echo [1] 套件覆蓋率百分比：
    go tool covdata percent -i="%COVERAGE_DIR%"
    
    echo.
    echo [2] 匯出 coverage.out ...
    go tool covdata textfmt -i="%COVERAGE_DIR%" -o="%COVERAGE_OUT%"
    
    echo [3] 產出 HTML 報告: "%COVERAGE_HTML%"
    go tool cover -html="%COVERAGE_OUT%" -o="%COVERAGE_HTML%"
    
    if "%1" neq "--no-pause" (
        start "" "%COVERAGE_HTML%"
    )
) else (
    echo [警告] 未找到覆蓋率資料目錄：%COVERAGE_DIR%
)

echo.
if "%TEST_EXIT_CODE%"=="0" (
    echo ========================================================
    echo  恭喜！所有老虎機 BDD 測試全數通過！
    echo ========================================================
) else (
    echo ========================================================
    echo  警告：BDD 測試未完全通過，請檢查錯誤記錄。
    echo ========================================================
)

if "%1"=="--no-pause" goto END

echo.
echo 請按 Enter / 任意鍵重新執行測試，或關閉視窗 (Ctrl+C) 結束...
pause >nul
goto START_LOOP

:END
exit /b %TEST_EXIT_CODE%
