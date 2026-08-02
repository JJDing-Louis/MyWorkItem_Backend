# MyWorkItem Backend Constitution

## 核心原則

### I. Schema 先行且可追溯

任何資料庫實作必須符合已核准的 Schema V1.1。Migration 只能新增、不得回改已套用檔案，
所有 PK、FK、UNIQUE、Index、交易與資料生命週期必須有測試證據。

### II. 分層與依賴反轉

API 只處理 HTTP 邊界；Application 定義 Use Case 與抽象；Domain 保存核心規則；
Infrastructure 實作 Dapper、SqlKata、SQL Server、Token 與密碼服務。不得循環依賴。

### III. 安全預設

JWT 與 Refresh Token 使用安全 Cookie；所有 unsafe request 驗證 CSRF；密碼與 Token 不得明碼保存、
寫入日誌或提交 Git。授權只相信伺服器驗證的身分與 Function。

### IV. 測試與完成證據

功能需有 Unit、Integration 或 Workflow Test。未實際通過 build、test、format、Docker、Migration、
Health Check 的項目不得宣稱完成。

### V. 最小且明確的變更

實作以可維護、可讀、可測試為優先，不引入 EF Core 或無需求的框架；不修改前端專案，
不自行 Commit、Push、建立 PR 或刪除資料庫 Volume。

## 技術約束

- .NET 10、ASP.NET Core Web API、Dapper、SqlKata、DbUp、SQL Server。
- NUnit、FluentAssertions、Bogus、NSubstitute、WebApplicationFactory、Testcontainers。
- 時間使用 UTC `datetimeoffset`；SQL 必須參數化；寫入交易需明確。
- 文件與註解使用繁體中文；公開 API 使用穩定英文名稱。

## 治理

本 Constitution 優先於臨時實作偏好。若需違反，必須在 feature plan 的 Complexity Tracking
記錄原因、替代方案與風險，並取得使用者確認。

**Version**: 1.0.0 | **Ratified**: 2026-08-03 | **Last Amended**: 2026-08-03
