Feature: BookOfRa

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
  And 玩家個人目標 RTP 設定為 95.0
  And 建立 EGame.novomatic_bookofradeluxe 遊戲場景

@freemode @trigger @order_02
Scenario: 測試命中 3 個 Scatter 符號觸發 10 次 Free Spins 並進入 FreeMode
  Given 玩家設定單線下注為 1 且選擇 10 條押註線
  When 使用 Cheat 參數 [1, 1, 1, 3, 3] 進行 Spin
  Then Spin 回傳狀態應該成功
  And 盤面結果應該為:
    | 9  | 7  | 5  | 9 | 6 |
    | 10 | 10 | 10 | 6 | 8 |
    | 5  | 5  | 9  | 1 | 3 |
  And 應該包含 ID 為 10 的 Scatter 符號 3 個且獲得 10 次免費旋轉
  And 遊戲應該處於 FreeMode 免費模式
  And 剩餘免費旋轉次數 FSR 應該為 10
  And 已完成免費旋轉次數 FSN 應該為 0
  And 特殊擴展符號 ES 應該已被選定且範圍在 1 到 9 之間
  And 總贏分應該為 80
  And 俱樂部水池餘額應該為 9999930
  And 玩家點數錢包餘額應該為 100070 點

@freemode @expanding_symbol @order_03
Scenario: 測試 FreeMode 免費遊戲特殊擴展符號 ES 全線擴展派彩機制
  Given 玩家設定單線下注為 1 且選擇 10 條押註線
  When 使用 Cheat 參數 [1, 1, 1, 3, 3] 進行 Spin
  Then Spin 回傳狀態應該成功
  And 遊戲應該處於 FreeMode 免費模式
  And 剩餘免費旋轉次數 FSR 應該為 10
  And 特殊擴展符號 ES 應該已被選定且範圍在 1 到 9 之間
  When 透過作弊產生包含當前 ES 符號的盤面並 Spin
  Then Spin 回傳狀態應該成功
  And 玩家點數錢包免扣除下注成本
  And 遊戲應該處於 FreeMode 免費模式
  And 剩餘免費旋轉次數 FSR 應該為 9
  And 已完成免費旋轉次數 FSN 應該為 1
  And 應該包含特殊擴展符號 ES 全線 10 條線擴展派彩
  And 總贏分應該大於 0

@freemode @lifecycle @order_04
Scenario: 測試 FreeMode 免費遊戲生命週期（免扣款旋轉、狀態推進與 Retrigger 累加）
  Given 玩家設定單線下注為 1 且選擇 10 條押註線
  When 使用 Cheat 參數 [1, 1, 1, 3, 3] 進行 Spin
  Then Spin 回傳狀態應該成功
  And 遊戲應該處於 FreeMode 免費模式
  And 剩餘免費旋轉次數 FSR 應該為 10
  And 玩家點數錢包餘額應該為 100070 點
  When 使用 Cheat 參數 [0, 0, 0, 0, 0] 進行 Spin
  Then Spin 回傳狀態應該成功
  And 玩家點數錢包免扣除下注成本
  And 遊戲應該處於 FreeMode 免費模式
  And 剩餘免費旋轉次數 FSR 應該為 9
  And 已完成免費旋轉次數 FSN 應該為 1
  When 使用 Cheat 參數 [14, 5, 25, 1, 0] 進行 Spin
  Then Spin 回傳狀態應該成功
  And 盤面結果應該為:
    | 9  | 8  | 5  | 9 | 7 |
    | 10 | 10 | 10 | 6 | 4 |
    | 5  | 6  | 9  | 7 | 9 |
  And 應該包含 ID 為 10 的 Scatter 符號 3 個且獲得 10 次免費旋轉
  And 剩餘免費旋轉次數 FSR 應該為 18
  And 已完成免費旋轉次數 FSN 應該為 2
  And 玩家點數錢包免扣除下注成本

@freemode @scatter5 @order_05
Scenario: 測試 5 個 Scatter 觸發最高額 Scatter 滿貫派彩與免費旋轉
  Given 玩家設定單線下注為 1 且選擇 10 條押註線
  When 使用 Cheat 參數 [1, 1, 1, 1, 1] 進行 Spin
  Then Spin 回傳狀態應該成功
  And 盤面結果應該為:
    | 9  | 7  | 5  | 8  | 8  |
    | 10 | 10 | 10 | 10 | 10 |
    | 5  | 5  | 9  | 9  | 6  |
  And 應該包含 ID 為 10 的 Scatter 符號 5 個且獲得 10 次免費旋轉
  And 總贏分應該為 2110
  And 遊戲應該處於 FreeMode 免費模式
  And 剩餘免費旋轉次數 FSR 應該為 10
  And 俱樂部水池餘額應該為 9997900
  And 玩家點數錢包餘額應該為 102100 點

@explorer5 @order_01
Scenario: 測試常規五連線探險家最高賠率 5000 倍派彩
  Given 玩家設定單線下注為 1 且選擇 10 條押註線
  When 使用 Cheat 參數 [7, 30, 10, 4, 35] 進行 Spin
  Then Spin 回傳狀態應該成功
  And 盤面結果應該為:
    | 7 | 6 | 7 | 6 | 7 |
    | 1 | 1 | 1 | 1 | 1 |
    | 6 | 7 | 8 | 5 | 5 |
  And 總贏分應該為 5000
  And 中獎線數應該為 1 條
  And 遊戲不應處於 FreeMode 免費模式
  And 俱樂部水池餘額應該為 9995010
  And 玩家點數錢包餘額應該為 104990 點
  And Sleep 5 秒
  And 驗證MySQL資料庫 "ClubData" 資料表的所有紀錄數值應該為:
    | ID | Name    | Bank    | Fund    | Lock | Rate | MRTP |
    | 1  | virtual | 9995010 | 1000000 | 0    | 2.5  | 95   |
  And 驗證MySQL資料庫 "User" 資料表的所有紀錄數值應該為:
    | ID | Email               | Secret | Name    | GAL |
    | 1  | admin@example.org   | 0YBoaT | admin   | 31  |
    | 2  | dealer@example.org  | LtpkAr | dealer  | 3   |
    | 3  | player@example.org  | iVI05M | player  | 1   |
    | 4  | player2@example.org | iVI05M | player2 | 1   |
  And 驗證MySQL資料庫 "Props" 資料表的所有紀錄數值應該為:
    | ID | CID | UID | Wallet | Access | MRTP |
    | 1  | 1   | 3   | 104990 | 1      | 95   |
    | 2  | 1   | 4   | 1000   | 1      | 98   |
  And 驗證MySQL資料庫 "Story" 資料表的所有紀錄數值應該為:
    | ID | CID | UID | Game |
    | 1  | 1   | 3   | 2    |
  And 驗證MySQL資料庫 "Spinlog" 資料表的所有紀錄數值應該為:
    | ID | GID | Bet | Gain | Jack | Wallet | MRTP | Game | Wins |
    | 1  | 1   | 10  | 5000 | 0    | 104990 | 95   | *    | *    |
  And 驗證MySQL資料庫 "Multlog" 資料表的所有紀錄數值應該為:
    | ID | GID | MRTP | Mult | Risk | Win | Gain | Wallet |
  And 驗證MySQL資料庫 "Walletlog" 資料表的所有紀錄數值應該為:
    | ID | CID | UID | AID | Wallet | Sum |
  And 驗證MySQL資料庫 "Banklog" 資料表的所有紀錄數值應該為:
    | ID | CID | Bank | Fund | Lock | BankSum | FundSum | LockSum |


