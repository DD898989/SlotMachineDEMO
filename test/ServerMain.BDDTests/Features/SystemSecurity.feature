Feature: SystemSecurity

Background:
  Given 建立獨立資料庫並寫入初始資料表 "ClubData":
    | ID | Name    | Bank    | Fund    | Lock | Rate | MRTP |
    | 1  | virtual | 1000000 | 1000000 | 0    | 2.5  | 95   |
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
  And 系統初始設定準備完成
  And 玩家使用帳號 "player@example.org" 密碼 "iVI05M" 成功登入並取得授權 Token
  And 玩家個人目標 RTP 設定為 98.0
  And 建立 EGame.agt_100jokers 遊戲場景

@security @zero_sum
Scenario: 測試連續旋轉後之錢包與水池零和校驗 (Zero-Sum Balance)
  Given 俱樂部水池充值至固定金額 1000000 點
  And 俱樂部彩金池充值至固定金額 500000 點
  And 玩家點數錢包充值至固定金額 50000 點
  And 玩家設定單線下注為 1 且選擇 100 條押註線
  And 記錄初始資產總和 (錢包 + 水池 + 彩金池)
  When 進行 50 次隨機旋轉
  Then 最終資產總和必須等於初始資產總和 (零和校驗通過)

@security @wallet_limit
Scenario: 測試單筆充值上限防穿透與負數防呆
  Given 玩家嘗試充值單筆 150000 點
  Then 充值應被拒絕且 HTTP 狀態碼為 400
  Given 玩家嘗試充值單筆 -150000 點
  Then 充值應被拒絕且 HTTP 狀態碼為 400

@security @isolation
Scenario: 測試俱樂部水池與結算
  Given 玩家隸屬俱樂部 1 且水池設為 1000000 點
  And 玩家點數錢包充值至固定金額 50000 點
  And 玩家設定單線下注為 1 且選擇 100 條押註線
  When 使用 Cheat 參數 [43, 0, 23, 0, 43] 進行 Spin
  Then Spin 回傳狀態應該成功
  And 總贏分應該為 100000
  And 俱樂部 1 水池餘額應該為 900100

@security @dynamic_rtp
Scenario: 測試動態 RTP 目標設定與輪帶切換
  Given 玩家個人目標 RTP 設定為 98.0
  And 玩家設定單線下注為 1 且選擇 100 條押註線
  When 使用 Cheat 參數 [0, 0, 0, 0, 0] 進行 Spin
  When 透過後台管理 API 查詢本次旋轉審計數據
  Then 目標 RTP TargetRTP 應該為 98.0
  And 輪帶有效 RTP EffRTP 應該為 98.256580
  Given 玩家個人目標 RTP 設定為 92.0
  When 使用 Cheat 參數 [0, 0, 0, 0, 0] 進行 Spin
  When 透過後台管理 API 查詢本次旋轉審計數據
  Then 目標 RTP TargetRTP 應該為 92.0
  And 輪帶有效 RTP 應隨之調整對齊為 92.0 (容差 1.0)
