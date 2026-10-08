Feature: Slotopol

Background:
  Given 建立獨立資料庫並寫入初始資料表 "ClubData":
    | ID | Name    | Bank     | Fund    | Lock | Rate | MRTP |
    | 1  | virtual | 10000000 | 3000000 | 0    | 2.5  | 95   |
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
  And 俱樂部彩金池充值至固定金額 3000000 點
  And 玩家個人目標 RTP 設定為 99.760556
  And 建立 EGame.megajack_slotopol 遊戲場景

@jackpot
Scenario: 測試五連線 Eldorado 觸發彩金池派彩 (Jackpot Hit)
  Given 玩家設定單線下注為 1 且選擇 21 條押註線
  When 使用 Cheat 參數 [5, 14, 2, 16, 1] 進行 Spin
  Then Spin 回傳狀態應該成功
  And 盤面結果應該為:
    | 11 | 11 | 11 | 11 | 11 |
    | 12 | 12 | 12 | 11 | 13 |
    | 11 | 11 | 11 | 11 | 12 |
  And 總贏分應該為 16040
  And 中獎線數應該為 8 條
  And 應該包含彩金 Jackpot 派彩
  And Spin 回傳之彩金池餘額 JpFund 應該為 3000000 點
  And 俱樂部水池餘額應該為 9983980.47
  And 玩家點數錢包餘額應該為 128601.91 點
  When 透過後台管理 API 查詢本次旋轉審計數據
  Then 達成水池風控嘗試次數 Attempts 應該為 1
  And 目標 RTP TargetRTP 應該為 99.760556
  And 輪帶有效 RTP EffRTP 應該為 99.760556
  And 旋轉前彩金池金額 FundBefore 應該為 3000000
  And 旋轉結算後最新彩金池金額 FundAfter 應該為 2987417.62
  And 本次中彩金金額 JackWin 應該為 12582.91
  And 彩金抽水比率 JackRate 應該為 2.5
  And 本次抽水注入彩金池金額 JackRent 應該為 0.53

@bonus @spin9_bonus
Scenario: 測試五連線 Spin 符號觸發 Eldorado 幸運輪盤獎勵遊戲 (Spin 9 Bonus)
  Given 玩家設定單線下注為 1 且選擇 21 條押註線
  When 使用 Cheat 參數 [3, 10, 0, 0, 3, 9, 3, 5, 2, 11, 1, 4, 7, 10] 進行 Spin
  Then Spin 回傳狀態應該成功
  And 盤面結果應該為:
    | 12 | 12 | 12 | 12 | 12 |
    | 13 | 8  | 13 | 1  | 6  |
    | 11 | 12 | 11 | 2  | 4  |
  And 應該包含 ID 為 4 的幸運輪盤 Bonus 獎勵
  And ID 為 4 的輪盤獎勵旋轉次數應該為 9 次
  And 總贏分應該為 450

@bonus @monopoly_bonus
Scenario: 測試五連線 Dice 符號觸發 Monopoly 大富翁走棋獎勵遊戲 (Monopoly Bonus)
  Given 玩家設定單線下注為 1 且選擇 21 條押註線
  When 使用 Cheat 參數 [0, 3, 1, 3, 0, 1, 1, 1, 1, 2, 1, 5] 進行 Spin
  Then Spin 回傳狀態應該成功
  And 盤面結果應該為:
    | 13 | 13 | 13 | 13 | 13 |
    | 1  | 9  | 11 | 6  | 11 |
    | 5  | 6  | 12 | 5  | 13 |
  And 應該包含 ID 為 5 的大富翁 Bonus 獎勵
  And ID 為 5 的大富翁走棋步數應該為 7 步
  And 總贏分應該為 145

@bonus @monopoly_zero_win_guarantee
Scenario: 測試五連線 Dice 符號觸發 Monopoly 大富翁走棋 0 分保底 5000 倍彩金機制
  Given 玩家設定單線下注為 1 且選擇 21 條押註線
  When 使用 Cheat 參數 [0, 3, 1, 3, 0, 6, 4, 6, 4, 6, 4, 6] 進行 Spin
  Then Spin 回傳狀態應該成功
  And 盤面結果應該為:
    | 13 | 13 | 13 | 13 | 13 |
    | 1  | 9  | 11 | 6  | 11 |
    | 5  | 6  | 12 | 5  | 13 |
  And 應該包含 ID 為 5 的大富翁 Bonus 獎勵
  And ID 為 5 的大富翁走棋步數應該為 7 步
  And 總贏分應該為 5000

@scatter @scatter5
Scenario: 測試 Scatter 美元符號 5 個全盤大滿貫派彩
  Given 玩家設定單線下注為 1 且選擇 21 條押註線
  When 使用 Cheat 參數 [1, 30, 10, 1, 15] 進行 Spin
  Then Spin 回傳狀態應該成功
  And 盤面結果應該為:
    | 1  | 1  | 1  | 1  | 1  |
    | 5  | 12 | 12 | 2  | 9  |
    | 12 | 9  | 13 | 13 | 12 |
  And 總贏分應該為 21000
  And 應該包含 Scatter 符號 1 或 2 的派彩
  And 俱樂部水池餘額應該為 9979020.47
  And 玩家點數錢包餘額應該為 120979 點

