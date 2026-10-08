@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

set ROOT_DIR=%~dp0..
set LOAD_PROJ=%~dp0ServerMain.LoadTests\ServerMain.LoadTests.csproj
set SERVER_PORT=18080

:START_LOOP
cls
echo ==============================================================================
echo [NBomber 高併發真實虛擬使用者壓力測試 (.NET 10 ^& Go Server)]
echo ==============================================================================

:: 產生 4 碼亂數後綴以確保獨立乾淨的測試環境 (slot_loadtest_????)
for /f "delims=" %%i in ('powershell -NoProfile -Command "([guid]::NewGuid().ToString('N').Substring(0,4))"') do set RAND_SUFFIX=%%i
set DB_NAME=slot_loadtest_!RAND_SUFFIX!
set DB_DSN=root:rootpassword@tcp(127.0.0.1:3306)/!DB_NAME!?charset=utf8mb4

echo [環境] 隨機建立隔離資料庫: !DB_NAME!

:: 先行清理可能殘留的 Go 伺服器行程
taskkill /F /IM server.exe >nul 2>&1

echo.
echo [步驟 1/4] 正在編譯 伺服器...
cd /d "%ROOT_DIR%"
go build -o server.exe main.go
if %errorlevel% neq 0 (
    echo [錯誤] 伺服器編譯失敗！
    goto CLEANUP
)
echo [成功] 伺服器編譯完成！

echo.
echo [步驟 2/4] 正在編譯 C# .NET 10 NBomber 壓力測試專案 (Release)...
cd /d "%~dp0"
dotnet build "%LOAD_PROJ%" -c Release
if %errorlevel% neq 0 (
    echo [錯誤] C# 壓測專案編譯失敗！
    goto CLEANUP
)
echo [成功] C# 壓測專案編譯完成！

echo.
echo [步驟 3/4] 正在建立並初始化隨機資料庫 !DB_NAME!...
dotnet run --project "%LOAD_PROJ%" -c Release --no-build -- --init-db-only --dbname "!DB_NAME!"
if %errorlevel% neq 0 (
    echo [錯誤] 資料庫初始化失敗！
    goto CLEANUP
)

echo [步驟 3/4] 正在背景啟動 伺服器 (監聽連接埠 :%SERVER_PORT%)...
cd /d "%ROOT_DIR%"
start "" /B server.exe --port=:%SERVER_PORT% --dbdns="%DB_DSN%"
timeout /t 3 /nobreak >nul

echo.
echo [步驟 4/4] 正在執行高併發 NBomber 壓力測試...
cd /d "%~dp0"
dotnet run --project "%LOAD_PROJ%" -c Release --no-build -- --url "http://localhost:%SERVER_PORT%" --dbname "!DB_NAME!" --skip-db-init %*
set LOAD_EXIT_CODE=%errorlevel%

:CLEANUP
echo.
echo [清理] 關閉背景 伺服器行程...
taskkill /F /IM server.exe >nul 2>&1

echo [清理] 刪除暫存隔離資料庫 !DB_NAME!...
cd /d "%~dp0"
dotnet run --project "%LOAD_PROJ%" -c Release --no-build -- --drop-db --dbname "!DB_NAME!" >nul 2>&1

echo.
if "%LOAD_EXIT_CODE%"=="0" (
    echo ========================================================
    echo  恭喜！壓力測試已順利完成！請查閱上方統計或產生的報告。
    echo ========================================================
) else (
    echo ========================================================
    echo  警告：壓力測試中斷或發生錯誤。
    echo ========================================================
)

if "%1"=="--no-pause" goto END

echo.
echo 請按 Enter / 任意鍵重新執行測試，或關閉視窗 (Ctrl+C) 結束...
pause >nul
goto START_LOOP

:END
exit /b %LOAD_EXIT_CODE%
