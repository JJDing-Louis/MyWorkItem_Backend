# Research: MyWorkItem Backend MVP

## Data access

- **Decision**: SqlKata 只產生參數化 SQL，Dapper 執行與映射。
- **Rationale**: 符合既定技術方向並維持 SQL 可控性。
- **Alternatives considered**: EF Core；因使用者明確排除而不採用。

## Schema migration

- **Decision**: DbUp 嵌入依序編號 SQL，Migrator 獨立執行。
- **Rationale**: API 不應在啟動時隱式修改 Production Schema。
- **Alternatives considered**: API startup migration；部署失敗邊界不清楚。

## Authentication and CSRF

- **Decision**: JWT Access／Refresh Token 放入 Cookie，所有 unsafe request 使用 ASP.NET Core Antiforgery。
- **Rationale**: HttpOnly 降低 Token 被前端腳本讀取的風險；Cookie 型驗證必須防 CSRF。
- **Alternatives considered**: Local Storage Bearer Token；不符合核准安全模型。

## Authorization freshness

- **Decision**: JWT 保存身分，Function Authorization Handler 每次從資料庫計算目前啟用權限。
- **Rationale**: 帳號、角色或功能停用後下一個 Request 立即生效。
- **Alternatives considered**: 只相信 Token Claims；最長會延遲 15 分鐘。

## Personal confirmation

- **Decision**: `(UserId, WorkItemId)` 唯一狀態列；不存在為 Pending，存在時為 Confirm。
- **Rationale**: 避免為每位使用者與每筆 Work Item 預建 Pending 資料。
- **Alternatives considered**: Boolean 或預建 Pending；前者浪費已核准 Status Code，後者資料量與同步成本高。

## Audit history

- **Decision**: 成功 CRUD 後保存完整 after-snapshot，HistoryId 使用 bigint identity。
- **Rationale**: 可依序稽核且不需重建差異格式。
- **Alternatives considered**: before/after 雙快照、只保存 Action；前者過重，後者無法還原內容。

## Swagger interaction

- **Decision**: Swagger UI 同源攜帶 Cookie，載入時取得 CSRF，unsafe request interceptor 自動加入 Header。
- **Rationale**: 開發者不需複製 JWT 或 CSRF Token，流程與前端一致。
- **Alternatives considered**: Swagger Bearer Authorize；與 Cookie-only API 契約不一致。

## Docker sequencing

- **Decision**: SQL Server healthy → Migrator exit 0 → API start。
- **Rationale**: 避免 API 對未完成 Schema 提供流量。
- **Alternatives considered**: API 自行等待／Migration；責任與錯誤回報較差。
