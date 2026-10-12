# SlotMachineDEMO - 各分支架構介紹

本專案改動自開源專案 [slotopol/server](https://github.com/slotopol/server)（原為 Go 語言實作），將其重構至 **.NET 10 (C#)**，並透過 BDD 測試與不同資料庫架構模式，探討高併發下的效能、一致性與架構權衡。

---

## 🎯 核心特色與架構決策

1. **BDD & HTML 視覺化驗證**
   - 採用 Gherkin 語法（SpecFlow / Reqnroll）撰寫行為驅動測試（BDD），並產出 HTML 視覺化報告。
   - 讓工程師與企劃人員能透過具體情境案例（Given / When / Then）快速理解複雜的老虎機業務規則（線路計算、賠率表、Cascade 消除、Jackpot）與後端程式邏輯。
   - **線上點擊直接遊覽報告**：
     - [👉 Gonzo's Quest Avalanche 連鎖掉落階梯視覺化 & Cascade 核心剖析](https://htmlpreview.github.io/?https://github.com/DD898989/SlotMachineDEMO/blob/dbbasic/test/ServerMain.BDDTests/Features/GonzosQuestAvalanche.feature.html)
     - [👉 MegaJack Slotopol 10 次指定下注模擬視覺化 (BDD Simulation)](https://htmlpreview.github.io/?https://github.com/DD898989/SlotMachineDEMO/blob/dbbasic/test/ServerMain.BDDTests/Features/SlotopolSimulation.feature.html)

2. **為什麼不使用 Redis？**
   - 老虎機的核心業務在每次 Spin 均為標準的**「1 讀 1 寫」**（讀取玩家狀態與錢包 ➜ 運算 ➜ 寫回異動）。
   - 缺乏一般快取「讀多寫少」的複用效益，引入 Redis 僅會增加額外的網路跳轉（Hop）與分散式雙寫同步負擔，實務上沒有意義。

---

## 🌿 各分支介紹

### 1. `dbbasic`（傳統資料庫直存）
- **模式**：Direct Database Access（標準 CRUD）
- **核心機制**：
  - 每個 HTTP 請求獨立建立 EF Core `DbContext`
  - 即時查詢 `Users`、`Stories`、`ClubData`、`Props`
  - 運算後直接以 `ExecuteUpdate` 與 `SaveChanges` 更新資料庫
- **特性**：標準 ACID 強一致性；瓶頸在於資料庫連線池與行鎖競爭
- **壓測表現**：~154 RPS｜平均延遲 2,778 ms

### 2. `reqcoalescing`（請求合併與批次處理）
- **模式**：Request Coalescing（微批次事務聚合）
- **核心機制**：
  - 以 DB 為唯一真實源，不維護全量記憶體狀態
  - 透過 `Channel` 與 `TaskCompletionSource` 收集併發請求
  - 讀取聚合：50ms 窗口內合併為單一 `WHERE GID IN (...)` 查詢
  - 寫入聚合：100ms 窗口內合併為單一交易，依俱樂部/玩家累加金額後以 `MySqlBatchCommand` 批次寫入
- **特性**：無記憶體與 DB 脫節風險，大幅降低資料庫往返與鎖競爭
- **壓測表現**：~3,198 RPS｜平均延遲 1,316 ms（效能約提升 20 倍）

### 3. `applicationcache`（記憶體快取 + 非同步後寫入）
- **模式**：In-Memory Cache & Write-Behind
- **核心機制**：
  - 啟動時預載全量俱樂部與玩家資料至 `ConcurrentDictionary`
  - 遊戲局、錢包扣款與加彩金全在記憶體以原子鎖完成
  - 注單推入 `ConcurrentQueue`，財務異動累計至 `SqlBank`
  - 背景 Worker 每 2.5 秒批次 Flush 至 MySQL
- **特性**：吞吐量極高、延遲最低；需承擔最終一致性與進程崩潰時未落盤資料遺失風險
- **壓測表現**：~7,298 RPS｜平均延遲 543 ms（效能約提升 47 倍）

### 4. `MD`（文檔分支）
- 本專案架構指南與分支評測說明。

---

## 📊 壓測數據對照表 (NBomber 1,000 玩家情境)

| 分支 | 架構模式 | RPS | 平均延遲 | P95 延遲 | 一致性 |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **`dbbasic`** | 資料庫直存 | 154/s | 2,778 ms | 3,817 ms | 強一致性 (ACID) |
| **`reqcoalescing`** | 請求合併 | 3,198/s | 1,316 ms | 1,908 ms | 強一致性 (DB 為準) |
| **`applicationcache`** | 記憶體快取 | 7,298/s | 543 ms | 754 ms | 最終一致性 (Write-Behind) |

---

## 📝 總結與架構建議

本專案透過三個實作分支展現了高併發遊戲與金融交易系統在**資料庫負載**與**一致性模型**上的典型演進路徑與實際取捨：

1. **`applicationcache` 的崩潰風險**：
   - 雖然在此架構下壓測數據高達 ~7,298 RPS，但其核心本質是將錢包與水池數據暫存於應用記憶體中。
   - **重大隱患**：若遭遇伺服器無預警崩潰（Crash）、記憶體溢出（OOM）或硬體斷電，尚未及時 Flush 到資料庫的玩家餘額與局記錄將永久遺失，造成嚴重的帳務災難。

2. **生產環境建議方案（推薦 `reqcoalescing` 或 `dbbasic`）**：
   - **全面使用資料庫交易（Transaction）落地**：這兩種做法在每次操作或微批次結算時，均即時透過 MySQL Transaction 提交入庫。即使伺服器行程瞬間崩潰，已提交的交易完好無損，未完成的交易由 DB 機制自動 Rollback，完全不影響帳務與資料一致性。
   - **效能與可靠性的平衡點 (`reqcoalescing`)**：若系統需要高吞吐量，強烈建議採用 `reqcoalescing` 分支——它既維持了「資料庫作為唯一真實源」並以 Transaction 嚴格落盤，又透過應用層微時間窗口（50~100ms）請求合併，化解了資料庫行鎖與連線競爭，達到 ~3,198 RPS（提升 20 倍）的卓越表現。
