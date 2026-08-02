# MyWorkItem Backend Runtime Workflow（Draft）

## 文件目的

本文件只描述系統實作完成後，程式在執行期間的實際流程，包括 HTTP Request Pipeline、
登入與 Token、Work Item 查詢、個人確認、管理端 CRUD，以及使用者與權限管理。

- 本文件不包含開發階段、Schema 審查、建置、測試或 Docker 啟動流程。
- Login View 與 Work Item Views 位於獨立前端專案；本 Repository 實作後端 API。
- `AuthenticationController` 獨立處理登入工作階段，不與 `UserController` 混用。
- 本文件仍是 Draft；資料表與狀態代碼需以最後核准的 Schema 為準。

## 1. 系統整體 Runtime Workflow

```mermaid
flowchart LR
    User([前台使用者]) --> Login[Login View]
    User --> WorkItemViews[Work Item List／Detail]
    Admin([後台使用者]) --> Login
    Admin --> AdminViews[Admin／User Management Views]

    Login -->|CSRF、Login、Refresh、Logout、Me| AuthController[AuthenticationController]
    WorkItemViews -->|查詢、確認、撤銷| WorkItemController[WorkItemController]
    AdminViews -->|Work Item CRUD| WorkItemController
    AdminViews -->|帳號與權限管理| UserController[UserController]

    AuthController --> AuthUseCase[Authentication Use Cases]
    WorkItemController --> WorkItemUseCase[Work Item Use Cases]
    UserController --> UserUseCase[User／Role／Function Use Cases]

    AuthUseCase --> Repositories[Repository／Token／Password Services]
    WorkItemUseCase --> Repositories
    UserUseCase --> Repositories
    Repositories -->|SqlKata + Dapper| DB[(SQL Server)]
```

## 2. HTTP Request Pipeline

每個 API Request 都先通過共用 Pipeline；Controller 只負責 HTTP 邊界，不直接執行 SQL
或放置商業規則。

```mermaid
flowchart TD
    Request[前端送出 HTTPS Request] --> Cors{Origin 是否在 CORS Allowlist？}
    Cors -->|否| CorsRejected[拒絕跨來源存取]
    Cors -->|是| Write{是否為 POST、PUT、PATCH、DELETE？}

    Write -->|是| Csrf{CSRF Cookie 與 Header 是否有效？}
    Csrf -->|否| BadCsrf[400 ProblemDetails]
    Csrf -->|是| Auth
    Write -->|否| Auth{端點是否需要登入？}

    Auth -->|需要，但 JWT 無效或逾期| Unauthorized[401 ProblemDetails]
    Auth -->|不需要或 JWT 有效| Permission{端點是否要求 Function？}
    Permission -->|需要，但使用者沒有權限| Forbidden[403 ProblemDetails]
    Permission -->|不需要或具有權限| Validate{Request DTO 是否有效？}
    Validate -->|否| BadRequest[400 ProblemDetails]
    Validate -->|是| Controller[Controller]
    Controller --> UseCase[Application Use Case]
    UseCase --> Domain[Domain Rule／Transaction Boundary]
    Domain --> Repository[Infrastructure Repository]
    Repository -->|參數化查詢| Database[(SQL Server)]
    Database --> Repository
    Repository --> UseCase
    UseCase --> ResponseMap[映射 Response DTO]
    ResponseMap --> Success[200／201／204 Response]
```

未預期例外由全域 Exception Handler 記錄安全日誌並回傳 `500 ProblemDetails`；Response 不得
包含 Stack Trace、SQL、Cookie、Token 或其他敏感資料。

## 3. 登入 Workflow

```mermaid
sequenceDiagram
    autonumber
    actor User as 使用者
    participant Login as Login View
    participant Auth as AuthenticationController
    participant Service as Authentication Service
    participant Password as Password Service
    participant Token as Token Service
    participant DB as SQL Server

    User->>Login: 輸入帳號與密碼
    Login->>Auth: GET /api/v1/auth/csrf
    Auth-->>Login: XSRF-TOKEN Cookie／Token
    Login->>Auth: POST /api/v1/auth/login<br/>X-CSRF-TOKEN + LoginRequest
    Auth->>Service: LoginAsync(LoginRequest)
    Service->>DB: 查詢 Account、User、Roles、Functions
    DB-->>Service: 帳號、PasswordHash 與權限資料
    Service->>Password: 驗證密碼

    alt 帳密錯誤或帳號停用
        Password-->>Service: 驗證失敗
        Service-->>Auth: 登入失敗
        Auth-->>Login: 401 ProblemDetails
        Login-->>User: 顯示通用登入失敗訊息
    else 驗證成功
        Password-->>Service: 驗證成功
        Service->>Token: 建立 Access Token 與 Refresh Token
        Token-->>Service: JWT、Refresh Token、Token Hash
        Service->>DB: 保存 Refresh Token Hash 與 Token Family
        Service-->>Auth: 登入結果
        Auth-->>Login: 設定 Access／Refresh HttpOnly Cookie
        Login->>Auth: GET /api/v1/auth/me
        Auth-->>Login: User、Roles、Functions
        Login-->>User: 導向 Work Item List 或管理頁面
    end
```

