Feature: GonzosQuestAvalanche

Background:
  Given 建立獨立資料庫並寫入初始資料表 "ClubData":
    | ID | Name    | Bank     | Fund    | Lock | Rate | MRTP |
    | 1  | virtual | 10000000 | 1000000 | 0    | 2.5  | 95   |
  And 寫入初始資料表 "User":
    | ID | Email              | Secret | Name    | GAL |
    | 1  | admin@example.org  | 0YBoaT | admin   | 31  |
    | 2  | dealer@example.org | LtpkAr | dealer  | 3   |
    | 3  | player@example.org | iVI05M | player  | 1   |
    | 4  | player2@example.org| iVI05M | player2 | 1   |
  And 寫入初始資料表 "Props":
    | CID | UID | Wallet | Access | MRTP |
    | 1   | 3   | 100000 | 1      | 98   |
    | 1   | 4   | 1000   | 1      | 98   |
  And 啟動 Golang 伺服器
  And 玩家使用帳號 "player@example.org" 密碼 "iVI05M" 成功登入並取得授權 Token
  And 玩家點數錢包充值至固定金額 100000 點
  And 俱樂部水池充值至固定金額 10000000 點
  And 俱樂部彩金池充值至固定金額 1000000 點
  And 玩家個人目標 RTP 設定為 95.310212
  And 建立 EGame.netent_gonzosquest 遊戲場景

@cascade @base_avalanche_max
Scenario: 測試常規模式下 Avalanche 連鎖消去 1x 至最高 5x 滿貫乘倍封頂機制
  Given 玩家設定單線下注為 1 且使用固定 20 條押註線
  When 使用 Cheat 參數 [0, 26, 26, 0, 0] 進行 Spin
  Then Spin 回傳狀態應該成功
  And 掉落連消次數 CFN 應該為 0
  And 中獎項目應該包含乘倍為 1 的連線
  And 總贏分應該為 10
  When 進行連鎖掉落 Spin 旋轉
  Then Spin 回傳狀態應該成功
  And 玩家點數錢包免扣除下注成本
  And 掉落連消次數 CFN 應該為 1
  And 中獎項目應該包含乘倍為 2 的連線
  And 總贏分應該為 20
  When 進行連鎖掉落 Spin 旋轉
  Then Spin 回傳狀態應該成功
  And 玩家點數錢包免扣除下注成本
  And 掉落連消次數 CFN 應該為 2
  And 中獎項目應該包含乘倍為 3 的連線
  And 總贏分應該為 60
  When 進行連鎖掉落 Spin 旋轉
  Then Spin 回傳狀態應該成功
  And 玩家點數錢包免扣除下注成本
  And 掉落連消次數 CFN 應該為 3
  And 中獎項目應該包含乘倍為 4 的連線
  And 總贏分應該為 12
  When 進行連鎖掉落 Spin 旋轉
  Then Spin 回傳狀態應該成功
  And 玩家點數錢包免扣除下注成本
  And 掉落連消次數 CFN 應該為 4
  And 中獎項目應該包含乘倍為 5 的連線
  And 總贏分應該為 75
  When 進行連鎖掉落 Spin 旋轉
  Then Spin 回傳狀態應該成功
  And 玩家點數錢包免扣除下注成本
  And 掉落連消次數 CFN 應該為 5
  And 總贏分應該為 0

@freefall @freefall_avalanche_max
Scenario: 測試 Free Fall 免費模式下 Avalanche 連鎖消去 3x 至最高 15x 滿貫乘倍封頂機制
  Given 玩家設定單線下注為 1 且使用固定 20 條押註線
  When 使用 Cheat 參數 [4, 23, 13, 0, 0] 進行 Spin
  Then Spin 回傳狀態應該成功
  And 應該包含 ID 為 2 的 Scatter 符號 3 個且獲得 10 次 Free Fall 免費旋轉
  And 遊戲應該處於 FreeMode 免費模式
  And 剩餘免費旋轉次數 FSR 應該為 10
  When 進行連鎖掉落 Spin 旋轉
  Then Spin 回傳狀態應該成功
  And 玩家點數錢包免扣除下注成本
  And 掉落連消次數 CFN 應該為 1
  And 總贏分應該為 390
  And 剩餘免費旋轉次數 FSR 應該為 9
  When 進行連鎖掉落 Spin 旋轉
  Then Spin 回傳狀態應該成功
  And 玩家點數錢包免扣除下注成本
  And 掉落連消次數 CFN 應該為 2
  And 總贏分應該為 0
  And 剩餘免費旋轉次數 FSR 應該為 8
  When 使用 Cheat 參數 [0, 26, 26, 0, 0] 進行 Spin
  Then Spin 回傳狀態應該成功
  And 玩家點數錢包免扣除下注成本
  And 掉落連消次數 CFN 應該為 0
  And 中獎項目應該包含乘倍為 3 的連線
  And 總贏分應該為 30
  And 剩餘免費旋轉次數 FSR 應該為 7
  When 進行連鎖掉落 Spin 旋轉
  Then Spin 回傳狀態應該成功
  And 玩家點數錢包免扣除下注成本
  And 掉落連消次數 CFN 應該為 1
  And 中獎項目應該包含乘倍為 6 的連線
  And 總贏分應該為 60
  And 剩餘免費旋轉次數 FSR 應該為 6
  When 進行連鎖掉落 Spin 旋轉
  Then Spin 回傳狀態應該成功
  And 玩家點數錢包免扣除下注成本
  And 掉落連消次數 CFN 應該為 2
  And 中獎項目應該包含乘倍為 9 的連線
  And 總贏分應該為 180
  And 剩餘免費旋轉次數 FSR 應該為 5
  When 進行連鎖掉落 Spin 旋轉
  Then Spin 回傳狀態應該成功
  And 玩家點數錢包免扣除下注成本
  And 掉落連消次數 CFN 應該為 3
  And 中獎項目應該包含乘倍為 12 的連線
  And 總贏分應該為 36
  And 剩餘免費旋轉次數 FSR 應該為 4
  When 進行連鎖掉落 Spin 旋轉
  Then Spin 回傳狀態應該成功
  And 玩家點數錢包免扣除下注成本
  And 掉落連消次數 CFN 應該為 4
  And 中獎項目應該包含乘倍為 15 的連線
  And 總贏分應該為 225
  And 剩餘免費旋轉次數 FSR 應該為 3
  When 進行連鎖掉落 Spin 旋轉
  Then Spin 回傳狀態應該成功
  And 玩家點數錢包免扣除下注成本
  And 掉落連消次數 CFN 應該為 5
  And 總贏分應該為 0
  And 剩餘免費旋轉次數 FSR 應該為 2
