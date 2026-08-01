# MyWorkItem Backend 架構

本文件使用 C4 的 Context 與 Container 視角說明系統邊界。圖表採通用 Mermaid
flowchart 表達，以提高 GitHub、IDE 與 Markdown 閱讀器的相容性。

## C4 Context

```mermaid
flowchart LR
    user["前台使用者<br/>查看 Work Item<br/>維護自己的確認狀態"]
    operator["後台使用者<br/>管理 Work Item"]
    admin["系統管理員<br/>管理使用者、角色與 Function"]

    frontend["MyWorkItem Frontend<br/>獨立前端系統"]
    backend["MyWorkItem Backend<br/>REST API、驗證、授權與資料持久化"]
    database[("Microsoft SQL Server<br/>帳號、權限、Work Item 與確認狀態")]

    user -->|HTTPS| frontend
    operator -->|HTTPS| frontend
    admin -->|HTTPS| frontend
    frontend -->|"JSON / HTTPS<br/>JWT Cookie + CSRF Header"| backend
    backend -->|"參數化 SQL"| database
```

### 系統責任

| 系統／人員 | 責任 |
| --- | --- |
| 前台使用者 | 查看全部有效 Work Item，確認或撤銷自己的確認狀態 |
| 後台使用者 | 新增、修改與軟刪除 Work Item |
| 系統管理員 | 維護使用者、角色、Function 與授權關係 |
| Frontend | 畫面、暫時 Checkbox 狀態、Credentials 與 CSRF Header 傳送 |
| Backend | API 契約、JWT Cookie、CSRF、權限驗證、交易與資料持久化 |
| SQL Server | 保存帳號、角色、權限、Refresh Token、Work Item 與個人確認紀錄 |

## C4 Container

```mermaid
flowchart TB
    browser["Browser / Frontend<br/>獨立專案"]

    subgraph backend["MyWorkItem Backend"]
        api["MyWorkItem.Api<br/>ASP.NET Core Web API<br/>Controllers、JWT Cookie、CSRF、Swagger、ProblemDetails"]
        application["MyWorkItem.Application<br/>Use Cases、DTO、驗證、Repository 介面、權限碼"]
        domain["MyWorkItem.Domain<br/>Account、User、Role、Function、WorkItem 核心模型"]
        infrastructure["MyWorkItem.Infrastructure<br/>Dapper、SqlKata、Repository、JWT、密碼雜湊"]
        migrator["MyWorkItem.DatabaseMigrator<br/>DbUp Migration、Development/Test Seed"]
    end

    sql[("SQL Server 2022<br/>MyWorkItem Database")]

    browser -->|"REST JSON<br/>Cookie + X-CSRF-TOKEN"| api
    api --> application
    api --> infrastructure
    application --> domain
    infrastructure --> application
    infrastructure --> domain
    infrastructure -->|"Microsoft.Data.SqlClient<br/>參數化查詢"| sql
    migrator -->|"DbUp SQL + Seed"| sql
```

## 執行與部署容器

```mermaid
flowchart LR
    compose["Docker Compose"]
    sql["sqlserver<br/>SQL Server 2022 Developer<br/>Port 1433"]
    migrator["migrator<br/>一次性執行<br/>成功後 Exit 0"]
    api["api<br/>ASP.NET Core Runtime<br/>Container Port 8080"]

    compose --> sql
    sql -->|Health Check 通過| migrator
    migrator -->|Migration 成功| api
```

依賴順序為 `sqlserver healthy` → `migrator completed successfully` → `api start`。
API 與 Migrator 都使用非 root 的 `$APP_UID` 執行。

## 驗證與授權流程

```mermaid
sequenceDiagram
    participant F as Frontend
    participant A as API
    participant D as SQL Server

    F->>A: GET /api/v1/auth/csrf
    A-->>F: XSRF-TOKEN Cookie
    F->>A: POST /api/v1/auth/login<br/>X-CSRF-TOKEN Header
    A->>D: 驗證 Account 與 PasswordHash
    D-->>A: Account、User、Role、Function
    A->>D: 保存 Refresh Token Hash
    A-->>F: mwi_access + mwi_refresh HttpOnly Cookie
    F->>A: 受保護 API + Cookie
    A->>A: 驗證 JWT 與 permission Claim
    A->>D: 執行授權後的資料操作
    D-->>A: 結果
    A-->>F: JSON / ProblemDetails
```

## 重要設計決策

- Work Item 對全部登入使用者可見，不建立指派資料表。
- Checkbox 暫選由前端管理；後端只保存已確認／待確認狀態。
- 個人狀態以 `(UserId, WorkItemId)` 複合主鍵隔離，使用者不能傳入其他 `UserId`。
- Work Item 使用軟刪除；一般清單與詳情排除 `DeletedAt` 不為空的資料。
- Work Item 更新使用 SQL Server `rowversion` 防止 Lost Update。
- Access Token 有效 15 分鐘；Refresh Token 有效 7 天並採輪替與 Token Family 撤銷。
- SqlKata 負責組合參數化查詢，Dapper 負責執行與映射，不使用 EF Core。
