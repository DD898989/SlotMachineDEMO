Feature: Jokers100

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
  And 玩家個人目標 RTP 設定為 98.0
  And 建立 EGame.agt_100jokers 遊戲場景
  And 玩家設定單線下注為 1 且選擇 100 條押註線

@strawberry @order_02
Scenario: 測試全盤草莓五連線派彩
  Given 玩家設定單線下注為 1 且選擇 100 條押註線
  When 使用 Cheat 參數 [43, 20, 7, 20, 43] 進行 Spin
  Then Spin 回傳狀態應該成功
  And 盤面結果應該為:
    | 3 | 3 | 3 | 3 | 3 |
    | 3 | 3 | 3 | 3 | 3 |
    | 3 | 3 | 3 | 3 | 3 |
    | 3 | 3 | 3 | 3 | 3 |
  And 總贏分應該為 100000
  And 中獎線數應該為 100 條
  And 俱樂部水池餘額應該為 9900100
  And 玩家點數錢包餘額應該為 199900 點
  When 透過後台管理 API 查詢本次旋轉審計數據
  Then 達成水池風控嘗試次數 Attempts 應該為 1
  And 目標 RTP TargetRTP 應該為 98.0
  And 輪帶有效 RTP EffRTP 應該為 98.256580
  And 旋轉前彩金池金額 FundBefore 應該為 1000000
  And 旋轉結算後最新彩金池金額 FundAfter 應該為 1000000
  And 本次中彩金金額 JackWin 應該為 0
  And 彩金抽水比率 JackRate 應該為 0
  And 本次抽水注入彩金池金額 JackRent 應該為 0

@scatter @order_03
Scenario: 測試 Scatter 符號 3 個派彩
  Given 玩家設定單線下注為 1 且選擇 100 條押註線
  When 使用 Cheat 參數 [10, 19, 22, 4, 0] 進行 Spin
  Then Spin 回傳狀態應該成功
  And 盤面結果應該為:
    | 2 | 2 | 2 | 6 | 4 |
    | 7 | 3 | 1 | 6 | 4 |
    | 7 | 3 | 1 | 6 | 4 |
    | 7 | 3 | 1 | 6 | 4 |
  And 總贏分應該為 300
  And 應該包含 Scatter 符號 1 或 2 的派彩
  And 俱樂部水池餘額應該為 9999800
  And 玩家點數錢包餘額應該為 100200 點
  When 透過後台管理 API 查詢本次旋轉審計數據
  Then 達成水池風控嘗試次數 Attempts 應該為 1
  And 目標 RTP TargetRTP 應該為 98.0
  And 輪帶有效 RTP EffRTP 應該為 98.256580
  And 旋轉前彩金池金額 FundBefore 應該為 1000000
  And 旋轉結算後最新彩金池金額 FundAfter 應該為 1000000
  And 本次中彩金金額 JackWin 應該為 0
  And 彩金抽水比率 JackRate 應該為 0
  And 本次抽水注入彩金池金額 JackRent 應該為 0

@nowin @order_04
Scenario: 測試未中獎盤面與扣款
  Given 玩家設定單線下注為 1 且選擇 100 條押註線
  When 使用 Cheat 參數 [0, 4, 0, 4, 0] 進行 Spin
  Then Spin 回傳狀態應該成功
  And 盤面結果應該為:
    | 4 | 6 | 10 | 6 | 4 |
    | 4 | 6 | 10 | 6 | 4 |
    | 4 | 6 | 10 | 6 | 4 |
    | 4 | 6 | 10 | 6 | 4 |
  And 總贏分應該為 0
  And 未中獎且錢包扣款 100 點
  And 俱樂部水池餘額應該為 10000100
  And 玩家點數錢包餘額應該為 99900 點
  When 透過後台管理 API 查詢本次旋轉審計數據
  Then 達成水池風控嘗試次數 Attempts 應該為 1
  And 目標 RTP TargetRTP 應該為 98.0
  And 輪帶有效 RTP EffRTP 應該為 98.256580
  And 旋轉前彩金池金額 FundBefore 應該為 1000000
  And 旋轉結算後最新彩金池金額 FundAfter 應該為 1000000
  And 本次中彩金金額 JackWin 應該為 0
  And 彩金抽水比率 JackRate 應該為 0
  And 本次抽水注入彩金池金額 JackRent 應該為 0

