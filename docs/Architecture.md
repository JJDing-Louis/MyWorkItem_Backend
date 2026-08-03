# 系統架構

本文件描述目前 `dev2` 的實際架構。前端位於獨立 Repository；本 Repository 只負責後端 API、資料庫版本控制、Migration 與自動化測試。

## C4 Context

```mermaid
%%{init: {"flowchart": {"defaultRenderer": "elk", "nodeSpacing": 55, "rankSpacing": 75}}}%%
flowchart TD
    worker["Worker<br/><small>查看、確認及撤銷 Work Item</small>"]
    manager["Manager<br/><small>管理 Work Item 與 Users</small>"]
    admin["Admin<br/><small>管理完整權限</small>"]
    frontend["MyWorkItem Frontend<br/><small>Vue 3 Web App／獨立 Repository</small>"]
    backend["MyWorkItem Backend<br/><small>.NET 10 ASP.NET Core Web API</small>"]
    database[("SQL Server 2022<br/><small>業務資料、個人狀態、歷程及 Token Hash</small>")]

    worker -->|"HTTPS"| frontend
    manager -->|"HTTPS"| frontend
    admin -->|"HTTPS"| frontend
    frontend -->|"REST JSON<br/>Cookie＋CSRF Header"| backend
    backend -->|"TDS／參數化 SQL"| database

    classDef person fill:#FFF7ED,stroke:#EA580C,color:#7C2D12,stroke-width:1.5px;
    classDef system fill:#EFF6FF,stroke:#2563EB,color:#1E3A8A,stroke-width:1.5px;
    classDef storage fill:#ECFDF5,stroke:#059669,color:#064E3B,stroke-width:1.5px;
    class worker,manager,admin person;
    class frontend,backend system;
    class database storage;
```

## C4 Container

```mermaid
%%{init: {"flowchart": {"defaultRenderer": "elk", "nodeSpacing": 55, "rankSpacing": 80}}}%%
flowchart TD
    browser["Browser<br/><small>Vue 3、Cookie、Checkbox 暫存</small>"]

    subgraph backend["MyWorkItem Backend"]
        direction TD
        api["MyWorkItem.Api<br/><small>Controller、JWT Cookie、CSRF、CORS<br/>Function Authorization、Swagger、ProblemDetails</small>"]
        application["MyWorkItem.Application<br/><small>Use Case 介面、DTO、驗證契約</small>"]
        infrastructure["MyWorkItem.Infrastructure<br/><small>Dapper、SqlKata、Service<br/>Password Hasher、JWT／Refresh Token</small>"]
        domain["MyWorkItem.Domain<br/><small>Entity 與 Role／Function／Status Code</small>"]
        migrator["MyWorkItem.DatabaseMigrator<br/><small>DbUp Migration＋Development Seeder</small>"]

        api -->|"呼叫 Use Case"| application
        api -->|"DI 組合"| infrastructure
        infrastructure -.->|"實作介面"| application
        application -->|"使用核心型別"| domain
        infrastructure -->|"使用核心型別"| domain
    end

    sqlserver[("SQL Server 2022<br/><small>12 張業務表＋DbUp Journal</small>")]
    browser -->|"HTTPS／REST JSON"| api
    infrastructure -->|"Dapper＋SqlKata"| sqlserver
    migrator -->|"DbUp＋Seeder"| sqlserver

    classDef boundary fill:#EFF6FF,stroke:#2563EB,color:#1E3A8A,stroke-width:1.5px;
    classDef component fill:#F8FAFC,stroke:#64748B,color:#0F172A,stroke-width:1.25px;
    classDef storage fill:#ECFDF5,stroke:#059669,color:#064E3B,stroke-width:1.5px;
    class browser boundary;
    class api,application,infrastructure,domain,migrator component;
    class sqlserver storage;
```

## API Component

```mermaid
%%{init: {"flowchart": {"defaultRenderer": "elk", "nodeSpacing": 45, "rankSpacing": 70}}}%%
flowchart TD
    request["HTTP Request"] --> pipeline["CORS → Rate Limit → JWT Cookie<br/>→ CSRF → Function Authorization"]

    subgraph controllers["MyWorkItem.Api Controllers"]
        direction LR
        auth["AuthenticationController<br/><small>CSRF、Login、Refresh、Logout、Me</small>"]
        workitems["WorkItemsController<br/><small>查詢、CRUD、指派、個人確認</small>"]
        users["UsersController<br/><small>帳號、個資、狀態、密碼、角色</small>"]
        roles["RolesController<br/><small>角色與 Function 配置</small>"]
        functions["FunctionsController<br/><small>Function 定義管理</small>"]
    end

    pipeline --> auth
    pipeline --> workitems
    pipeline --> users
    pipeline --> roles
    pipeline --> functions
    auth --> authService["AuthenticationService"]
    workitems --> workItemService["WorkItemService"]
    users --> userService["UserAdminService"]
    roles --> permissionService["PermissionAdminService"]
    functions --> permissionService
    authService --> db[("SQL Server")]
    workItemService --> db
    userService --> db
    permissionService --> db
```

## Docker Deployment

```mermaid
%%{init: {"flowchart": {"defaultRenderer": "elk", "nodeSpacing": 50, "rankSpacing": 75}}}%%
flowchart TD
    env[".env<br/><small>SA、App Login、JWT、Seed Secrets</small>"] --> sql["sqlserver<br/><small>SQL Server 2022／14333</small>"]
    sql -->|"Healthy"| init["sqlserver-init<br/><small>建立或更新 myworkitem Login</small>"]
    init -->|"Exit 0"| migrator["migrator<br/><small>DbUp＋Development Seeder</small>"]
    migrator -->|"Exit 0"| api["api<br/><small>非 root／Host 5080 → Container 8080</small>"]
    sql --> volume[("sqlserver-data<br/><small>具名 Volume</small>")]
```

### 責任與限制

- `sa` 僅供 SQL Server 初始化與 `sqlserver-init` 使用；API、Migrator 與 IDE 使用 `myworkitem` Login。
- `EnsureLocalLogin.sql` 目前為本機開發方便授予 `myworkitem` `sysadmin`；這不是 Production 最小權限設計。
- API Container 使用 .NET `app` 非 root 使用者；SQL Server Image 與 Apple Silicon `linux/amd64` 模擬僅供本機開發。
- Production 不應使用本機 Compose Secret、示範 Seed Account 或 `EnsureLocalLogin.sql`。
