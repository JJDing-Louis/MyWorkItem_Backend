# MyWorkItem Backend 開發指引

## 適用範圍

本文件適用於 `MyWorkItem_Backend` 目錄及其所有子目錄。

目前此目錄尚未建立後端專案。初始化專案時應以本文件為基準；實際的 .NET
版本、方案名稱與執行命令，則以建立後的 `.sln`、`.csproj` 與專案文件為準。

## 技術方向

- 使用 ASP.NET Core Web API 與 C#。
- 資料存取優先使用 Entity Framework Core。
- 身分驗證與使用者管理優先採用 ASP.NET Core Identity。
- API 採用 RESTful 設計，並以 JSON 作為主要交換格式。
- 啟用 Nullable Reference Types 與 Implicit Usings。
- 除非需求明確需要，否則不要加入大型框架或不必要的第三方套件。

## 開發原則

- 優先維持類別與方法的單一職責，使用清楚且具體的命名。
- 遵循分離關注點原則，避免 Controller 同時負責商業規則與資料存取。
- 依賴由 Dependency Injection 注入，不在應用程式碼中直接建立基礎設施物件。
- 優先使用 `async`／`await` 處理 I/O，並將 `CancellationToken` 傳遞至下游。
- 避免過度抽象；只有在職責明確或重複邏輯實際存在時才抽取介面與共用元件。
- 所有外部輸入都必須驗證，不可只依賴前端驗證。
- 錯誤回應應採一致格式，不直接回傳例外堆疊、資料庫資訊或敏感內容。
- 日誌應包含足以追蹤問題的上下文，但不得記錄密碼、Token 或個人敏感資料。
- 不得硬編碼密碼、連線字串、JWT 金鑰或其他 Secrets。
- 程式碼註解與專案文件使用繁體中文；型別與成員名稱遵循 C# 慣例。
- 修改應聚焦於需求範圍，避免變更無關檔案。

## 建議架構

專案規模尚小時，優先採用清楚的分層結構，不預先建立沒有實際用途的專案：

```text
src/
├── MyWorkItem.Api/             # HTTP 端點、Middleware 與應用程式進入點
├── MyWorkItem.Application/     # 使用案例、DTO、驗證與介面
├── MyWorkItem.Domain/          # 核心實體、值物件與商業規則
└── MyWorkItem.Infrastructure/  # EF Core、Identity 與外部服務實作
tests/
├── MyWorkItem.UnitTests/
└── MyWorkItem.IntegrationTests/
```

若功能與規模尚不足以支撐多專案方案，可先使用單一 Web API 專案，依
`Features`、`Domain` 與 `Infrastructure` 等目錄分離職責，再依實際需求拆分。

## API 設計

- Controller 或 Endpoint 僅處理 HTTP 邊界、授權、輸入轉換與結果映射。
- API 不直接公開 EF Core Entity，應使用明確的 Request／Response DTO。
- 使用適當的 HTTP Method 與狀態碼，例如新增回傳 `201 Created`、查無資料回傳
  `404 Not Found`、驗證失敗回傳 `400 Bad Request`。
- 錯誤格式優先採用 `ProblemDetails`，並為可預期錯誤提供穩定的錯誤代碼。
- 清單端點應考量分頁、排序與篩選，避免無限制載入資料。
- 對公開 API 的破壞性變更必須明確記錄，並評估版本化需求。

## 資料庫與 EF Core

- Entity 關聯、唯一鍵、索引與刪除行為必須明確設定，不依賴模糊的預設值。
- 多使用者資料必須依目前登入使用者進行授權與篩選，不可只相信用戶端傳入的
  `UserId`。
- 使用者對工作項目的狀態屬於使用者個別資料，不應直接存放成所有使用者共用的
  `WorkItem` 狀態；關聯資料應以 `(UserId, WorkItemId)` 維持唯一性。
- 查詢預設避免 N+1 問題；唯讀查詢視情況使用 `AsNoTracking()`。
- Migration 必須與模型修改一併提交，名稱需能清楚表達資料庫變更目的。
- 禁止在正式環境以刪除資料庫或重建資料庫作為一般部署流程。
- 涉及多筆一致性更新時，應評估交易與並行衝突處理。

## 安全性

- 每個需要保護的端點都必須明確套用 Authentication 與 Authorization。
- 物件層級授權必須在後端驗證，避免使用者讀取或修改他人的資料。
- 使用參數化查詢與 EF Core，禁止串接使用者輸入產生 SQL。
- 設定 CORS 時只允許必要來源、Method 與 Header，不使用無限制設定作為正式環境值。
- Secrets 應使用環境變數、User Secrets 或部署平台的安全設定管理。
- 套件加入前應確認用途、維護狀態與安全風險，並保持相依版本可追蹤。

## 測試與驗證

- Domain 與 Application 層的商業規則應以單元測試覆蓋。
- 授權、資料庫查詢、序列化與 HTTP 狀態碼應以整合測試驗證。
- 多使用者情境至少驗證：使用者 A 的操作不會改變或洩漏使用者 B 的狀態。
- 修正缺陷時，應在合理範圍內先加入可重現問題的測試。
- 測試應可重複執行，且不得依賴正式環境資料或真實 Secrets。

專案建立後，每次修改至少執行下列命令；若方案或專案命名不同，應依實際檔案調整：

```bash
dotnet restore
dotnet build --no-restore
dotnet test --no-build
dotnet format --verify-no-changes
```

若某項命令尚未設定或未能通過，必須清楚記錄實際執行結果與原因，不得將未驗證的
修改描述為已完成。

## Git 與變更管理

- 每次 Commit 維持單一目的，避免混入格式化或無關檔案。
- Commit Message 使用繁體中文，格式建議為 `type: 簡短說明`。
- 提交前檢查差異，避免加入建置產物、開發用 Secrets 或本機設定。
- 資料庫 Migration、API Contract 或設定變更應在變更說明中明確列出。