@wild @wild_mult
Scenario: 測試 Wild 百搭符號替代遊艇五連線派彩翻倍
  Given 玩家設定單線下注為 1 且選擇 21 條押註線
  When 使用 Cheat 參數 [19, 2, 2, 12, 11] 進行 Spin
  Then Spin 回傳狀態應該成功
  And 盤面結果應該為:
    | 10 | 10 | 11 | 10 | 10 |
    | 13 | 13 | 12 | 9  | 7  |
    | 9  | 9  | 11 | 7  | 12 |
  And 總贏分應該為 258
  And 中獎線數應該為 8 條
  And 俱樂部水池餘額應該為 9999762.47
  And 玩家點數錢包餘額應該為 100237 點

@payline @sel1_only
Scenario: 測試選擇 1 條押線時僅結算第一線且未選中線不予計獎
  Given 玩家設定單線下注為 1 且選擇 1 條押註線
  When 使用 Cheat 參數 [19, 2, 2, 12, 11] 進行 Spin
  Then Spin 回傳狀態應該成功
  And 總贏分應該為 0
  And 未中獎且錢包扣款 1 點
  And 俱樂部水池餘額應該為 10000000.97
  And 玩家點數錢包餘額應該為 99999 點

@gamble @doubleup_nogain
Scenario: 測試未中獎狀態下發起 Double Up 比倍失敗
  Given 玩家設定單線下注為 1 且選擇 21 條押註線
  When 使用 Cheat 參數 [2, 1, 0, 0, 0] 進行 Spin
  Then Spin 回傳狀態應該成功
  And 總贏分應該為 0
  When 發起倍率為 2 的全額比倍 Double Up
  Then 比倍應該失敗且 HTTP 狀態碼為 403
  And 比倍錯誤訊息應該包含 "no money won"

@gamble @doubleup_badmult
Scenario: 測試使用超過上限之非法倍率發起 Double Up 比倍失敗
  Given 玩家設定單線下注為 1 且選擇 21 條押註線
  When 使用 Cheat 參數 [19, 2, 2, 12, 11] 進行 Spin
  Then Spin 回傳狀態應該成功
  And 總贏分應該為 258
  When 發起倍率為 11 的全額比倍 Double Up
  Then 比倍應該失敗且 HTTP 狀態碼為 400
  And 比倍錯誤訊息應該包含 "given value exceeds the limit"

@fund_accumulation @nowin
Scenario: 測試未中獎盤面與彩金池持續抽水累積 (Fund Accumulation)
  Given 玩家設定單線下注為 1 且選擇 21 條押註線
  When 使用 Cheat 參數 [2, 1, 0, 0, 0] 進行 Spin
  Then Spin 回傳狀態應該成功
  And 盤面結果應該為:
    | 5  | 5  | 12 | 12 | 13 |
    | 12 | 10 | 13 | 1  | 11 |
    | 13 | 13 | 11 | 2  | 13 |
  And 總贏分應該為 0
  And 未中獎且錢包扣款 21 點
  And 俱樂部水池餘額應該為 10000020.47
  And 玩家點數錢包餘額應該為 99979 點
  When 透過後台管理 API 查詢本次旋轉審計數據
  Then 達成水池風控嘗試次數 Attempts 應該為 1
  And 目標 RTP TargetRTP 應該為 99.760556
  And 輪帶有效 RTP EffRTP 應該為 99.760556
  And 旋轉前彩金池金額 FundBefore 應該為 3000000
  And 旋轉結算後最新彩金池金額 FundAfter 應該為 3000000.53
  And 本次中彩金金額 JackWin 應該為 0
  And 彩金抽水比率 JackRate 應該為 2.5
  And 本次抽水注入彩金池金額 JackRent 應該為 0.53

@spin_fail @bad_bank
Scenario: 測試水池餘額不足以支付彩金時觸發風控
  Given 俱樂部水池充值至固定金額 10 點
  And 玩家設定單線下注為 1 且選擇 21 條押註線
  When 使用 Cheat 參數 [5, 14, 2, 16, 1] 進行 Spin
  Then Spin 應該失敗且 HTTP 狀態碼為 500
  And Spin 錯誤訊息應該包含 "can not generate spin with current bank balance"
  And 俱樂部水池餘額應該為 10 點

@spin_fail @bad_sel
Scenario: 測試選擇超過 21 條上限之押線數進行 Spin 失敗
  Given 玩家設定單線下注為 1 且選擇 22 條押註線
  When 進行 Spin 旋轉
  Then Spin 應該失敗且 HTTP 狀態碼為 403
  And Spin 錯誤訊息應該包含 "wrong parameter"

