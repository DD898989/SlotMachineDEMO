Feature: GonzosQuest

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

@top_win @mask1 @order_01
Scenario: 測試頂級五連線藍色面具 2500 倍滿貫派彩
  Given 玩家設定單線下注為 1 且使用固定 20 條押註線
  When 使用 Cheat 參數 [17, 2, 4, 2, 13] 進行 Spin
  Then Spin 回傳狀態應該成功
  And 盤面結果應該為:
    | 7 | 4 | 5 | 8 | 8 |
    | 3 | 3 | 3 | 3 | 3 |
    | 9 | 7 | 8 | 9 | 9 |
  And 總贏分應該為 2500
  And 中獎線數應該為 1 條
  And 俱樂部水池餘額應該為 9997520
  And 玩家點數錢包餘額應該為 102480 點

@wild @substitution @order_02
Scenario: 測試 Wild 問號百搭符號替代連線派彩
  Given 玩家設定單線下注為 1 且使用固定 20 條押註線
  When 使用 Cheat 參數 [0, 0, 2, 31, 0] 進行 Spin
  Then Spin 回傳狀態應該成功
  And 盤面結果應該為:
    | 6 | 6 | 8 | 5 | 7 |
    | 5 | 5 | 7 | 1 | 8 |
    | 7 | 4 | 5 | 7 | 9 |
  And 應該包含 ID 為 1 的 Wild 百搭符號替代派彩
  And 總贏分應該為 50
  And 俱樂部水池餘額應該為 9999970
  And 玩家點數錢包餘額應該為 100030 點

@cascade @avalanche_mult @order_03
Scenario: 測試 Cascade 連鎖掉落消除機制與 Avalanche 2 倍乘倍
  Given 玩家設定單線下注為 1 且使用固定 20 條押註線
  When 使用 Cheat 參數 [0, 0, 2, 0, 0] 進行 Spin
  Then Spin 回傳狀態應該成功
  And 盤面結果應該為:
    | 6 | 6 | 8 | 5 | 7 |
    | 5 | 5 | 7 | 6 | 8 |
    | 7 | 4 | 5 | 8 | 9 |
  And 總贏分應該為 15
  And 掉落連消次數 CFN 應該為 0
  And 盤面應標記需要消除掉落的符號 Hits
  And 俱樂部水池餘額應該為 10000005
  And 玩家點數錢包餘額應該為 99995 點
  When 進行連鎖掉落 Spin 旋轉
  Then Spin 回傳狀態應該成功
  And 玩家點數錢包免扣除下注成本
  And 掉落連消次數 CFN 應該為 1
  And 盤面結果應該為:
    | 8 | 8 | 9 | 5 | 7 |
    | 6 | 6 | 8 | 6 | 8 |
    | 7 | 4 | 7 | 8 | 9 |
  And 中獎項目應該包含乘倍為 2 的連線
  And 總贏分應該為 8
  And 玩家點數錢包餘額應該為 100003 點

@freefall @trigger @order_04
Scenario: 測試命中 3 個 Scatter 符號觸發 10 次 Free Fall 免費旋轉進入 FreeMode
  Given 玩家設定單線下注為 1 且使用固定 20 條押註線
  When 使用 Cheat 參數 [4, 23, 13, 0, 0] 進行 Spin
  Then Spin 回傳狀態應該成功
  And 盤面結果應該為:
    | 9 | 5 | 8 | 5 | 7 |
    | 5 | 9 | 3 | 6 | 8 |
    | 2 | 2 | 2 | 8 | 9 |
  And 應該包含 ID 為 2 的 Scatter 符號 3 個且獲得 10 次 Free Fall 免費旋轉
  And 遊戲應該處於 FreeMode 免費模式
  And 剩餘免費旋轉次數 FSR 應該為 10
  And 已完成免費旋轉次數 FSN 應該為 0
  And 玩家點數錢包餘額應該為 99980 點

