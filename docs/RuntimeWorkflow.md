# Runtime Workflow

本文件只描述目前程式實際執行流程。設計背景與歷史決策見 `Draft/`，API 欄位以 OpenAPI 為準。

## 1. Docker 啟動流程

```mermaid
%%{init: {"flowchart": {"defaultRenderer": "elk", "nodeSpacing": 50, "rankSpacing": 75}}}%%
flowchart TD
    command["docker compose up --build"]
    config{".env 必要值完整？"}
    configError["Compose 插值失敗<br/>不建立服務"]
    sql["啟動 SQL Server 2022<br/>掛載 sqlserver-data"]
    healthy{"SQL Server Healthy？"}
    sqlError["停止依賴鏈<br/>檢查 SA 密碼與 Volume"]
    init["sqlserver-init<br/>建立／更新 myworkitem Login"]
    initOk{"Init Exit Code 0？"}
    initError["停止依賴鏈<br/>檢查 App Login 密碼"]
    migration["DatabaseMigrator<br/>DbUp Migration＋Development Seeder"]
    migrationOk{"Migrator Exit Code 0？"}
    migrationError["停止 API 啟動<br/>保留 Migration 錯誤"]
    api["啟動非 root API Container"]
    apiHealthy{"GET /health = 200？"}
    ready["API Ready<br/>http://localhost:5080"]

    command --> config
    config -->|"是"| sql
    config -->|"否"| configError
    sql --> healthy
    healthy -->|"是"| init
    healthy -->|"否"| sqlError
    init --> initOk
    initOk -->|"是"| migration
    initOk -->|"否"| initError
    migration --> migrationOk
    migrationOk -->|"是"| api
    migrationOk -->|"否"| migrationError
    api --> apiHealthy
    apiHealthy -->|"是"| ready
    apiHealthy -->|"否"| migrationError
```

`docker compose down` 會保留資料；加入 `--volumes` 會永久刪除 `sqlserver-data`，只能在明確確認不保留本機資料後使用。

## 2. IDE 啟動流程

```mermaid
%%{init: {"flowchart": {"defaultRenderer": "elk", "nodeSpacing": 45, "rankSpacing": 70}}}%%
flowchart TD
    infra["docker compose up -d<br/>sqlserver sqlserver-init migrator"]
    infraOk{"SQL Healthy 且<br/>Migrator Exited 0？"}
    secrets["載入 appsettings.json<br/>＋ Development＋User Secrets"]
    validConfig{"連線字串與 JWT Key有效？"}
    rider["Rider 啟動 MyWorkItem.Api:http"]
    ideReady["API Ready<br/>http://localhost:5170"]
    stop["停止啟動並顯示設定錯誤"]

    infra --> infraOk
    infraOk -->|"是"| secrets
    infraOk -->|"否"| stop
    secrets --> validConfig
    validConfig -->|"是"| rider --> ideReady
    validConfig -->|"否"| stop
```

IDE 不讀取 Compose `.env`；`ConnectionStrings:DefaultConnection` 與 `Jwt:SigningKey` 必須放在 User Secrets 或 Rider Environment Variables。

## 3. HTTP Request Pipeline

```mermaid
%%{init: {"flowchart": {"defaultRenderer": "elk", "nodeSpacing": 45, "rankSpacing": 70}}}%%
flowchart TD
    request["HTTP Request"] --> cors{"Origin 允許？"}
    cors -->|"否"| corsReject["瀏覽器阻擋 CORS"]
    cors -->|"是"| rate{"Auth Rate Limit<br/>未超過？"}
    rate -->|"否"| tooMany["429 ProblemDetails"]
    rate -->|"是"| auth["從 mwi_access Cookie<br/>驗證 JWT"]
    auth --> csrf{"Unsafe Method？"}
    csrf -->|"是"| csrfCheck{"CSRF Cookie 與 Header有效？"}
    csrfCheck -->|"否"| badCsrf["400 ProblemDetails"]
    csrfCheck -->|"是"| authorization
    csrf -->|"否"| authorization{"需要 Function？"}
    authorization -->|"缺少登入"| unauthorized["401"]
    authorization -->|"缺少 Function"| forbidden["403"]
    authorization -->|"通過或 AllowAnonymous"| controller["Controller → Service → SQL Server"]
    controller --> response["DTO 或 ProblemDetails"]
```

Function Authorization 會即時查詢 Account、Role 與 Function 聯集；帳號停用或權限異動於下一個 Request 生效。

## 4. Authentication Workflow