## 4. Access Token Refresh Workflow

Refresh 由前端在受保護 API 回傳 401 後受控觸發，原始 Request 最多重送一次，避免無限循環。

```mermaid
sequenceDiagram
    autonumber
    participant View as Frontend View
    participant API as Protected API
    participant Auth as AuthenticationController
    participant Token as Token Service
    participant DB as SQL Server

    View->>API: 呼叫受保護 API
    API-->>View: 401 Access Token 逾期
    View->>Auth: POST /api/v1/auth/refresh<br/>X-CSRF-TOKEN + Refresh Cookie
    Auth->>DB: 以 Token Hash 查詢 Refresh Token
    DB-->>Auth: Token、Family、有效期限與撤銷狀態

    alt Token 有效且未使用
        Auth->>Token: 建立新 Access／Refresh Token
        Token-->>Auth: 新 Token 與 Hash
        Auth->>DB: 同一 Transaction 撤銷舊 Token並保存新 Token
        Auth-->>View: 更新 HttpOnly Cookie
        View->>API: 重送一次原始 Request
        API-->>View: 正常 Response
    else Token 無效、逾期、撤銷或偵測重播
        Auth->>DB: 撤銷該 Token Family
        Auth-->>View: 清除 Cookie並回傳 401
        View->>View: 返回 Login View
    end
```

## 5. Work Item 列表與詳情 Workflow

所有登入使用者取得相同的有效 Work Item；個人確認狀態只能由 JWT 中的 UserId 決定，
API 不接受前端傳入其他 UserId。

```mermaid
sequenceDiagram
    autonumber
    actor User as 登入使用者
    participant View as Work Item View
    participant Controller as WorkItemController
    participant Service as WorkItem Query Service
    participant Repository as WorkItem Repository
    participant DB as SQL Server

    User->>View: 開啟列表或詳情
    View->>Controller: GET /api/v1/work-items<br/>或 GET /api/v1/work-items/{workItemId}
    Controller->>Controller: 從 JWT Claims 取得目前 UserId
    Controller->>Service: 查詢條件 + CurrentUserId
    Service->>Repository: QueryAsync(CurrentUserId, Query)
    Repository->>DB: 查詢未軟刪除 WorkItems<br/>LEFT JOIN 該使用者的 UserWorkItemStates
    DB-->>Repository: Work Item 與個人狀態資料
    Repository-->>Service: Query Result
    Service->>Service: 無狀態資料列時映射為 Pending
    Service-->>Controller: Response DTO
    Controller-->>View: 200 + statusCode、isConfirmed、confirmedAt
    View-->>User: 顯示列表或詳情與個人狀態
```

## 6. 個人確認 Workflow

Checkbox 是前端暫選狀態，不寫入後端。只有使用者按下 Confirm 後，後端才保存個人狀態。

### 6.1 單筆確認與撤銷

```mermaid
flowchart TD
    Action{使用者操作} -->|確認| Confirm["PUT /api/v1/work-items/{workItemId}/confirmation"]
    Action -->|撤銷| Revoke["DELETE /api/v1/work-items/{workItemId}/confirmation"]

    Confirm --> CurrentUser1[從 JWT 取得目前 UserId]
    CurrentUser1 --> Check1{Work Item 存在且未軟刪除？}
    Check1 -->|否| NotFound1[404 ProblemDetails]
    Check1 -->|是| Upsert[以 UserId + WorkItemId<br/>冪等 Upsert 為 Confirm]
    Upsert --> Confirmed[Commit 並回傳 204]

    Revoke --> CurrentUser2[從 JWT 取得目前 UserId]
    CurrentUser2 --> Check2{Work Item 存在且未軟刪除？}
    Check2 -->|否| NotFound2[404 ProblemDetails]
    Check2 -->|是| Delete[冪等刪除該使用者的狀態列]
    Delete --> Pending[Commit 並回傳 204<br/>後續查詢映射為 Pending]
```

### 6.2 批次確認

```mermaid
sequenceDiagram
    autonumber
    actor User as 使用者
    participant View as Work Item List View
    participant Controller as WorkItemController
    participant Service as Confirmation Service
    participant DB as SQL Server

    User->>View: 勾選多筆 Work Item
    View->>View: 暫存在前端 Checkbox 集合
    User->>View: 按下 Confirm
    View->>Controller: POST /api/v1/work-items/confirmations/batch<br/>X-CSRF-TOKEN + workItemIds
    Controller->>Controller: 從 JWT 取得目前 UserId
    Controller->>Service: ConfirmBatchAsync(CurrentUserId, workItemIds)
    Service->>DB: 驗證所有 Work Item 存在且未軟刪除

    alt 任一 Work Item 無效
        DB-->>Service: 驗證失敗
        Service-->>Controller: 不開啟或回滾 Transaction
        Controller-->>View: 404／400 ProblemDetails，沒有部分成功
    else 全部有效
        Service->>DB: 開始單一 Transaction
        Service->>DB: 逐筆冪等 Upsert 個人狀態為 Confirm
        alt 全部寫入成功
            Service->>DB: Commit
            Service-->>Controller: Success
            Controller-->>View: 204 No Content
        else 任一寫入失敗
            Service->>DB: Rollback
            Service-->>Controller: Failure
            Controller-->>View: ProblemDetails，沒有部分成功
        end
    end
```

