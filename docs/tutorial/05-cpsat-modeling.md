# 第 5 章　核心演算法一：用 CP-SAT 建模排班問題

> 本章目標：理解**限制規劃**（Constraint Programming）的思考方式，學會把排班規則寫成 0/1 變數上的線性限制，把「盡量公平」這類目標線性化，並理解加權目標函數的設計。程式碼位於 `src/ShiftPlanner.Scheduling/ScheduleModel.cs`。

---

## 5.1 為什麼不用貪婪演算法或自己寫搜尋？

排班是典型的**組合最佳化**問題。16 人 × 30 天 × 3 班，每格「排或不排」，可能的組合數是 2^1440，暴力列舉不可能。

常見的作法有三種：

| 方法 | 優點 | 缺點 |
|---|---|---|
| 貪婪演算法（一天一天、一格一格填） | 簡單、快 | 前面的選擇會卡死後面；找不到解時，不知道是真的無解還是演算法太笨 |
| 區域搜尋（模擬退火、禁忌搜尋） | 彈性大 | 要自己調參數；**無法證明無解** |
| **限制規劃求解器（CP-SAT）** | 只要描述「什麼是合法的」，求解器負責搜尋；**可以證明無解** | 要學建模；結果可能非決定性 |

題目要求「找不到可行解時要回報具體原因」，這需要能**分辨「真的無解」和「沒找到」**，所以本專案選擇 CP-SAT（Google OR-Tools 中的求解器）。

### CP-SAT 的思考方式

你不寫「怎麼排」，只寫「**什麼樣的班表是合法的**」和「**什麼樣的班表比較好**」：

```
變數：   x[e,d,s] ∈ {0, 1}         某人某天是否上某班
限制：   一堆關於 x 的線性不等式    （硬性規則）
目標：   最小化 Σ 權重 × 違反程度   （軟性偏好）
```

求解器結合了 SAT（布林可滿足性）、線性規劃鬆弛、限制傳播等技術，在這個空間中搜尋最佳解，或證明不存在可行解。

---

## 5.2 決策變數：只建立「可能為真」的變數

```csharp
// src/ShiftPlanner.Scheduling/ScheduleModel.cs
private void CreateVariables()
{
    var leaves = _request.Leaves.ToLookup(l => l.EmployeeId);
    foreach (var employee in _request.Employees)
    {
        foreach (var date in Horizon.Days())
        {
            if (leaves[employee.Id].Any(l => l.Contains(date)))
            {
                continue;                                    // 請假日：不建立變數
            }

            foreach (var shift in DisplayNames.AllShifts.Where(employee.CanWork))   // 不可上的班別：不建立
            {
                var x = Model.NewBoolVar($"x_{employee.Id}_{date:MMdd}_{shift}");
                _x[(employee.Id, date, shift)] = x;
                ...
            }
        }
    }
}
```

**建模技巧：用「不建立變數」取代「加限制式」。**

H5（可上班別）和 H7（請假日）如果寫成限制，就是 `x[e,d,s] = 0`。直接不建立這些變數，效果相同，而且模型更小。其他地方查不到變數時，就當作常數 0：

```csharp
private LinearExpr ShiftOn(int employeeId, DateOnly date, ShiftType shift)
{
    if (date < Horizon.Start)                                    // 前月：用常數
        return LinearExpr.Constant(_context.Contains((employeeId, date, shift)) ? 1 : 0);
    if (date > Horizon.End)                                      // 下月：用常數
        return LinearExpr.Constant(_following.Contains((employeeId, date, shift)) ? 1 : 0);
    return _x.TryGetValue((employeeId, date, shift), out var x) ? x : LinearExpr.Constant(0);
}
```

`ShiftOn` 是整個模型最重要的輔助函式。它讓「本月的變數」「前月的常數」「下月的常數」「不存在的組合」都能用**同一種方式**參與運算，限制式不需要特別處理跨月。

另一個輔助函式 `WorkOn(e, d)` = Σ_s `ShiftOn(e, d, s)`，表示「某人某天有沒有上班」（0 或 1）。

---

