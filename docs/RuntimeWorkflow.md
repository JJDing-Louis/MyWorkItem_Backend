# Runtime Workflow

```mermaid
%%{init: {"flowchart": {"defaultRenderer": "elk", "nodeSpacing": 50, "rankSpacing": 75}}}%%
flowchart TD
    start(["使用者開啟前端"])

    subgraph authentication["1. Authentication"]
        direction LR
        csrfAnonymous["GET /auth/csrf"]
        csrfCookie["取得匿名<br/>XSRF-TOKEN Cookie"]
        login["POST /auth/login<br/>＋ X-CSRF-TOKEN"]
        verify["驗證帳密<br/>建立 Token Family"]
        saveRefresh[("SQL Server<br/>保存 Refresh Token Hash")]
        authCookies["回傳 Access／Refresh<br/>HttpOnly Cookie"]

        csrfAnonymous --> csrfCookie --> login --> verify --> saveRefresh --> authCookies
    end

    subgraph query["2. Query Work Items"]
        direction LR
        list["GET /work-items"]
        authorize["即時驗證 Account<br/>與 Role Function 聯集"]
        queryItems[("SQL Server<br/>SqlKata 組合查詢<br/>Dapper 執行映射")]
        listResult["回傳 Items<br/>isConfirmed＋rowVersion"]

        list --> authorize --> queryItems --> listResult
    end

    subgraph confirmation["3. Personal Confirmation"]
        direction LR
        csrfAuthenticated["GET /auth/csrf<br/>重新綁定登入身分"]
        confirm["PUT /work-items/{id}/confirmation<br/>＋ X-CSRF-TOKEN"]
        transaction[("SQL Server Transaction<br/>Upsert UserWorkItemStates")]
        noContent["204 No Content"]

        csrfAuthenticated --> confirm --> transaction --> noContent
    end

    start --> csrfAnonymous
    authCookies --> list
    listResult --> csrfAuthenticated

    classDef step fill:#EFF6FF,stroke:#2563EB,color:#1E3A8A,stroke-width:1.25px;
    classDef security fill:#FFF7ED,stroke:#EA580C,color:#7C2D12,stroke-width:1.25px;
    classDef storage fill:#ECFDF5,stroke:#059669,color:#064E3B,stroke-width:1.5px;
    class csrfAnonymous,csrfCookie,login,authCookies,csrfAuthenticated,confirm security;
    class verify,list,authorize,listResult,noContent step;
    class saveRefresh,queryItems,transaction storage;
```

管理者 CRUD 時，Work Item mutation 與 `WorkItemHistories` after-snapshot 必須在同一 Transaction；Update 的 Base64 RowVersion 不符時回 409。批次確認先鎖定並確認全部 ID 有效，任一不存在即 Rollback。

## 操作者使用情境 Workflow

以下流程串接後台使用者（Manager／Admin）與一般操作者（Worker）。後台使用者先維護 Work Item，Worker 再查看及操作個人確認狀態。Checkbox 只保存在瀏覽器，只有送出確認後，後端才會依目前 JWT 的 `UserId` 保存個人確認狀態。

### 後台使用者登入與授權 Decision

此段依照 Decision 慣例固定位置：「是」向右進入後台，「否」垂直向下回傳拒絕結果。

```mermaid
flowchart TD
    subgraph backOfficeSignIn["1. 後台使用者登入與授權"]
        direction TB

        subgraph decisionMainFlow[" "]
            direction LR
            openBackOffice(["Manager／Admin<br/>開啟後台登入畫面"])
            backOfficeLogin["輸入帳密<br/>取得 CSRF Token 並登入"]
            managePermission{"具備<br/>WorkItems.Manage？"}
            permissionGranted["授權成功<br/>進入後台管理"]

            openBackOffice --> backOfficeLogin --> managePermission
            managePermission -->|"是"| permissionGranted
        end

        forbidden["回傳 403 ProblemDetails<br/>禁止進入管理功能"]
        managePermission -->|"否"| forbidden
    end

    classDef security fill:#FFF7ED,stroke:#EA580C,color:#7C2D12,stroke-width:1.25px;
    classDef decision fill:#FEFCE8,stroke:#CA8A04,color:#713F12,stroke-width:1.25px;
    classDef interaction fill:#EFF6FF,stroke:#2563EB,color:#1E3A8A,stroke-width:1.25px;
    class openBackOffice,backOfficeLogin,forbidden security;
    class managePermission decision;
    class permissionGranted interaction;
    style decisionMainFlow fill:transparent,stroke:transparent;
```

### 後台管理 Work Item Workflow

後台使用者通過登入及 `WorkItems.Manage` 授權後，進入 Work Item 管理流程：

