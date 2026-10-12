# SlotMachineDEMO - 各分支架構與自動化測試指南

本專案改動自開源專案 https://github.com/slotopol/server

---

## 核心特色與架構決策

### 1. 遊戲邏輯與程式碼對應互動視覺化（快速上手引導）
- 提供兩款經典遊戲的**全流程互動視覺化展示**，專門用於快速上手遊戲玩法規則、核心演算法以及底層程式碼與資料庫狀態的對應關係：
  - **遊戲玩法與規則視覺化**：步進演示盤面滾輪、贏分線判定、掉落消除（Cascade / Avalanche）與階梯乘數機制。
  - **核心業務源碼深度剖析**：各步驟皆對照底層 C# 核心演算法、資料結構轉換與領域邏輯實現。
  - **資料庫狀態實時聯動**：直觀呈現下注扣款、派彩、水池彩金變動（ClubData）與注單明細寫入的對應關係。
- 點擊直接開啟線上互動展示：
  - [Gonzo's Quest Avalanche 連鎖掉落滿貫階梯視覺化 & Cascade 核心源碼剖析](https://htmlpreview.github.io/?https://github.com/DD898989/SlotMachineDEMO/blob/dbbasic/test/ServerMain.BDDTests/Features/GonzosQuestAvalanche.feature.html)
  - [MegaJack Slotopol 10 次指定下注模擬視覺化 (BDD Simulation)](https://htmlpreview.github.io/?https://github.com/DD898989/SlotMachineDEMO/blob/dbbasic/test/ServerMain.BDDTests/Features/SlotopolSimulation.feature.html)

### 2. BDD 行為驅動測試（嚴格比對資料庫底層資料）
- 採用 Gherkin 語法撰寫 BDD 測試，並整合自動化測試工具產出完整覆蓋率。
- 測試不僅驗證 HTTP API 回傳，更於每一步驟直接連線 MySQL，精確比對資料庫底層資料（包含 Users 錢包餘額、ClubData 水池與彩金池、Spinlogs 注單明細、Multlogs 比倍紀錄），確保運算邏輯與資料庫實體狀態分毫不差。

### 3. 為什麼不使用 Redis？
- 老虎機核心業務在每次 Spin 下注時為標準的「1 讀 1 寫」（讀取玩家該局狀態與錢包 -> 運算 -> 寫回扣款與派彩結果）。
- 缺乏傳統快取「讀多寫少」的複用場景，引入 Redis 僅會增加額外網路跳轉（Hop）與分散式雙寫一致性問題，無實質效益。

---

## 各分支介紹

### 1. `dbbasic`（傳統資料庫直存）
- 模式：Direct Database Access（標準 CRUD）
- 核心機制：
  - 每個 HTTP 請求獨立建立 EF Core DbContext
  - 即時查詢 Users、Stories、ClubData、Props
  - 運算後直接以 ExecuteUpdate 與 SaveChanges 更新資料庫
- 特性：標準 ACID 強一致性；瓶頸在於資料庫連線池與行鎖競爭
- 壓測表現：~154 RPS｜平均延遲 2,778 ms

### 2. `reqcoalescing`（請求合併與批次處理）
- 模式：Request Coalescing（微批次事務聚合）
- 核心機制：
  - 以 DB 為唯一真實源，不維護全量記憶體狀態
  - 透過 Channel 與 TaskCompletionSource 收集併發請求
  - 讀取聚合：50ms 窗口內合併為單一 WHERE GID IN (...) 查詢
  - 寫入聚合：100ms 窗口內合併為單一交易，依俱樂部/玩家累加金額後以 MySqlBatchCommand 批次寫入
- 特性：無記憶體與 DB 脫節風險，大幅降低資料庫往返與鎖競爭
- 壓測表現：~3,198 RPS｜平均延遲 1,316 ms（效能約提升 20 倍）

### 3. `applicationcache`（記憶體快取 + 非同步後寫入）
- 模式：In-Memory Cache & Write-Behind
- 核心機制：
  - 啟動時預載全量俱樂部與玩家資料至 ConcurrentDictionary
  - 遊戲局、錢包扣款與加彩金全在記憶體以原子鎖完成
  - 注單推入 ConcurrentQueue，財務異動累計至 SqlBank
  - 背景 Worker 每 2.5 秒批次 Flush 至 MySQL
- 特性：吞吐量極高、延遲最低；需承擔最終一致性與進程崩潰時未落盤資料遺失風險
- 壓測表現：~7,298 RPS｜平均延遲 543 ms（效能約提升 47 倍）

### 4. `MD`（文檔分支）
- 本專案架構指南、分支評測與測試說明。

---

## 壓測數據對照表 (NBomber 1,000 玩家情境)

| 分支 | 架構模式 | RPS | 平均延遲 | P95 延遲 | 一致性 |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **`dbbasic`** | 資料庫直存 | 154/s | 2,778 ms | 3,817 ms | 強一致性 |
| **`reqcoalescing`** | 請求合併 | 3,198/s | 1,316 ms | 1,908 ms | 強一致性 |
| **`applicationcache`** | 記憶體快取 | 7,298/s | 543 ms | 754 ms | 最終一致性 (Write-Behind) |

---

## 自動化測試腳本一覽 (test/*.bat)

| 腳本名稱 | 測試類型 | 核心機制與執行流程 | Cheat 參數注入 |
| :--- | :--- | :--- | :--- |
| **`test/run_bdd_coverage.bat`** | BDD 功能覆蓋率 | 編譯 Debug 伺服器 -> 執行 BDD 全量測試 -> 透過 `dotnet-coverage` 跨進程收集伺服器端覆蓋率 -> `ReportGenerator` 產出 HTML 報告並自動開啟瀏覽器。 | **使用**（指定盤面與停輪，確定性驗證特定贏分線、彩金與 Cascade 分支） |
| **`test/run_load.bat`** | 單機真實併發壓測 | 自動隨機建立暫存資料庫 -> 背景啟動伺服器 (:18080) -> curl 探針確認就緒 -> 模擬 1,000 玩家隨機下注壓測 -> 自動殺行程並刪除暫存庫。 | 無（隨機玩家行為） |
| **`test/run_load_fixed.bat`** | 單機固定基準壓測 | 建立隨機隔離資料庫 -> 啟動單機伺服器 -> 依固定 Cheat 序列執行固定次數/時長下注 -> 比對理論預期金額與資料庫實際餘額 -> 清理刪庫。 | **使用**（固定 10 步停輪模式，用以驗證單機高併發下帳務數值絕對精準） |
| **`test/run_load_fixed_lb.bat`** | 雙實例負載均衡壓測 | 建立隔離資料庫 -> 同步背景啟動 2 台伺服器 (:18080 / :18081) -> curl 探測雙機就緒 -> 對雙節點分流壓測 -> 比對雙機競爭下的最終資料庫數值 -> 清理刪庫。 | **使用**（固定 10 步停輪模式，用以驗證多節點並發鎖競爭下的帳務絕對精準） |


---

## 總結與架構建議

本專案透過三個實作分支展現了高併發遊戲與金融交易系統在資料庫負載與一致性模型上的典型演進路徑與實際取捨：

1. `applicationcache` 的崩潰風險：
   雖然在此架構下壓測數據高達 ~7,298 RPS，但其核心本質是將錢包與水池數據暫存於應用記憶體中。若遭遇伺服器無預警崩潰（Crash）、記憶體溢出（OOM）或硬體斷電，尚未及時 Flush 到資料庫的玩家餘額與局記錄將永久遺失，造成嚴重的帳務災難。

2. 生產環境建議方案（`reqcoalescing`）：
   若系統需要兼顧高吞吐量與強一致性，強烈建議採用 `reqcoalescing` 分支——它維持了資料庫作為唯一真實源並以 Transaction 嚴格落盤，同時透過應用層微時間窗口（50~100ms）請求合併，消除資料庫行鎖與連線競爭，達到 ~3,198 RPS（較 dbbasic 提升約 20 倍）的卓越表現。
