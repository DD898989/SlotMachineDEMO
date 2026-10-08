@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

set ROOT_DIR=%~dp0..
set LOAD_PROJ=%~dp0ServerMain.LoadTests\ServerMain.LoadTests.csproj
set SERVER_PORT=18080

:START_LOOP
cls
echo ==============================================================================
echo [NBomber 高併發真實虛擬使用者壓力測試 (.NET 10 C# 伺服器)]
echo ==============================================================================

:: 產生亂數後綴以確保獨立乾淨的測試環境 (slot_loadtest_????)
set DB_NAME=slot_loadtest_!RANDOM!
set DB_DSN=root:rootpassword@tcp(127.0.0.1:3306)/!DB_NAME!?charset=utf8mb4

echo [環境] 隨機建立隔離資料庫: !DB_NAME!

:: 先行清理可能殘留的伺服器行程
taskkill /F /IM server.exe >nul 2>&1

echo.
echo [步驟 1/4] 正在編譯 伺服器...
cd /d "%ROOT_DIR%"
dotnet publish "%ROOT_DIR%\src\ServerMain.csproj" -o "%ROOT_DIR%\bin\publish" -c Release
if %errorlevel% neq 0 (
    echo [錯誤] 伺服器編譯失敗！
    goto CLEANUP
)
echo [成功] 伺服器編譯完成！

echo.
echo [步驟 2/5] 正在編譯 C# .NET 10 NBomber 壓力測試專案 (Release)...
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
echo [步驟 4/5] 正在背景啟動 C# 伺服器 (監聽連接埠 :%SERVER_PORT%)...
cd /d "%ROOT_DIR%\bin\publish"
start "" /B cmd /c "server.exe --port=:%SERVER_PORT% --dbdns="%DB_DSN%" > "%ROOT_DIR%\bin\publish\server_loadtest.log" 2>&1"

:: 探測伺服器是否就緒 (最多等待 10 秒)
set SERVER_READY=0
for /L %%i in (1,1,20) do (
    if !SERVER_READY! equ 0 (
        curl -s -f http://127.0.0.1:%SERVER_PORT%/ >nul 2>&1
        if !errorlevel! equ 0 (
            set SERVER_READY=1
        ) else (
            timeout /t 1 /nobreak >nul
        )
    )
)

if !SERVER_READY! equ 0 (
    echo [錯誤] 伺服器啟動逾時或失敗！請檢查伺服器日誌:
    if exist "%ROOT_DIR%\bin\publish\server_loadtest.log" (
        type "%ROOT_DIR%\bin\publish\server_loadtest.log"
    )
    goto CLEANUP
)
echo [成功] C# 伺服器啟動成功並已就緒！

echo.
echo [步驟 5/5] 正在執行高併發 NBomber 壓力測試...
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