## 5.3 硬性限制的線性化

### H6 每天最多一班

$$\sum_{s} x_{e,d,s} \le 1 \qquad \forall e, d$$

```csharp
Model.AddAtMostOne(group.Select(kv => (ILiteral)kv.Value));
```

`AddAtMostOne` 是 CP-SAT 的專用限制，比一般線性不等式傳播得更有效率。

### H1 休息 11 小時：先算出衝突組合，再「兩者不可兼得」

第一步，**由班別時間推導**哪些組合休息不足（不是寫死）：

```csharp
// src/ShiftPlanner.Scheduling/RestConflicts.cs
for (var offset = 1; offset <= 2; offset++)
    foreach (var prev in AllShifts)
        foreach (var next in AllShifts)
        {
            var prevEnd   = catalog.GetInterval(new Assignment(0, reference, prev)).End;
            var nextStart = catalog.GetInterval(new Assignment(0, reference.AddDays(offset), next)).Start;
            if (nextStart - prevEnd < RestPeriodRule.DefaultMinimumRest)
                conflicts.Add((offset, prev, next));
        }
```

標準班別算出來是三組：(1, 晚, 早)、(1, 夜, 早)、(1, 夜, 晚)。

第二步，對每一組衝突加上「不能同時為 1」：

$$x_{e,d,s_1} + x_{e,d+k,s_2} \le 1$$

```csharp
Model.AddBoolOr([prev.Not(), next.Not()]);    // ¬prev ∨ ¬next，等價於 prev + next ≤ 1
```

如果前一班落在**前月上下文**（常數 1），限制就退化成 `next = 0`；如果後一班落在**已發布的下月**，就是 `prev = 0`：

```csharp
if (prevDate < Horizon.Start)
{
    if (_context.Contains((employee.Id, prevDate, prevShift)))
        Model.AddLinearConstraint(next, 0, 0);       // 10/31 上夜班 → 11/1 不能上早班
}
```

### H2 每週休息日、H4 每週工時

對每一週（含跨月的週）、每位員工：

$$\sum_{d \in 已知天數} \text{WorkOn}(e,d) \le |已知天數| - 需休天數$$

$$\sum_{d,s} \text{ShiftOn}(e,d,s) \times \text{分鐘數}(s) \le 上限 \times 60$$

工時用**分鐘**而不是小時，因為 CP-SAT 只接受**整數**係數。如果班別長度是 7.5 小時，用小時就無法表示。

### H3 連續上班：滑動視窗

「連續上班不超過 6 天」⇔「**任意連續 7 天中至少休 1 天**」：

$$\sum_{i=0}^{6} \text{WorkOn}(e, d+i) \le 6 \qquad \forall e, \forall d$$

```csharp
for (var start = Horizon.Start.AddDays(1 - window);            // 從前月延伸進來的視窗
     start <= Horizon.End && start.AddDays(window - 1) <= KnownUntil;   // 延伸到下月已知的範圍
     start = start.AddDays(1))
```

> **建模技巧**：「連續 k 天」這類規則通常不直接建模「連續段」，而是轉換成「**每個長度 k+1 的視窗中至少有一個 0**」，這樣就是簡單的線性不等式。

### 人力與技能需求

$$\sum_{e} x_{e,d,s} \ge 需求(d,s)$$

$$\sum_{e \text{ 會英文}} x_{e,d,s} \ge 1$$

---

## 5.4 軟性目標：加權總和

所有軟性偏好放在一個**最小化**的目標函數中：

| 項目 | 權重 | 意義 |
|---|---:|---|
| 強烈休假偏好被排班 | 1000 | 每違反一筆 |
| 臨時重排：與原班表不同的格子 | 500 | 每一格（只在重排時） |
| 一般休假偏好被排班 | 100 | 每違反一筆 |
| 全職週末班數差距（max − min） | 50 | |
| 月底把休假推給下個月 | 30 | 每推一天（第 8 章） |
| 全職夜班數差距（max − min） | 20 | |
| 每排一班 | 1 | 需求是「最低」人數，避免無謂地多排 |