@freefall @lifecycle_multiplier @order_05
Scenario: 測試 Free Fall 免費遊戲 6 倍掉落乘倍與 3 倍常規旋轉乘倍完整生命週期
  Given 玩家設定單線下注為 1 且使用固定 20 條押註線
  When 使用 Cheat 參數 [4, 23, 13, 0, 0] 進行 Spin
  Then Spin 回傳狀態應該成功
  And 遊戲應該處於 FreeMode 免費模式
  And 剩餘免費旋轉次數 FSR 應該為 10
  When 進行連鎖掉落 Spin 旋轉
  Then Spin 回傳狀態應該成功
  And 玩家點數錢包免扣除下注成本
  And 掉落連消次數 CFN 應該為 1
  And 中獎項目應該包含乘倍為 6 的連線
  And 總贏分應該為 390
  And 剩餘免費旋轉次數 FSR 應該為 9
  And 已完成免費旋轉次數 FSN 應該為 1
  When 進行連鎖掉落 Spin 旋轉
  Then Spin 回傳狀態應該成功
  And 玩家點數錢包免扣除下注成本
  And 掉落連消次數 CFN 應該為 2
  And 總贏分應該為 0
  And 剩餘免費旋轉次數 FSR 應該為 8
  And 已完成免費旋轉次數 FSN 應該為 2
  When 使用 Cheat 參數 [0, 0, 2, 0, 0] 進行 Spin
  Then Spin 回傳狀態應該成功
  And 玩家點數錢包免扣除下注成本
  And 掉落連消次數 CFN 應該為 0
  And 中獎項目應該包含乘倍為 3 的連線
  And 總贏分應該為 45
  And 剩餘免費旋轉次數 FSR 應該為 7
  And 已完成免費旋轉次數 FSN 應該為 3

@nowin @order_06
Scenario: 測試未中獎盤面與扣款
  Given 玩家設定單線下注為 1 且使用固定 20 條押註線
  When 使用 Cheat 參數 [0, 0, 0, 0, 0] 進行 Spin
  Then Spin 回傳狀態應該成功
  And 盤面結果應該為:
    | 6 | 6 | 7 | 5 | 7 |
    | 5 | 5 | 9 | 6 | 8 |
    | 7 | 4 | 8 | 8 | 9 |
  And 總贏分應該為 0
  And 未中獎且錢包扣款 20 點
  And 俱樂部水池餘額應該為 10000020
  And 玩家點數錢包餘額應該為 99980 點
  And 掉落連消次數 CFN 應該為 0
  And 盤面不應標記任何需要消除掉落的符號 Hits
  When 透過後台管理 API 查詢本次旋轉審計數據
  Then 達成水池風控嘗試次數 Attempts 應該為 1
  And 目標 RTP TargetRTP 應該為 95.310212
  And 輪帶有效 RTP EffRTP 應該為 95.310212
  And 輪帶序列 Reels 軸數應該為 5

@boundary @fixed_lines_set @order_07
Scenario: 測試透過 API 設定押線數時回傳功能不可用
  When 嘗試變更押註線數為 10 條
  Then 設定押註線數應該失敗且 HTTP 狀態碼為 403

@boundary @spin_badsel @order_08
Scenario: 測試 Spin 請求嘗試指定押線數時回傳功能不可用
  Given 玩家設定單線下注為 1 且選擇 20 條押註線
  When 使用 Cheat 參數 [0, 0, 0, 0, 0] 進行 Spin
  Then Spin 應該失敗且 HTTP 狀態碼為 403
  And Spin 錯誤訊息應該包含 "feature unavailable"

@spin_fail @no_token @order_09
Scenario: 測試未攜帶授權 Token 進行 Spin 失敗
  When 未提供授權 Token 進行 Spin
  Then Spin 應該失敗且 HTTP 狀態碼為 401
  And Spin 錯誤訊息應該包含 "authorization is required"

@spin_fail @no_money @order_10
Scenario: 測試玩家點數錢包餘額不足進行 Spin 失敗
  Given 玩家點數錢包充值至固定金額 19 點
  And 玩家設定單線下注為 1 且使用固定 20 條押註線
  When 進行 Spin 旋轉
  Then Spin 應該失敗且 HTTP 狀態碼為 403
  And Spin 錯誤訊息應該包含 "not enough money on balance"
  And 玩家點數錢包餘額應該為 19 點

