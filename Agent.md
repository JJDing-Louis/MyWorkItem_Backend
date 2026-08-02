# MyWorkItem Backend 開發規範

## 適用範圍

本文件適用於 `MyWorkItem_Backend` 目錄及其所有子目錄。

目前 `dev2` 分支只有需求草圖與開發設定，尚未建立可建置的後端專案。建立方案、
原始碼、Migration 或測試前，必須先完成需求與 Schema 審查，不得把規劃描述成已完成
實作，也不得沿用其他分支未經確認的資料模型。

## 語言與文件

- 自然語言、README、設計文件、程式碼註解、docstring 與 Git Commit Message 使用
  繁體中文。
- 程式語言關鍵字、型別、API 名稱與技術專有名詞保留原文。
- 文件中的建置、測試與啟動結果必須來自實際執行；未執行或失敗時需明確說明。
- 架構與資料關係適合圖示時使用 Mermaid，並在提交前驗證可渲染性。

## 需求與 Schema 審查關卡

根目錄 `Schema.md` 是使用者提供的原始草圖，必須保留原文，不得直接覆寫、移動、
刪除或默默改成另一套資料模型。

任何資料庫實作前，必須先建立獨立的 Schema Review 文件，逐項整理：

1. 原始 Entity、Attribute 與 Relationship。
2. 草圖中缺少或矛盾的 PK、FK、UNIQUE、Nullability、型別與 Index。
3. 需求文件與 Schema 草圖之間的衝突。
4. 建議修正、替代方案、影響範圍與風險。
5. 哪些內容是原稿保留、明確需求調整、安全性新增或尚待使用者決定。

以下已知議題不得自行決定，需先提出並取得確認：

- `Account.UserID` 與 `User` 的一對一方向，以及角色應歸屬 Account 或 User。
- `UserFunction` 表名與其中 `RoleID` 欄位的語意衝突。
- `WorkItemID`、`CreateUserID`、`AsignUserID` 同時標為 PK 是否為草圖標示錯誤。
- `AsignUserID` 是否限制 Work Item 可見性，或只作為管理資訊。
- `WorkItem.Status` 是全域生命週期，還是每位使用者的個人確認狀態。
- `WorkItemStatus` 與 `WorkItem.Status` 的 FK 關係及狀態代碼設計。
- 個人「待確認／已確認」是否需獨立關聯表，以及其唯一鍵與稽核欄位。

未取得確認前，只能提出審查文件與方案，不得建立正式 Migration 或依假設修改 API。

## 預定技術方向

- 使用 .NET 10、ASP.NET Core Web API 與 C#。
- 使用 JWT 驗證；Access Token 與 Refresh Token 採安全 Cookie 傳遞。
- Cookie 型驗證的寫入請求必須有 CSRF 防護。
- 資料存取使用 Dapper、SqlKata 與 Microsoft.Data.SqlClient，不加入 EF Core。
- SqlKata 只負責組合參數化查詢，Dapper 負責執行與映射。
- 資料庫使用 SQL Server；Schema 版本控制使用 DbUp SQL Migration。
- API 文件使用 Swagger／OpenAPI，正式環境是否公開 UI 必須明確設定。
- 單元測試使用 NUnit、FluentAssertions、Bogus 與 NSubstitute。
- 整合測試使用 NUnit、WebApplicationFactory 與 SQL Server Testcontainers。
- 開發環境提供 Dockerfile、Docker Compose、SQL Server 與 Database Migrator。
- 啟用 Nullable Reference Types、Implicit Usings 與集中套件版本管理。

若套件或版本尚未實際建立與還原，文件只能標示為預定方向，不得宣稱已可執行。

## 架構與責任邊界

預定採清楚的分層架構：

```text
src/
├── MyWorkItem.Api/              # Controller、Middleware、JWT、CSRF、Swagger
├── MyWorkItem.Application/      # Use Case、DTO、驗證、介面與權限規則
├── MyWorkItem.Domain/           # Entity、Value Object 與核心商業規則
├── MyWorkItem.Infrastructure/   # Dapper、SqlKata、Repository、Token 與密碼服務
└── MyWorkItem.DatabaseMigrator/ # DbUp Migration 與環境種子資料
tests/
├── MyWorkItem.UnitTests/
└── MyWorkItem.IntegrationTests/
```

- 相依方向以 Domain 為核心；Application 不依賴 Infrastructure。
- API 只處理 HTTP 邊界、Authentication、Authorization、輸入轉換與結果映射。
- 商業規則不得放入 Controller 或 SQL 字串。
- Infrastructure 實作 Application 定義的抽象；高層模組不得直接依賴低層細節。
- 每個類別、介面、抽象類別、Record 與主要型別使用獨立檔案。
- 優先使用組合，不濫用繼承；只有存在實際變動點時才引入設計模式。
- 避免 God Object、過長方法、循環相依與沒有實際用途的抽象層。

## 程式碼設計