### 權重的設計原則：近似「字典序」

這些權重故意設成**數量級的差距**。實務上的效果接近「先最小化強烈偏好違反，打平時再看一般偏好，再看公平性……」。

例如：違反一筆強烈偏好（1000）的代價，大於把週末差距從 10 降到 0 的好處（50 × 10 = 500）。所以求解器永遠不會為了公平而犧牲某人的強烈休假偏好。

> **注意**：加權總和不是嚴格的字典序最佳化。如果需要嚴格保證，可以分階段求解：先求最佳的強烈偏好違反數，把它固定成限制，再最佳化下一層。代價是要求解多次。

### 偏好的寫法

```csharp
_objective.AddTerm(WorkOn(preference.EmployeeId, preference.Date), weight);
```

因為 H6 保證每天最多一班，`WorkOn` 只會是 0 或 1，直接乘上權重就是違反成本。

---

## 5.5 公平性：把 max − min 線性化

「夜班盡量平均」最直接的指標是「最多的人 − 最少的人」。但 max 和 min 不是線性函數，不能直接寫進線性目標。

CP-SAT 提供 `AddMaxEquality` / `AddMinEquality`，引入輔助變數：

```csharp
// src/ShiftPlanner.Scheduling/ScheduleModel.cs
private IntVar? Spread(IReadOnlyList<LinearExpr> counts, string name)
{
    var max = Model.NewIntVar(0, upper, $"{name}_max");
    var min = Model.NewIntVar(0, upper, $"{name}_min");
    var spread = Model.NewIntVar(0, upper, $"{name}_spread");
    Model.AddMaxEquality(max, counts);          // max = max(每人夜班數)
    Model.AddMinEquality(min, counts);          // min = min(每人夜班數)
    Model.AddLinearConstraint(spread + min − max == 0);   // spread = max − min
    return spread;
}
```

接著：

- **軟性**：`objective += 20 × spread`
- **硬性**（驗收 #11）：`spread ≤ 2`

```csharp
if (_request.MaxNightSpread is int max)
{
    Model.AddLinearConstraint(nightSpread, 0, max);
}
```

> **思考**：為什麼用 max − min 而不是標準差？標準差有平方和開根號，是非線性的。max − min 可以用上面的技巧精確線性化，而且對使用者更直觀（「最多的人比最少的人多幾班」）。報表上仍然顯示標準差，因為那是題目要求的指標。

---

## 5.6 一個可以手算的小例子

2 位員工（A、B），2 天（第 1、2 天），只有晚班和早班，每班每天需要 1 人。

變數：`A1晚、A1早、A2晚、A2早、B1晚、B1早、B2晚、B2早`（8 個 0/1 變數）

限制：

```
每天一班：  A1晚 + A1早 ≤ 1，A2晚 + A2早 ≤ 1，B1晚 + B1早 ≤ 1，B2晚 + B2早 ≤ 1
人力需求：  A1晚 + B1晚 ≥ 1，A1早 + B1早 ≥ 1，A2晚 + B2晚 ≥ 1，A2早 + B2早 ≥ 1
休息規則：  A1晚 + A2早 ≤ 1，B1晚 + B2早 ≤ 1      （晚接隔天早只休 8 小時）
```

推理：第 1 天兩人都要上（一晚一早）。假設 A 上第 1 天晚班，那 A 第 2 天不能上早班，只能上晚班；B 第 1 天上早班，第 2 天就要上早班。解：

| | 第 1 天 | 第 2 天 |
|---|---|---|
| A | 晚 | 晚 |
| B | 早 | 早 |

另一個對稱解是 A、B 互換。若再加上「A 第 2 天強烈希望休假」，兩個解都需要 A 第 2 天上班……求解器會發現**只有違反偏好才可行**，並回報最小代價的解。

> **練習**：試著把這個例子寫成 CP-SAT 程式，觀察求解器的輸出。

---

## 5.7 求解與結果

```csharp
// src/ShiftPlanner.Scheduling/ShiftScheduleGenerator.cs
StringParameters = $"max_time_in_seconds:{request.TimeLimit.TotalSeconds} num_workers:8 random_seed:42"
```