@spin_fail @bad_bank @order_11
Scenario: 測試水池防爆風控機制觸發導致 Spin 失敗
  Given 俱樂部水池充值至固定金額 10 點
  And 玩家設定單線下注為 1 且使用固定 20 條押註線
  When 使用 Cheat 參數 [17, 2, 4, 2, 13] 進行 Spin
  Then Spin 應該失敗且 HTTP 狀態碼為 500
  And Spin 錯誤訊息應該包含 "can not generate spin with current bank balance"
  And 俱樂部水池餘額應該為 10 點
  And Sleep 5 秒
  And 驗證MySQL資料庫 "ClubData" 資料表的所有紀錄數值應該為:
    | ID | Name    | Bank | Fund    | Lock | Rate | MRTP |
    | 1  | virtual | 10   | 1000000 | 0    | 2.5  | 95   |
  And 驗證MySQL資料庫 "User" 資料表的所有紀錄數值應該為:
    | ID | Email               | Secret | Name    | GAL |
    | 1  | admin@example.org   | 0YBoaT | admin   | 31  |
    | 2  | dealer@example.org  | LtpkAr | dealer  | 3   |
    | 3  | player@example.org  | iVI05M | player  | 1   |
    | 4  | player2@example.org | iVI05M | player2 | 1   |
  And 驗證MySQL資料庫 "Props" 資料表的所有紀錄數值應該為:
    | ID | CID | UID | Wallet | Access | MRTP      |
    | 1  | 1   | 3   | 100000 | 1      | 95.310212 |
    | 2  | 1   | 4   | 1000   | 1      | 98        |
  And 驗證MySQL資料庫 "Story" 資料表的所有紀錄數值應該為:
    | ID | CID | UID | Game |
    | 1  | 1   | 3   | 11   |
    | 2  | 1   | 3   | 11   |
    | 3  | 1   | 3   | 11   |
    | 4  | 1   | 3   | 11   |
    | 5  | 1   | 3   | 11   |
    | 6  | 1   | 3   | 11   |
    | 7  | 1   | 3   | 11   |
    | 8  | 1   | 3   | 11   |
    | 9  | 1   | 3   | 11   |
    | 10 | 1   | 3   | 11   |
    | 11 | 1   | 3   | 11   |
  And 驗證MySQL資料庫 "Spinlog" 資料表的所有紀錄數值應該為:
    | ID | GID | Bet | Gain | Jack | Wallet | MRTP      | Game | Wins |
    | 1  | 1   | 20  | 2500 | 0    | 102480 | 95.310212 | *    | *    |
    | 2  | 2   | 20  | 50   | 0    | 100030 | 95.310212 | *    | *    |
    | 3  | 3   | 20  | 15   | 0    | 99995  | 95.310212 | *    | *    |
    | 4  | 3   | 0   | 8    | 0    | 100003 | 95.310212 | *    | *    |
    | 5  | 4   | 20  | 0    | 0    | 99980  | 95.310212 | *    | *    |
    | 6  | 5   | 20  | 0    | 0    | 99980  | 95.310212 | *    | *    |
    | 7  | 5   | 0   | 390  | 0    | 100370 | 95.310212 | *    | *    |
    | 8  | 5   | 0   | 0    | 0    | 100370 | 95.310212 | *    | *    |
    | 9  | 5   | 0   | 45   | 0    | 100415 | 95.310212 | *    | *    |
    | 10 | 6   | 20  | 0    | 0    | 99980  | 95.310212 | *    | *    |
  And 驗證MySQL資料庫 "Multlog" 資料表的所有紀錄數值應該為:
    | ID | GID | MRTP | Mult | Risk | Win | Gain | Wallet |
  And 驗證MySQL資料庫 "Walletlog" 資料表的所有紀錄數值應該為:
    | ID | CID | UID | AID | Wallet | Sum    |
    | 1  | 1   | 3   | 1   | 100000 | -2480  |
    | 2  | 1   | 3   | 1   | 100000 | -30    |
    | 3  | 1   | 3   | 1   | 100000 | -3     |
    | 4  | 1   | 3   | 1   | 100000 | 20     |
    | 5  | 1   | 3   | 1   | 100000 | -415   |
    | 6  | 1   | 3   | 1   | 100000 | 20     |
    | 7  | 1   | 3   | 1   | 19     | -99981 |
    | 8  | 1   | 3   | 1   | 100000 | 99981  |
  And 驗證MySQL資料庫 "Banklog" 資料表的所有紀錄數值應該為:
    | ID | CID | Bank     | Fund    | Lock | BankSum  | FundSum | LockSum |
    | 1  | 1   | 10000000 | 1000000 | 0    | 2480     | 0       | 0       |
    | 2  | 1   | 10000000 | 1000000 | 0    | 30       | 0       | 0       |
    | 3  | 1   | 10000000 | 1000000 | 0    | 3        | 0       | 0       |
    | 4  | 1   | 10000000 | 1000000 | 0    | -20      | 0       | 0       |
    | 5  | 1   | 10000000 | 1000000 | 0    | 415      | 0       | 0       |
    | 6  | 1   | 10000000 | 1000000 | 0    | -20      | 0       | 0       |
    | 7  | 1   | 10       | 1000000 | 0    | -9999990 | 0       | 0       |