- 遵守 SOLID，優先考量可讀性、可維護性、可測試性與明確責任。
- 公開型別與成員使用 PascalCase；區域變數與參數使用 camelCase。
- I/O 使用 `async`／`await`，並向下傳遞 `CancellationToken`。
- 所有外部輸入必須驗證，包含空值、長度、格式、分頁邊界與識別碼。
- 禁止吞掉例外；預期錯誤轉換為一致的 ProblemDetails，未預期錯誤需留下安全日誌。
- 日誌不得包含密碼、JWT、Refresh Token、連線字串或個人敏感資料。
- 修改既有功能時採最小必要變更，並明確列出受影響 API、Schema 與測試。

## API 與安全性

- 使用 RESTful 路由與明確 Request／Response DTO，不直接公開資料庫模型。
- 適當使用 `200`、`201`、`204`、`400`、`401`、`403`、`404`、`409`。
- 錯誤回應使用 ProblemDetails，Production 不回傳 Stack Trace 或 SQL 細節。
- API 不接受前端傳入 `UserId` 決定目前操作身分；身分只能來自驗證後的 JWT。
- 每個受保護端點必須明確設定 Authentication 與 Function-based Authorization。
- Access Token、Refresh Token、JWT Signing Key 與密碼不得寫入版本控制。
- Refresh Token 只保存 Hash，輪替與 Token Family 撤銷需在交易中完成。
- CORS 僅允許設定中的前端 Origin；允許 Credentials 時不得使用萬用 Origin。
- 所有 `POST`、`PUT`、`PATCH`、`DELETE` 需驗證 CSRF Token。
- SQL 必須參數化，禁止拼接使用者輸入，並評估 SQL Injection 與 Race Condition。

## 資料庫設計

設計優先順序為：資料正確性、業務需求、至少符合 2NF、可理解性、可維護性、
開發複雜度、效能，再考慮進一步 3NF／BCNF。

Schema Review 至少檢查：

- 1NF、2NF、Partial Dependency 與單欄多值問題。
- 每張表的業務責任、PK、FK、UNIQUE、Nullability 與資料型別。
- 關聯表複合主鍵的非鍵欄位是否依賴完整主鍵。
- 主要查詢、排序、篩選與反向關聯所需 Index。
- 軟刪除、稽核欄位、資料生命週期與保留策略。
- Cascade Delete 是否可能誤刪業務資料；除非需求明確，預設不使用 Cascade Delete。
- 並行更新、Race Condition、Transaction Boundary 與批次操作的原子性。
- 重複資料是否會造成明顯一致性問題；不得只為追求 3NF 而過度拆表。

Migration 規則：

- SQL Migration 使用不可重複的遞增編號與清楚名稱。
- 已套用的 Migration 不得修改；變更 Schema 時新增下一支 Migration。
- Migration、最終 ERD、Schema Review 與 API 契約需一起進入版本控制。
- API 不負責自動改 Schema；由 Database Migrator 在部署流程中執行 DbUp。
- Production 不得以刪除或重建資料庫作為一般升級方式。

## 測試與驗證

- UnitTests 使用 NUnit、FluentAssertions、Bogus、NSubstitute。
- Bogus 集中建立 Account、User、Role、Function、WorkItem 與 Request DTO 測試資料。
- IntegrationTests 使用實際 SQL Server Testcontainers 驗證 Migration、Dapper 查詢與 HTTP。
- 至少涵蓋輸入驗證、權限聯集、JWT Claims、Refresh 輪替、CSRF 與 ProblemDetails。
- Schema 測試需驗證 Table、Column、PK、FK、UNIQUE、Index 與 Migration 可重跑。
- 多使用者情境需驗證使用者 A 的個人操作不會影響或洩漏使用者 B。
- 批次操作需驗證單一交易，不允許部分成功。
- 修正缺陷時，合理範圍內先加入可重現問題的測試。

專案建立後，每次變更至少執行：

```bash
dotnet restore
dotnet build --no-restore
dotnet test --no-build
dotnet format --verify-no-changes
docker compose config
```

涉及 Docker 或 Migration 時，還需實際驗證 SQL Server Health Check、Migrator Exit Code、
API Health Check 與必要端點。Docker 未啟動或驗證失敗時需記錄實際錯誤，不得描述為通過。

## Docker 與 Secrets

- Dockerfile 使用多階段建置，Runtime Container 採非 root 使用者。
- Compose 的 API 必須等待 Migrator 成功，Migrator 必須等待 SQL Server 健康。
- SQL Server 使用具名 Volume；刪除 Volume 前必須說明資料會不可恢復並取得確認。
- `.env`、User Secrets、SA 密碼、JWT Key 與 Production 管理員密碼不得提交。
- 只提供不含真實 Secret 的 `.env.example`。
- IDE 直接啟動 ASP.NET Core 不得假設會自動讀取 Docker Compose 的 `.env`。

## Git 與變更管理

- 修改前確認目前分支、Worktree 與工作樹狀態，避免跨分支誤寫。
- 不覆蓋、刪除或暫存不屬於目前任務的使用者變更。
- Commit 保持單一目的，格式使用 `type: 中文摘要`。
- Commit 前執行 `git diff --check`、敏感資料檢查與相關測試。
- 未經明確要求，不自行 Commit、Push、建立 Pull Request 或修改遠端狀態。
- 若需求與既有 Schema、API 或架構衝突，先列出差異、影響與替代方案，等待確認後再實作。
