# 第 9 章　匯出格式：iCalendar 與 CSV 的細節

> 本章目標：理解「看起來很簡單」的檔案格式中藏著的細節：時區、字元編碼、折行、跳脫，以及「格式正確」和「在使用者的軟體中正確」之間的差距。程式碼位於 `src/ShiftPlanner.Core/Export/`。

---

## 9.1 iCalendar（.ics）

### 需求

> 每位員工可匯出個人班表為 iCalendar（`.ics`），能正確匯入 Google 日曆。夜班的結束時間在隔天 07:00，時區為台北時間。

### 最小的有效檔案

```
BEGIN:VCALENDAR
VERSION:2.0
PRODID:-//ShiftPlanner//Shift Planner//ZH-TW
BEGIN:VTIMEZONE
TZID:Asia/Taipei
BEGIN:STANDARD
DTSTART:19700101T000000
TZOFFSETFROM:+0800
TZOFFSETTO:+0800
TZNAME:CST
END:STANDARD
END:VTIMEZONE
BEGIN:VEVENT
UID:7-20261114-night@shiftplanner
DTSTAMP:20261020T013000Z
DTSTART;TZID=Asia/Taipei:20261114T230000
DTEND;TZID=Asia/Taipei:20261115T070000
SUMMARY:夜班 23:00–07:00
END:VEVENT
END:VCALENDAR
```

### 時間的三種寫法

RFC 5545 允許三種時間表示方式，選錯了會在不同時區的使用者那裡出問題：

| 寫法 | 範例 | 意義 | 問題 |
|---|---|---|---|
| 浮動時間 | `DTSTART:20261114T230000` | 「當地的 23:00」，在哪個時區看就是哪個時區的 23:00 | 出國的員工會看到錯的時間 |
| UTC | `DTSTART:20261114T150000Z` | 絕對時間 | 檔案本身不直觀；換算錯誤時很難察覺 |
| **時區 + 當地時間** | `DTSTART;TZID=Asia/Taipei:20261114T230000` | 台北時間 23:00 | 需要附上 `VTIMEZONE` 定義 |

本專案用第三種：可讀性最好，語意也最精確。台灣沒有日光節約時間，所以 `VTIMEZONE` 只需要一個固定 +08:00 的 `STANDARD` 區段。

### 夜班跨日：不需要特殊處理

```csharp
var (start, end) = catalog.GetInterval(assignment);   // 第 2 章
// 夜班：start = 11/14 23:00，end = start + 8 小時 = 11/15 07:00
```

因為時間換算集中在 `ShiftCatalog.GetInterval`，ICS 匯出完全不需要知道「夜班會跨日」。

### `DTSTAMP` 與 `TimeProvider`

`DTSTAMP` 是「這個檔案產生的時間」，必須是 UTC。它來自注入的 `TimeProvider`，所以測試可以固定這個值，產生的檔案也就可以逐字比對。

### UID：讓重新匯入變成「更新」而不是「重複」

```
UID:{員工 Id}-{日期}-{班別}@shiftplanner
```

同一班重新匯出時 UID 相同，日曆軟體會**更新**既有事件，不會出現兩筆。

> **已知限制**：如果某人 11/14 從夜班改成早班，UID 也會跟著變，舊的夜班事件不會自動刪除。要完整處理，需要用「員工 + 日期」當 UID，或發送 `METHOD:CANCEL`。這是第 11 章的延伸練習。

### 文字跳脫

`SUMMARY`、`DESCRIPTION` 等文字欄位中，以下字元有特殊意義，必須跳脫：

```csharp
text.Replace("\\", "\\\\")    // 反斜線要最先處理，否則會重複跳脫
    .Replace(";", "\\;")
    .Replace(",", "\\,")
    .Replace("\r\n", "\\n")
    .Replace("\n", "\\n");
```

> **思考**：為什麼反斜線一定要最先替換？試試看把順序反過來，`a;b` 會變成什麼？

### 折行：以位元組計算，不能切斷中文字

RFC 5545 規定每行不超過 **75 個八位元組（octets）**，不是 75 個字元。超過的部分要換行，續行以一個空白開頭。

一個中文字在 UTF-8 中佔 3 個位元組。如果用字元數計算，一行 75 個中文字其實是 225 個位元組；如果直接用位元組切，可能把一個中文字切成兩半，變成亂碼。

```csharp
// src/ShiftPlanner.Core/Export/ICalendarWriter.cs
private static void Fold(StringBuilder builder, string line)
{
    var octets = 0;
    var limit = MaxLineOctets;                     // 75
    foreach (var rune in line.EnumerateRunes())    // 以 Unicode 字元（rune）為單位走訪
    {
        var size = rune.Utf8SequenceLength;        // 這個字元佔幾個位元組
        if (octets + size > limit)
        {
            builder.Append("\r\n ");               // 換行 + 一個空白
            octets = 0;
            limit = MaxLineOctets - 1;             // 續行開頭的空白也算一個 octet
        }

        builder.Append(rune.ToString());
        octets += size;
    }

    builder.Append("\r\n");
}
```

重點：