@wild @order_05
Scenario: 測試小丑百搭符號替代五連線派彩
  Given 玩家設定單線下注為 1 且選擇 100 條押註線
  When 使用 Cheat 參數 [43, 0, 23, 0, 43] 進行 Spin
  Then Spin 回傳狀態應該成功
  And 盤面結果應該為:
    | 3 | 1 | 1 | 1 | 3 |
    | 3 | 1 | 1 | 1 | 3 |
    | 3 | 1 | 1 | 1 | 3 |
    | 3 | 1 | 1 | 1 | 3 |
  And 總贏分應該為 100000
  And 中獎線數應該為 100 條
  And 俱樂部水池餘額應該為 9900100
  And 玩家點數錢包餘額應該為 199900 點
  When 透過後台管理 API 查詢本次旋轉審計數據
  Then 達成水池風控嘗試次數 Attempts 應該為 1
  And 目標 RTP TargetRTP 應該為 98.0
  And 輪帶有效 RTP EffRTP 應該為 98.256580
  And 旋轉前彩金池金額 FundBefore 應該為 1000000
  And 旋轉結算後最新彩金池金額 FundAfter 應該為 1000000
  And 本次中彩金金額 JackWin 應該為 0
  And 彩金抽水比率 JackRate 應該為 0
  And 本次抽水注入彩金池金額 JackRent 應該為 0

@scatter5 @order_06
Scenario: 測試 Scatter 符號 5 個頂級滿貫派彩
  Given 玩家設定單線下注為 1 且選擇 100 條押註線
  When 使用 Cheat 參數 [10, 19, 22, 19, 10] 進行 Spin
  Then Spin 回傳狀態應該成功
  And 盤面結果應該為:
    | 2 | 2 | 2 | 2 | 2 |
    | 7 | 3 | 1 | 3 | 7 |
    | 7 | 3 | 1 | 3 | 7 |
    | 7 | 3 | 1 | 3 | 7 |
  And 總贏分應該為 50000
  And 應該包含 Scatter 符號 1 或 2 的派彩
  And 俱樂部水池餘額應該為 9950100
  And 玩家點數錢包餘額應該為 149900 點
  When 透過後台管理 API 查詢本次旋轉審計數據
  Then 達成水池風控嘗試次數 Attempts 應該為 1
  And 目標 RTP TargetRTP 應該為 98.0
  And 輪帶有效 RTP EffRTP 應該為 98.256580
  And 旋轉前彩金池金額 FundBefore 應該為 1000000
  And 旋轉結算後最新彩金池金額 FundAfter 應該為 1000000
  And 本次中彩金金額 JackWin 應該為 0
  And 彩金抽水比率 JackRate 應該為 0
  And 本次抽水注入彩金池金額 JackRent 應該為 0

@strawberry2 @order_07
Scenario: 測試草莓特有 2 連線派彩
  Given 玩家設定單線下注為 2 且選擇 20 條押註線
  When 使用 Cheat 參數 [43, 20, 0, 4, 0] 進行 Spin
  Then Spin 回傳狀態應該成功
  And 盤面結果應該為:
    | 3 | 3 | 10 | 6 | 4 |
    | 3 | 3 | 10 | 6 | 4 |
    | 3 | 3 | 10 | 6 | 4 |
    | 3 | 3 | 10 | 6 | 4 |
  And 總贏分應該為 160
  And 中獎線數應該為 20 條
  And 俱樂部水池餘額應該為 9999880
  And 玩家點數錢包餘額應該為 100120 點
  When 透過後台管理 API 查詢本次旋轉審計數據
  Then 達成水池風控嘗試次數 Attempts 應該為 1
  And 目標 RTP TargetRTP 應該為 98.0
  And 輪帶有效 RTP EffRTP 應該為 98.256580
  And 旋轉前彩金池金額 FundBefore 應該為 1000000
  And 旋轉結算後最新彩金池金額 FundAfter 應該為 1000000
  And 本次中彩金金額 JackWin 應該為 0
  And 彩金抽水比率 JackRate 應該為 0
  And 本次抽水注入彩金池金額 JackRent 應該為 0

@spin_fail @no_token @order_08
Scenario: 測試未攜帶授權 Token 進行 Spin 失敗
  When 未提供授權 Token 進行 Spin
  Then Spin 應該失敗且 HTTP 狀態碼為 401
  And Spin 錯誤訊息應該包含 "authorization is required"

@spin_fail @scene_not_found @order_09
Scenario: 測試使用不存在的遊戲場景 GID 進行 Spin 失敗
  When 使用不存在的 GID 999999 進行 Spin
  Then Spin 應該失敗且 HTTP 狀態碼為 404
  And Spin 錯誤訊息應該包含 "game with given ID is not opened"

