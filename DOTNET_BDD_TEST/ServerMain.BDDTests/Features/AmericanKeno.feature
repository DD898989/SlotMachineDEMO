Feature: AmericanKeno

Background:
  Given 建立獨立資料庫並寫入初始資料表 "club_data":
    | ID | Name    | Bank    | Fund    | Lock | Rate | MRTP |
    | 1  | virtual | 1000000 | 1000000 | 0    | 2.5  | 95   |
  And 寫入初始資料表 "user":
    | ID | Email              | Secret | Name    | GAL |
    | 1  | admin@example.org  | 0YBoaT | admin   | 31  |
    | 2  | dealer@example.org | LtpkAr | dealer  | 3   |
    | 3  | player@example.org | iVI05M | player  | 1   |
    | 4  | player2@example.org| iVI05M | player2 | 1   |
  And 寫入初始資料表 "props":
    | CID | UID | Wallet | Access | MRTP |
    | 1   | 3   | 100000 | 1      | 98   |
    | 1   | 4   | 1000   | 1      | 98   |
  And 啟動 Golang 伺服器
  And 玩家使用帳號 "player@example.org" 密碼 "iVI05M" 成功登入並取得授權 Token
  And 建立 EGame.aristocrat_americankeno 遊戲場景
  And 玩家點數錢包充值至固定金額 100000 點
  And 俱樂部水池充值至固定金額 1000000 點
  And 玩家設定 Keno 下注為 10 點

@keno @boundary
Scenario Outline: 測試 Keno 選號邊界與非法防呆攔截
  When 玩家嘗試選取 Keno 號碼 <選號>
  Then Keno 選號應該失敗且 HTTP 狀態碼為 403

  Examples:
    | 選號 |
    | [0]  |
    | [81] |
    | [1,2,3,4,5,6,7,8,9,10,11] |

@keno @paytable
Scenario Outline: 測試 Keno 選 10 號命中不同球數之賠率派彩
  Given 玩家設定 Keno 下注為 10 點
  And 玩家嘗試選取 Keno 號碼 [1,2,3,4,5,6,7,8,9,10]
  When 使用 Keno Cheat 參數開獎 <開獎號碼> 進行 Spin
  Then Keno 總贏分應該為 <預期贏分>

  Examples:
    | 命中數量 | 開獎號碼 | 預期贏分 | 說明 |
    | 0 | [11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,27,28,29,30] | 10 | 0 命中安慰獎 (賠率 1) |
    | 5 | [1,2,3,4,5,16,17,18,19,20,21,22,23,24,25,26,27,28,29,30] | 50 | 5 命中基本派彩 (賠率 5) |
    | 8 | [1,2,3,4,5,6,7,8,19,20,21,22,23,24,25,26,27,28,29,30] | 5000 | 8 命中大獎 (賠率 500) |
    | 10 | [1,2,3,4,5,6,7,8,9,10,21,22,23,24,25,26,27,28,29,30] | 100000 | 10 命中特大獎滿貫 (賠率 10000) |

@keno @db_check
Scenario: 驗證 AmericanKeno 測試完成後 MySQL 各資料表狀態
  Given Sleep 10 秒
  Then 驗證MySQL資料庫 "ClubData" 資料表的所有紀錄數值應該為:
    | ID | Name    | Bank | Fund    | Lock | Rate | MRTP |
    | 1  | virtual | *    | 1000000 | 0    | 2.5  | 95   |
  And 驗證MySQL資料庫 "User" 資料表的所有紀錄數值應該為:
    | ID | Email               | Secret | Name    | GAL |
    | 1  | admin@example.org   | 0YBoaT | admin   | 31  |
    | 2  | dealer@example.org  | LtpkAr | dealer  | 3   |
    | 3  | player@example.org  | iVI05M | player  | 1   |
    | 4  | player2@example.org | iVI05M | player2 | 1   |
  And 驗證MySQL資料庫 "Props" 資料表的所有紀錄數值應該為:
    | ID | CID | UID | Wallet | Access | MRTP |
    | 1  | 1   | 3   | *      | 1      | *    |
    | 2  | 1   | 4   | 1000   | 1      | 98   |
  And 驗證MySQL資料庫 "Story" 資料表的所有紀錄數值應該為:
    | ID | CID | UID | Game |
    | 1  | 1   | 3   | 16   |
    | 2  | 1   | 3   | 16   |
    | 3  | 1   | 3   | 16   |
    | 4  | 1   | 3   | 16   |
    | 5  | 1   | 3   | 16   |
    | 6  | 1   | 3   | 16   |
    | 7  | 1   | 3   | 16   |
    | 8  | 1   | 3   | 16   |
  And 驗證MySQL資料庫 "Spinlog" 資料表的所有紀錄數值應該為:
    | ID | GID | Bet | Gain | Jack | Wallet | MRTP | Game | Wins |
    | 1  | *   | 10  | *    | 0    | *      | 98   | *    | *    |
    | 2  | *   | 10  | *    | 0    | *      | 98   | *    | *    |
    | 3  | *   | 10  | *    | 0    | *      | 98   | *    | *    |
    | 4  | *   | 10  | *    | 0    | *      | 98   | *    | *    |
    | 5  | *   | 10  | *    | 0    | *      | 98   | *    | *    |
    | 6  | *   | 10  | *    | 0    | *      | 98   | *    | *    |
    | 7  | *   | 10  | *    | 0    | *      | 98   | *    | *    |
    | 8  | *   | 10  | *    | 0    | *      | 98   | *    | *    |
  And 驗證MySQL資料庫 "Multlog" 資料表的所有紀錄數值應該為:
    | ID | GID | MRTP | Mult | Risk | Win | Gain | Wallet |
  And 驗證MySQL資料庫 "Walletlog" 資料表的所有紀錄數值應該為:
    | ID | CID | UID | AID | Wallet | Sum |
    | 1  | 1   | 3   | 1   | *      | *   |
    | 2  | 1   | 3   | 1   | *      | *   |
    | 3  | 1   | 3   | 1   | *      | *   |
    | 4  | 1   | 3   | 1   | *      | *   |
  And 驗證MySQL資料庫 "Banklog" 資料表的所有紀錄數值應該為:
    | ID | CID | Bank | Fund    | Lock | BankSum | FundSum | LockSum |
    | 1  | 1   | *    | 1000000 | 0    | *       | 0       | 0       |
    | 2  | 1   | *    | 1000000 | 0    | *       | 0       | 0       |
    | 3  | 1   | *    | 1000000 | 0    | *       | 0       | 0       |
    | 4  | 1   | *    | 1000000 | 0    | *       | 0       | 0       |
