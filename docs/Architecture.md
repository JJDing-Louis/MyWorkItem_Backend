# 架構圖

## C4 Context

```mermaid
%%{init: {"flowchart": {"defaultRenderer": "elk", "nodeSpacing": 45, "rankSpacing": 70}}}%%
flowchart TD
    subgraph actors["使用者"]
        direction TD
        worker["Worker<br/>查看、確認 Work Item"]
        manager["Manager<br/>管理 Work Item 與使用者"]
        admin["Admin<br/>管理完整權限"]
    end

    frontend["MyWorkItem Frontend<br/><small>獨立前端專案</small>"]
    backend["MyWorkItem Backend<br/><small>.NET 10 Web API</small>"]
    database[("SQL Server<br/><small>業務資料、個人狀態、歷程與 Token Hash</small>")]

    worker -->|"HTTPS"| frontend
    manager -->|"HTTPS"| frontend
    admin -->|"HTTPS"| frontend
    frontend -->|"JSON API<br/>Cookie＋CSRF Header"| backend
    backend -->|"Dapper＋SqlKata / TDS"| database

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
    user["使用者"] -->|"使用"| frontend["Frontend<br/><small>Web App<br/>互動與 checkbox 暫存</small>"]

    subgraph backend["MyWorkItem Backend"]
        direction TD
        api["MyWorkItem.Api<br/><small>Controller、安全管線<br/>Swagger、ProblemDetails</small>"]
        application["Application<br/><small>DTO、Use Case 介面<br/>應用契約</small>"]
        infrastructure["Infrastructure<br/><small>Dapper、SqlKata、JWT<br/>Repository Service</small>"]
        domain["Domain<br/><small>Entity 與 Code Constants</small>"]
        migrator["DatabaseMigrator<br/><small>DbUp Migration＋Seeder</small>"]

        api -->|"執行 Use Case"| application
        application -->|"使用核心模型"| domain
        api -->|"DI 組合"| infrastructure
        infrastructure -.->|"實作 Application 介面"| application
    end

    sqlserver[("SQL Server 2022<br/><small>Schema V1.1</small>")]

    frontend -->|"HTTPS / REST JSON"| api
    infrastructure -->|"參數化 SQL"| sqlserver
    migrator -->|"Migration／Seeder"| sqlserver

    classDef actor fill:#FFF7ED,stroke:#EA580C,color:#7C2D12,stroke-width:1.5px;
    classDef boundary fill:#EFF6FF,stroke:#2563EB,color:#1E3A8A,stroke-width:1.5px;
    classDef component fill:#F8FAFC,stroke:#64748B,color:#0F172A,stroke-width:1.25px;
    classDef storage fill:#ECFDF5,stroke:#059669,color:#064E3B,stroke-width:1.5px;
    class user actor;
    class frontend boundary;
    class api,application,infrastructure,domain,migrator component;
    class sqlserver storage;
```