@spin_fail @not_slot @order_10
Scenario: 測試對非老虎機類型遊戲場景發送 Spin 失敗
  Given 建立 EGame.aristocrat_americankeno 遊戲場景
  When 進行 Spin 旋轉
  Then Spin 應該失敗且 HTTP 狀態碼為 403
  And Spin 錯誤訊息應該包含 "specified GID refers to non-slot game"

@spin_fail @bad_bet @order_11
Scenario: 測試負數單線下注金額進行 Spin 失敗
  Given 玩家設定單線下注為 -1 且選擇 100 條押註線
  When 進行 Spin 旋轉
  Then Spin 應該失敗且 HTTP 狀態碼為 403
  And Spin 錯誤訊息應該包含 "wrong parameter"

@spin_fail @bad_sel @order_12
Scenario: 測試選擇超過上限之押線數進行 Spin 失敗
  Given 玩家設定單線下注為 1 且選擇 101 條押註線
  When 進行 Spin 旋轉
  Then Spin 應該失敗且 HTTP 狀態碼為 403
  And Spin 錯誤訊息應該包含 "wrong parameter"

@spin_fail @no_money @order_13
Scenario: 測試玩家點數錢包餘額不足進行 Spin 失敗
  Given 玩家點數錢包充值至固定金額 99 點
  And 玩家設定單線下注為 1 且選擇 100 條押註線
  When 進行 Spin 旋轉
  Then Spin 應該失敗且 HTTP 狀態碼為 403
  And Spin 錯誤訊息應該包含 "not enough money on balance"
  And 玩家點數錢包餘額應該為 99 點

@spin_fail @bad_bank @order_01
Scenario: 測試水池防爆風控機制觸發導致 Spin 失敗
  Given 俱樂部水池充值至固定金額 10 點
  And 玩家設定單線下注為 1 且選擇 100 條押註線
  When 使用 Cheat 參數 [43, 20, 7, 20, 43] 進行 Spin
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
    | ID | CID | UID | Wallet | Access | MRTP |
    | 1  | 1   | 3   | 100000 | 1      | 98   |
    | 2  | 1   | 4   | 1000   | 1      | 98   |
  And 驗證MySQL資料庫 "Story" 資料表的所有紀錄數值應該為:
    | ID | CID | UID | Game |
    | 1  | 1   | 3   | 13   |
  And 驗證MySQL資料庫 "Spinlog" 資料表的所有紀錄數值應該為:
    | ID | GID | Bet | Gain | Jack | Wallet | MRTP | Game | Wins |
  And 驗證MySQL資料庫 "Multlog" 資料表的所有紀錄數值應該為:
    | ID | GID | MRTP | Mult | Risk | Win | Gain | Wallet |
  And 驗證MySQL資料庫 "Walletlog" 資料表的所有紀錄數值應該為:
    | ID | CID | UID | AID | Wallet | Sum |
  And 驗證MySQL資料庫 "Banklog" 資料表的所有紀錄數值應該為:
    | ID | CID | Bank | Fund    | Lock | BankSum  | FundSum | LockSum |
    | 1  | 1   | 10   | 1000000 | 0    | -9999990 | 0       | 0       |

@bank_retry @attempts_2 @order_14
Scenario: 測試水池防爆風控機制觸發重試並在第 2 次嘗試成功 (Attempts = 2)
  Given 俱樂部水池充值至固定金額 10 點
  And 玩家設定單線下注為 1 且選擇 100 條押註線
  When 使用 Cheat 參數 [43, 20, 7, 20, 43, 0, 4, 0, 4, 0] 進行 Spin
  Then Spin 回傳狀態應該成功
  And 盤面結果應該為:
    | 4 | 6 | 10 | 6 | 4 |
    | 4 | 6 | 10 | 6 | 4 |
    | 4 | 6 | 10 | 6 | 4 |
    | 4 | 6 | 10 | 6 | 4 |
  And 總贏分應該為 0
  And 未中獎且錢包扣款 100 點
  And 俱樂部水池餘額應該為 110 點
  And 玩家點數錢包餘額應該為 99900 點
  When 透過後台管理 API 查詢本次旋轉審計數據
  Then 達成水池風控嘗試次數 Attempts 應該為 2
  And 目標 RTP TargetRTP 應該為 98.0
  And 輪帶有效 RTP EffRTP 應該為 98.256580
  And 旋轉前彩金池金額 FundBefore 應該為 1000000
  And 旋轉結算後最新彩金池金額 FundAfter 應該為 1000000
  And 本次中彩金金額 JackWin 應該為 0
  And 彩金抽水比率 JackRate 應該為 0
  And 本次抽水注入彩金池金額 JackRent 應該為 0



