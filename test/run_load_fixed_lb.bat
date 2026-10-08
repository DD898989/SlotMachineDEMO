@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

set ROOT_DIR=%~dp0..
set LOAD_PROJ=%~dp0ServerMain.LoadTestsFixedLB\ServerMain.LoadTestsFixedLB.csproj
set SERVER_PORT_1=18080
set SERVER_PORT_2=18081

:START_LOOP
cls
echo ==============================================================================
echo [雙伺服器負載均衡 (LB) 數據時長壓測與理論驗證 (.NET 10 C# 雙實例)]
echo ==============================================================================

:: 產生亂數後綴以確保獨立乾淨的測試環境 (slot_fixed_lb_????)
set DB_NAME=slot_fixed_lb_!RANDOM!
set DB_DSN=root:rootpassword@tcp(127.0.0.1:3306)/!DB_NAME!?charset=utf8mb4

echo [環境] 隨機建立隔離資料庫: !DB_NAME!

:: 先行清理可能殘留的伺服器行程
taskkill /F /IM server.exe >nul 2>&1

echo.
echo [步驟 1/5] 正在編譯 伺服器...
cd /d "%ROOT_DIR%"
dotnet publish "%ROOT_DIR%\src\ServerMain.csproj" -o "%ROOT_DIR%\bin\publish" -c Release
if %errorlevel% neq 0 (
    echo [錯誤] 伺服器編譯失敗！
    goto CLEANUP
)
echo [成功] 伺服器編譯完成！

echo.
echo [步驟 2/5] 正在編譯 C# .NET 10 LB 壓力測試專案 (Release)...
cd /d "%~dp0"
dotnet build "%LOAD_PROJ%" -c Release
if %errorlevel% neq 0 (
    echo [錯誤] C# 壓測專案編譯失敗！
    goto CLEANUP
)
echo [成功] C# 壓測專案編譯完成！

echo.
echo [步驟 3/5] 正在建立並初始化隨機資料庫 !DB_NAME!...
dotnet run --project "%LOAD_PROJ%" -c Release --no-build -- --init-db-only --dbname "!DB_NAME!"
if %errorlevel% neq 0 (
    echo [錯誤] 資料庫初始化失敗！
    goto CLEANUP
)
echo [成功] 資料庫建立與結構同步完成！

echo.
echo [步驟 4/5] 正在背景啟動 2 台 C# 伺服器實例 (連接埠 :%SERVER_PORT_1% 與 :%SERVER_PORT_2%)...
cd /d "%ROOT_DIR%\bin\publish"
start "" /B cmd /c "server.exe --port=:%SERVER_PORT_1% --dbdns="%DB_DSN%" > "%ROOT_DIR%\bin\publish\server_loadtest_1.log" 2>&1"
start "" /B cmd /c "server.exe --port=:%SERVER_PORT_2% --dbdns="%DB_DSN%" > "%ROOT_DIR%\bin\publish\server_loadtest_2.log" 2>&1"

:: 探測 2 台伺服器是否就緒 (最多等待 20 秒)
set SERVER_1_READY=0
set SERVER_2_READY=0

for /L %%i in (1,1,30) do (
    if !SERVER_1_READY! equ 0 (
        curl -s -f http://127.0.0.1:%SERVER_PORT_1%/ >nul 2>&1
        if !errorlevel! equ 0 set SERVER_1_READY=1
    )
    if !SERVER_2_READY! equ 0 (
        curl -s -f http://127.0.0.1:%SERVER_PORT_2%/ >nul 2>&1
        if !errorlevel! equ 0 set SERVER_2_READY=1
    )
    if !SERVER_1_READY! equ 1 if !SERVER_2_READY! equ 1 goto SERVERS_UP
    timeout /t 1 /nobreak >nul 2>&1
)

:SERVERS_UP
if !SERVER_1_READY! equ 0 (
    echo [錯誤] 伺服器 1 [:%SERVER_PORT_1%] 啟動逾時或失敗！請檢查伺服器日誌:
    if exist "%ROOT_DIR%\bin\publish\server_loadtest_1.log" (
        type "%ROOT_DIR%\bin\publish\server_loadtest_1.log"
    )
    goto CLEANUP
)
if !SERVER_2_READY! equ 0 (
    echo [錯誤] 伺服器 2 [:%SERVER_PORT_2%] 啟動逾時或失敗！請檢查伺服器日誌:
    if exist "%ROOT_DIR%\bin\publish\server_loadtest_2.log" (
        type "%ROOT_DIR%\bin\publish\server_loadtest_2.log"
    )
    goto CLEANUP
)
echo [成功] 2 台 C# 伺服器均已啟動並就緒！

echo.
echo [步驟 5/5] 正在發動負載均衡 (LB) 壓力測試...
cd /d "%~dp0"
dotnet run --project "%LOAD_PROJ%" -c Release --no-build -- --urls "http://127.0.0.1:%SERVER_PORT_1%,http://127.0.0.1:%SERVER_PORT_2%" --dbname "!DB_NAME!" --skip-db-init %*
set LOAD_EXIT_CODE=%errorlevel%

:CLEANUP
echo.
echo [清理] 關閉所有背景 伺服器行程...
taskkill /F /IM server.exe >nul 2>&1

echo [清理] 刪除暫存隔離資料庫 !DB_NAME!...
cd /d "%~dp0"
dotnet run --project "%LOAD_PROJ%" -c Release --no-build -- --drop-db --dbname "!DB_NAME!" >nul 2>&1

echo.
if "%LOAD_EXIT_CODE%"=="0" (
    echo ========================================================
    echo  恭喜！雙機負載均衡壓力測試已順利完成！
    echo ========================================================
) else (
    echo ========================================================
    echo  警告：壓力測試中斷或發生錯誤。
    echo ========================================================
)

echo "%*" | findstr /i /c:"--no-pause" >nul && goto END

echo.
echo 請按 Enter / 任意鍵重新執行測試，或關閉視窗 (Ctrl+C) 結束...
pause >nul
goto START_LOOP

:END
exit /b %LOAD_EXIT_CODE%