@spin_fail @no_token
Scenario: 測試未攜帶授權 Token 進行 Spin 失敗
  When 未提供授權 Token 進行 Spin
  Then Spin 應該失敗且 HTTP 狀態碼為 401
  And Spin 錯誤訊息應該包含 "authorization is required"

@spin_fail @scene_not_found
Scenario: 測試使用不存在的遊戲場景 GID 進行 Spin 失敗
  When 使用不存在的 GID 999999 進行 Spin
  Then Spin 應該失敗且 HTTP 狀態碼為 404
  And Spin 錯誤訊息應該包含 "game with given ID is not opened"

@spin_fail @not_slot
Scenario: 測試對非老虎機類型遊戲場景發送 Spin 失敗
  Given 建立 EGame.aristocrat_americankeno 遊戲場景
  When 進行 Spin 旋轉
  Then Spin 應該失敗且 HTTP 狀態碼為 403
  And Spin 錯誤訊息應該包含 "specified GID refers to non-slot game"

@spin_fail @bad_bet
Scenario: 測試負數單線下注金額進行 Spin 失敗
  Given 玩家設定單線下注為 -1 且選擇 21 條押註線
  When 進行 Spin 旋轉
  Then Spin 應該失敗且 HTTP 狀態碼為 403
  And Spin 錯誤訊息應該包含 "wrong parameter"

@spin_fail @no_money
Scenario: 測試玩家點數錢包餘額不足進行 Spin 失敗
  Given 玩家點數錢包充值至固定金額 10 點
  And 玩家設定單線下注為 1 且選擇 21 條押註線
  When 進行 Spin 旋轉
  Then Spin 應該失敗且 HTTP 狀態碼為 403
  And Spin 錯誤訊息應該包含 "not enough money on balance"
  And 玩家點數錢包餘額應該為 10 點
  
Scenario: 驗證 Slotopol 測試完成後 MySQL 各資料表狀態
  And Sleep 10 秒
  And 驗證MySQL資料庫 "ClubData" 資料表的所有紀錄數值應該為:
    | ID | Name    | Bank | Fund    | Lock | Rate | MRTP |
    | 1  | virtual | 9999999.999999993    | 3000000 | 0    | 2.5  | 95   |
  And 驗證MySQL資料庫 "User" 資料表的所有紀錄數值應該為:
    | ID | Email               | Secret | Name    | GAL |
    | 1  | admin@example.org   | 0YBoaT | admin   | 31  |
    | 2  | dealer@example.org  | LtpkAr | dealer  | 3   |
    | 3  | player@example.org  | iVI05M | player  | 1   |
    | 4  | player2@example.org | iVI05M | player2 | 1   |
  And 驗證MySQL資料庫 "Props" 資料表的所有紀錄數值應該為:
    | ID | CID | UID | Wallet | Access | MRTP      |
    | 1  | 1   | 3   | 100000 | 1      | 99.760556 |
    | 2  | 1   | 4   | 1000   | 1      | 98        |
  And 驗證MySQL資料庫 "Story" 資料表的所有紀錄數值應該為:
    | ID | CID | UID | Game |
    | 1  | 1   | 3   | 12   |
  And 驗證MySQL資料庫 "Spinlog" 資料表的所有紀錄數值應該為:
    | ID | GID | Bet | Gain  | Jack     | Wallet    | MRTP      | Game | Wins |
    | 1  | 1   | 21  | 21000 | 0        | 120979    | 99.760556 | *    | *    |
    | 2  | 2   | 21  | 258   | 0        | 100237    | 99.760556 | *    | *    |
    | 3  | 3   | 21  | 5000  | 0        | 104979    | 99.760556 | *    | *    |
    | 4  | 4   | 21  | 145   | 0        | 100124    | 99.760556 | *    | *    |
    | 5  | 5   | 21  | 16040 | 12582.91 | 128601.91 | 99.760556 | *    | *    |
    | 6  | 6   | 21  | 450   | 0        | 100429    | 99.760556 | *    | *    |
    | 7  | 8   | 21  | 258   | 0        | 100237    | 99.760556 | *    | *    |
    | 8  | 11  | 21  | 0     | 0        | 99979     | 99.760556 | *    | *    |
    | 9  | 12  | 21  | 0     | 0        | 99979     | 99.760556 | *    | *    |
    | 10 | 17  | 1   | 0     | 0        | 99999     | 99.760556 | *    | *    |
  And 驗證MySQL資料庫 "Multlog" 資料表的所有紀錄數值應該為:
    | ID | GID | MRTP | Mult | Risk | Win | Gain | Wallet |
  And 驗證MySQL資料庫 "Walletlog" 資料表的所有紀錄數值應該為:
    | ID | CID | UID | AID | Wallet | Sum    |
    | 10 | 1   | 3   | 1   | 10     | -99990 |
  And 驗證MySQL資料庫 "Banklog" 資料表的所有紀錄數值應該為:
    | ID | CID | Bank | Fund    | Lock | BankSum  | FundSum | LockSum |
    | 19 | 1   | 10   | 3000000 | 0    | -9999990 | 0       | 0       |