```mermaid
%%{init: {"flowchart": {"defaultRenderer": "elk", "nodeSpacing": 55, "rankSpacing": 80}}}%%
flowchart TD
    backOfficeAuthorized(["後台使用者已通過<br/>登入與 WorkItems.Manage 授權"])

    subgraph backOfficeManage["2. 後台管理 Work Item"]
        direction TD
        loadAdminItems["載入 Work Item 管理清單"]
        createDecision{"新增 Work Item？"}
        updateDecision{"修改或重新指派？"}
        createItem["新增 Work Item<br/>可指派使用者或保持未指派"]
        updateItem["修改內容或重新指派<br/>必須提供 RowVersion"]
        rowVersionDecision{"RowVersion 正確？"}
        deleteItem["軟刪除 Work Item"]
        workItemTransaction[("同一 Transaction<br/>更新 WorkItems 並寫入 History 快照")]
        conflict["RowVersion 衝突<br/>回傳 409 並重新載入"]
        workItemAvailable(["最新有效 Work Item<br/>可供所有登入使用者查看"])

        loadAdminItems --> createDecision
        createDecision -->|"是"| createItem
        createDecision -->|"否"| updateDecision
        updateDecision -->|"是"| updateItem
        updateDecision -->|"否"| deleteItem
        updateItem --> rowVersionDecision
        rowVersionDecision -->|"是"| workItemTransaction
        rowVersionDecision -->|"否"| conflict
        createItem --> workItemTransaction
        deleteItem --> workItemTransaction
        workItemTransaction --> workItemAvailable
    end

    backOfficeAuthorized --> loadAdminItems

    classDef decision fill:#FEFCE8,stroke:#CA8A04,color:#713F12,stroke-width:1.25px;
    classDef storage fill:#ECFDF5,stroke:#059669,color:#064E3B,stroke-width:1.5px;
    classDef outcome fill:#F5F3FF,stroke:#7C3AED,color:#4C1D95,stroke-width:1.5px;
    classDef management fill:#FDF4FF,stroke:#A21CAF,color:#701A75,stroke-width:1.25px;
    class loadAdminItems,createItem,updateItem,deleteItem,conflict management;
    class createDecision,updateDecision,rowVersionDecision decision;
    class workItemTransaction storage;
    class backOfficeAuthorized,workItemAvailable outcome;
```

後台保存完成的有效 Work Item，會成為 Worker 查詢清單的資料來源；兩張圖不使用跨圖連線。

### Worker 使用 Workflow

Worker 登入後查看全部有效 Work Item，後端依目前 JWT 身分附加該 Worker 自己的確認狀態：

```mermaid
%%{init: {"flowchart": {"defaultRenderer": "elk", "nodeSpacing": 55, "rankSpacing": 80}}}%%
flowchart TD
    availableItems(["後台已建立的<br/>最新有效 Work Item"])

    subgraph workerBrowse["1. Worker 登入與瀏覽"]
        direction TD
        workerLogin["Worker 輸入帳密<br/>取得 CSRF Token 並登入"]
        readPermission{"具備 WorkItems.Read<br/>與 WorkItems.Confirm？"}
        workerForbidden["回傳 403 ProblemDetails<br/>禁止查看或確認"]
        loadItems["載入全部有效 Work Item<br/>與自己的確認狀態"]
        chooseAction{"批次確認？"}

        workerLogin --> readPermission
        readPermission -->|"是"| loadItems --> chooseAction
        readPermission -->|"否"| workerForbidden
    end

    subgraph confirmation["2. Worker 個人確認"]
        direction TD
        singleFlow["查看詳情並依目前狀態<br/>確認或撤銷確認"]
        batchFlow["Checkbox 暫存在瀏覽器<br/>送出最多 100 筆批次確認"]
        persisted[("UserWorkItemStates<br/>依 UserId＋WorkItemId 保存")]
        refreshed["重新整理清單<br/>顯示最新個人狀態"]

        singleFlow --> persisted
        batchFlow --> persisted
        persisted --> refreshed
    end

    subgraph revisit["3. 離開後再次使用"]
        direction TD
        leave["登出或關閉網頁<br/>稍後重新登入"]
        retained(["仍顯示該操作者<br/>先前的確認狀態"])

        leave --> retained
    end

    availableItems --> workerLogin
    chooseAction -->|"否：單筆"| singleFlow
    chooseAction -->|"是：批次"| batchFlow
    refreshed --> leave

    classDef interaction fill:#EFF6FF,stroke:#2563EB,color:#1E3A8A,stroke-width:1.25px;
    classDef security fill:#FFF7ED,stroke:#EA580C,color:#7C2D12,stroke-width:1.25px;
    classDef decision fill:#FEFCE8,stroke:#CA8A04,color:#713F12,stroke-width:1.25px;
    classDef storage fill:#ECFDF5,stroke:#059669,color:#064E3B,stroke-width:1.5px;
    classDef outcome fill:#F5F3FF,stroke:#7C3AED,color:#4C1D95,stroke-width:1.5px;
    class workerLogin,workerForbidden security;
    class loadItems,singleFlow,batchFlow,refreshed,leave interaction;
    class readPermission,chooseAction decision;
    class persisted storage;
    class availableItems,retained outcome;
```

使用情境重點：

- 登入失敗不會進入 Work Item 清單，API 以 ProblemDetails 回傳錯誤。
- Manager 與 Admin 具備 `WorkItems.Manage`，可新增、修改、指派、清除指派及軟刪除 Work Item；Worker 不具備此權限。
- 後台修改與 Work Item History after-snapshot 必須使用同一 Transaction；RowVersion 衝突回傳 `409 Conflict`。
- Manager 另具備 `Users.Manage`；Admin 具備全部 Functions，可額外管理 Roles 與 Functions。這些權限變更會於下一個 Request 立即生效。
- 所有登入使用者可看到相同的有效 Work Item；`isConfirmed` 與 `confirmedAt` 依操作者而異。
- 單筆確認、撤銷及批次確認都以 JWT 身分決定操作者，Request 不接受 `UserId`。
- 批次確認最多 100 筆，任一項目無效時整批 Rollback。
- 重新登入後，後端從 `UserWorkItemStates` 還原該操作者先前的確認狀態。