## 7. 管理端 Work Item CRUD Workflow

管理端 Request 必須同時通過 JWT、CSRF 與 `WorkItems.Manage` Function 驗證。

```mermaid
sequenceDiagram
    autonumber
    actor Admin as 後台使用者
    participant View as Admin View
    participant Controller as WorkItemController
    participant Service as WorkItem Command Service
    participant DB as SQL Server

    Admin->>View: 新增、修改或刪除 Work Item
    View->>Controller: POST／PUT／DELETE<br/>Cookie + X-CSRF-TOKEN
    Controller->>Controller: 驗證 JWT 與 WorkItems.Manage
    Controller->>Service: Command + CurrentUserId
    Service->>Service: 驗證欄位與商業規則

    alt 新增
        Service->>DB: 開始 Transaction
        Service->>DB: INSERT WorkItem
        Service->>DB: INSERT History，Action = INSERT
    else 修改
        Service->>DB: 檢查 RowVersion
        alt RowVersion 已過期
            Service-->>Controller: Conflict
            Controller-->>View: 409 ProblemDetails
        else RowVersion 一致
            Service->>DB: 開始 Transaction
            Service->>DB: UPDATE WorkItem
            Service->>DB: INSERT History，Action = UPDATE
        end
    else 刪除
        Service->>DB: 開始 Transaction
        Service->>DB: 設定軟刪除欄位
        Service->>DB: INSERT History，Action = DELETE
    end

    opt 已進入 Transaction
        alt Work Item 與 History 都成功
            Service->>DB: Commit
            Service-->>Controller: Success
            Controller-->>View: 201／200／204
        else 任一寫入失敗
            Service->>DB: Rollback
            Service-->>Controller: Failure
            Controller-->>View: ProblemDetails
        end
    end
```

## 8. 使用者與權限管理 Workflow

本流程供具有對應 Manage Function 的後台使用者操作。若最終 MVP 縮小範圍，Roles 與
Functions 可改為唯讀種子資料，但授權判斷流程不變。

```mermaid
flowchart TD
    Request[後台送出管理 Request] --> Target{管理類型}
    Target -->|User| UserPermission{具有 Users.Manage？}
    Target -->|Role| RolePermission{具有 Roles.Manage？}
    Target -->|Function| FunctionPermission{具有 Functions.Manage？}

    UserPermission -->|否| Forbidden[403 ProblemDetails]
    RolePermission -->|否| Forbidden
    FunctionPermission -->|否| Forbidden

    UserPermission -->|是| UserAction[建立或修改使用者、啟停帳號、<br/>重設密碼、覆寫角色]
    RolePermission -->|是| RoleAction[建立或修改角色、啟停角色、<br/>配置 Functions]
    FunctionPermission -->|是| FunctionAction[建立或修改 Function、啟停 Function]

    UserAction --> Transaction[在明確 Transaction Boundary 寫入]
    RoleAction --> Transaction
    FunctionAction --> Transaction
    Transaction --> Invalidate[更新授權快取或要求重新簽發 Token]
    Invalidate --> Response[回傳成功 Response]
```

## 9. Logout Workflow

```mermaid
sequenceDiagram
    autonumber
    actor User as 使用者
    participant View as Frontend View
    participant Auth as AuthenticationController
    participant DB as SQL Server

    User->>View: 按下 Logout
    View->>Auth: POST /api/v1/auth/logout<br/>X-CSRF-TOKEN + Cookie
    Auth->>DB: 撤銷目前 Refresh Token Family
    DB-->>Auth: 撤銷完成
    Auth-->>View: 清除 Access／Refresh Cookie並回傳 204
    View->>View: 清除記憶體中的使用者與暫選狀態
    View-->>User: 返回 Login View
```

## 10. 主要 Runtime 規則

1. 使用者身分只來自驗證後的 JWT，API 不接受前端指定其他 `UserId`。
2. 所有登入使用者看到相同的有效 Work Item，個人確認狀態以
   `(UserId, WorkItemId)` 隔離。
3. 沒有個人狀態資料列時回傳 `Pending`；確認後保存為 `Confirm`。
4. Checkbox 暫選狀態只存在前端，不進入資料庫。
5. 所有寫入 Request 必須通過 CSRF 驗證。
6. 批次確認使用單一 Transaction，不允許部分成功。
7. Work Item 採軟刪除；一般列表與詳情排除已刪除資料。
8. Work Item 修改使用 RowVersion；並行衝突回傳 409。
9. Work Item CRUD 與 History 必須在同一 Transaction 完成。
10. API 錯誤統一使用 ProblemDetails，並明確區分 400、401、403、404、409、500。