```mermaid
%%{init: {"flowchart": {"defaultRenderer": "elk", "nodeSpacing": 50, "rankSpacing": 75}}}%%
flowchart TD
    csrf["GET /api/v1/auth/csrf"] --> token["取得 mwi_antiforgery<br/>與 XSRF-TOKEN"]
    token --> login["POST /api/v1/auth/login<br/>＋ X-CSRF-TOKEN"]
    credentials{"帳號啟用且密碼正確？"}
    login --> credentials
    credentials -->|"否"| loginFail["401 ProblemDetails"]
    credentials -->|"是"| issue["建立 Access Token<br/>與 Refresh Token Family"]
    issue --> save[("只保存 Refresh Token Hash")]
    save --> cookies["設定 mwi_access／mwi_refresh<br/>HttpOnly Cookie"]
    cookies --> authCsrf["重新 GET /auth/csrf<br/>綁定登入身分"]
```

Refresh 每次輪替 Token；重播舊 Refresh Token 時撤銷整個 Family。Logout 撤銷目前 Family 並清除 Access／Refresh Cookie。

## 5. 後台管理 Workflow

```mermaid
%%{init: {"flowchart": {"defaultRenderer": "elk", "nodeSpacing": 50, "rankSpacing": 80}}}%%
flowchart TD
    login["Manager／Admin 登入"] --> permission{"具備 WorkItems.Manage？"}
    permission -->|"否"| forbidden["403 ProblemDetails"]
    permission -->|"是"| list["查詢 Work Item 管理清單"]
    list --> action{"操作類型？"}
    action -->|"新增"| create["建立內容<br/>AssignedUserId 可為 NULL"]
    action -->|"修改／重新指派"| update["提交 Base64 RowVersion"]
    action -->|"刪除"| delete["設定 DeletedAt／DeletedByUserId"]
    update --> version{"RowVersion 相符？"}
    version -->|"否"| conflict["409 Conflict<br/>重新載入最新資料"]
    version -->|"是"| transaction
    create --> transaction[("同一 Transaction<br/>WorkItems＋History after-snapshot")]
    delete --> transaction
    transaction --> result["回傳最新 Work Item<br/>或 204"]
```

Manager 另具備 `Users.Manage`；Admin 具備 `Roles.Manage` 與 `Functions.Manage`。Code 建立後不可修改，停用以 `IsEnabled` 管理，不提供硬刪除。

## 6. Worker Workflow

```mermaid
%%{init: {"flowchart": {"defaultRenderer": "elk", "nodeSpacing": 50, "rankSpacing": 80}}}%%
flowchart TD
    login["Worker 登入"] --> permission{"具備 Read 與 Confirm？"}
    permission -->|"否"| forbidden["403 ProblemDetails"]
    permission -->|"是"| query["GET /work-items<br/>取得全部有效項目與自己的狀態"]
    query --> choose{"操作類型？"}
    choose -->|"查看"| detail["GET /work-items/{id}"]
    choose -->|"單筆確認"| confirm["PUT /confirmation"]
    choose -->|"撤銷"| revoke["DELETE /confirmation"]
    choose -->|"批次"| batch["Checkbox 暫存於瀏覽器<br/>POST 最多 100 筆"]
    confirm --> state[("Upsert UserWorkItemStates")]
    revoke --> remove[("Delete UserWorkItemStates")]
    batch --> validate{"全部 WorkItem 有效？"}
    validate -->|"否"| rollback["整批 Rollback"]
    validate -->|"是"| state
    state --> reload["重新查詢顯示個人 Confirm"]
    remove --> reload
    detail --> reload
    reload --> revisit["重新登入後狀態仍保留"]
```

所有登入使用者可查看相同有效 Work Item。`AssignedUserId` 不影響可見性；確認者只能來自 JWT，Request 不接受其他人的 `UserId`。

## 7. 自動化驗收對照

| Workflow | 驗證範圍 |
| --- | --- |
| WF-01 | CSRF 與 ProblemDetails |
| WF-02 | CSRF → Login → Me |
| WF-03 | Refresh 輪替與重播撤銷 |
| WF-04 | 列表、詳情、指派篩選與個人狀態 |
| WF-05 | 不同使用者確認隔離、撤銷與持久化 |
| WF-06 | 批次確認原子性與 Rollback |
| WF-07 | CRUD、History、RowVersion 409、軟刪除 |
| WF-08 | Users／Roles／Functions 與權限立即生效 |
| WF-09 | Logout 撤銷 Family、清 Cookie、Me 失效 |
| WF-10 | Admin → Manager → Worker 完整旅程 |
| WF-11 | Swagger 等價 Cookie／CSRF 操作旅程 |