| 參數 | 意義 |
|---|---|
| `max_time_in_seconds` | 時間上限，預設 30 秒；超過就回傳目前找到的最好解 |
| `num_workers: 8` | 8 個平行搜尋執行緒，各用不同的策略 |
| `random_seed` | 隨機種子 |

回傳狀態對應：

| CP-SAT 狀態 | 意義 | 本系統處理 |
|---|---|---|
| `OPTIMAL` | 找到並**證明**是最佳解 | 成功 |
| `FEASIBLE` | 找到可行解，但時間到了還沒證明最佳 | 成功（畫面標示「可行解」） |
| `INFEASIBLE` | **證明**不存在可行解 | 進入無解診斷（第 6 章） |
| `UNKNOWN` | 時間到了，既沒找到解也沒證明無解 | 回報逾時 |

範例資料的規模：約 1,440 個布林變數，11 月約 **0.7 秒**求得最佳解（`OPTIMAL`）。

### 非決定性：同一份輸入，可能得到不同的解

8 個 worker 平行搜尋，誰先找到好解取決於作業系統的執行緒排程。所以**同一份輸入，每次可能得到不同的最佳解**（目標值相同，但班次不同）。

這對測試有重要影響（第 10 章）：

- ✅ 測試應該檢查**性質**：零違規、夜班差距 ≤ 2、強烈偏好滿足率 ≥ 90%
- ❌ 不能假設「某人某天一定是某班」

若需要可重現的結果，可以設定 `num_workers:1`，代價是求解較慢。

---

## 5.8 模型規模總覽

以 11 月範例資料估算：

| 元件 | 數量（約） |
|---|---:|
| 布林變數 x | 1,400（16 人 × 30 天 × 可上的班別數，扣掉請假） |
| H6 每天一班 | 480 |
| H1 休息衝突 | 1,400 |
| H2 + H4 每週 | 16 人 × 6 週 × 2 = 192 |
| H3 滑動視窗 | 16 人 × 30 個視窗（起點 10/26～11/24）= 480 |
| 人力 + 技能需求 | 90 + 132（英文 90、平日早晚班日文 42） |
| 公平性輔助變數 | 6 |

這是一個「小」模型。CP-SAT 能處理數萬到數十萬個變數的問題，但時間會快速增長。如果規模擴大到數百人，可能要拆分問題（例如依部門分開排）或調整求解策略。

---

## 本章小結

1. 限制規劃只描述「什麼是合法、什麼是好的」，搜尋交給求解器；還能證明無解。
2. 用「不建立變數」取代 `x = 0` 的限制；用 `ShiftOn` 統一處理變數、上下文常數與不存在的組合。
3. 衝突組合由班別時間推導；「連續 k 天」轉成滑動視窗；係數一律用整數。
4. 加權目標用數量級差距近似字典序；max − min 用輔助變數線性化。
5. 多 worker 平行搜尋是非決定性的，測試要檢查性質而不是具體結果。

---

## 練習題

1. 為什麼 `RestConflicts` 只檢查 `offset` 1 到 2 天？如果班別長度改成 12 小時，需要檢查到幾天？
2. 寫出「每週工時 ≤ 40 小時」用**小時**當係數會出什麼問題的例子（提示：7.5 小時的班別）。
3. 如果把強烈偏好權重從 1000 降到 40，會發生什麼事？構造一個情境，讓求解器「為了公平而違反某人的強烈偏好」。
4. 把 5.6 節的小例子用 OR-Tools 寫出來（C# 或 Python 都可以），並加入「A 第 2 天強烈希望休假」，觀察目標值。
5. ★ 實作 5.4 節提到的「分階段字典序最佳化」：先最小化強烈偏好違反數，把結果固定成限制，再最佳化其他目標。比較兩種做法在範例資料上的結果與耗時。
6. ★ 目前「每排一班」成本 1，導致產生器剛好排到最低人數，全職員工不一定排滿 40 小時。設計一個目標項，讓全職員工的工時盡量接近 40 小時，並說明它和現有權重的關係。