1. 用 `EnumerateRunes()` 而不是走訪 `char`：C# 的 `char` 是 UTF-16 單位，emoji 等字元需要兩個 `char`，用 `char` 走訪可能把它切成兩半。
2. 判斷「加上這個字會不會超過」，超過就**先換行再加**，所以不會切斷字元。
3. 換行符號是 `\r\n`（CRLF），不是 `\n`。

---

## 9.2 CSV

### 需求

> 月報：每人總工時、夜班數、週末班數、休假偏好滿足率，可匯出 CSV。用 Excel 開啟時中文不亂碼，數字與畫面上的報表一致。

### 細節一：BOM

Excel 開啟 CSV 時，如果檔案開頭沒有 **BOM**（Byte Order Mark，`EF BB BF`），會用系統預設編碼（繁體中文 Windows 是 Big5）解讀，UTF-8 的中文就變成亂碼。

```csharp
var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
return [.. encoding.GetPreamble(), .. encoding.GetBytes(builder.ToString())];
```

### 細節二：RFC 4180 的引號規則

欄位中有逗號、雙引號或換行時，要用雙引號包起來，裡面的雙引號寫成兩個：

```
姓名欄位：Lin, "Amy"
CSV 中：  "Lin, ""Amy"""
```

### 細節三：Excel 會「猜」資料型別（本專案踩過的坑）

第一版的 CSV 有兩欄長這樣：

```
強烈偏好(滿足/登記),一般偏好(滿足/登記)
2/2,1/2
```

所有自動化測試都通過：BOM 正確、欄位數正確、數字和畫面一致。但驗收測試時**用 Excel 實際開啟**，看到的是：

```
2026年2月2日 星期一    2026年1月2日 星期五
```

Excel 把 `2/2`、`1/2` **自動辨識成日期**。CSV 本身沒有型別資訊，Excel 會根據內容猜測；`數字/數字` 看起來像日期。

修正：把分數欄拆成純數字欄。

```
強烈偏好滿足數,強烈偏好登記數,一般偏好滿足數,一般偏好登記數
2,2,1,2
```

並加上一個測試，防止以後再出現這類欄位：

```csharp
// 除了姓名與合約類型，每個欄位都必須是純數字或空白
Assert.All(numericColumns, i => Assert.True(
    fields[i] == "" || decimal.TryParse(fields[i], NumberStyles.Number, CultureInfo.InvariantCulture, out _)));
```

> **教學重點**：「格式正確」不等於「在使用者的軟體中正確」。題目說的是「**用 Excel 開啟時**」，驗收就必須真的用 Excel 開啟。自動化測試檢查的是你**想到的**問題；在真實環境中操作，才會發現你**沒想到的**問題。

其他會被 Excel 猜錯的內容：

| 內容 | Excel 猜成 |
|---|---|
| `1/2`、`3-4` | 日期 |
| `00123` | 數字 123（前導零消失） |
| `1E5` | 科學記號 100000 |
| `=SUM(A1)` | 公式（甚至有安全疑慮，稱為 CSV injection） |

### 細節四：畫面和 CSV 共用同一個來源

「數字與畫面一致」最可靠的保證，是**兩邊用同一段程式碼產生內容**：

```csharp
// src/ShiftPlanner.Core/Export/MonthlyReportCsv.cs
/// <summary>一列的顯示內容，順序對應 Headers。畫面上的月報表格也用這個方法，確保兩邊一致。</summary>
public static IReadOnlyList<string> Cells(EmployeeReportRow row) => [ ... ];
```

```razor
@* src/ShiftPlanner.Web/Components/Pages/ReportPage.razor（簡化：實際程式另外帶出欄位索引來決定對齊方式） *@
@foreach (var cell in MonthlyReportCsv.Cells(row)) { <td>@cell</td> }
```

連數字格式（小數點、百分比）都由同一個 `ReportFormat` 決定。即使以後有人改了格式，兩邊也會一起改。

---

## 本章小結

1. ICS 用 `TZID` + 當地時間並附 `VTIMEZONE`；夜班跨日由時間換算自然處理。
2. 穩定的 UID 讓重新匯入變成更新；`DTSTAMP` 來自 `TimeProvider`，測試可以逐字比對。
3. 折行以 UTF-8 位元組計算、以 rune 為單位，不能切斷字元；換行用 CRLF。
4. CSV 要有 BOM；Excel 會猜型別，數值欄位要是純數字。
5. 「一致」最好的保證是共用來源；「在使用者軟體中正確」要用真實軟體驗證。

---

## 練習題

1. 如果 ICS 用浮動時間，一位在東京出差的員工匯入後，夜班會顯示幾點開始？
2. 把 `Escape` 的替換順序改成先替換 `;` 再替換 `\`，輸入 `a;b` 會得到什麼？為什麼是錯的？
3. 一行 `SUMMARY:` 加上 30 個中文字，折行後會變成幾行？每行幾個位元組？
4. ★ 修改 UID 為「員工 + 日期」，讓同一天換班別時能更新既有事件。需要修改哪些測試？會不會產生新的問題？（提示：同一天兩班的違規草稿。）
5. ★ 為 CSV 加上 CSV injection 防護：如果姓名以 `=`、`+`、`-`、`@` 開頭，在前面加上單引號。寫出測試。
