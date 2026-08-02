# Runtime Workflow

```mermaid
%%{init: {"flowchart": {"defaultRenderer": "elk", "nodeSpacing": 50, "rankSpacing": 75}}}%%
flowchart TD
    start(["使用者開啟前端"])

    subgraph authentication["1. Authentication"]
        direction TD
        csrfAnonymous["GET /auth/csrf"]
        csrfCookie["取得匿名<br/>XSRF-TOKEN Cookie"]
        login["POST /auth/login<br/>＋ X-CSRF-TOKEN"]
        verify["驗證帳密<br/>建立 Token Family"]
        saveRefresh[("SQL Server<br/>保存 Refresh Token Hash")]
        authCookies["回傳 Access／Refresh<br/>HttpOnly Cookie"]

        csrfAnonymous --> csrfCookie --> login --> verify --> saveRefresh --> authCookies
    end

    subgraph query["2. Query Work Items"]
        direction TD
        list["GET /work-items"]
        authorize["即時驗證 Account<br/>與 Role Function 聯集"]
        queryItems[("SQL Server<br/>SqlKata 組合查詢<br/>Dapper 執行映射")]
        listResult["回傳 Items<br/>isConfirmed＋rowVersion"]

        list --> authorize --> queryItems --> listResult
    end

    subgraph confirmation["3. Personal Confirmation"]
        direction TD
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

以下流程以一般操作者（Worker）為主。Checkbox 只保存在瀏覽器，只有送出確認後，後端才會依目前 JWT 的 `UserId` 保存個人確認狀態。

```mermaid
%%{init: {"flowchart": {"defaultRenderer": "elk", "nodeSpacing": 55, "rankSpacing": 80}}}%%
flowchart TD
    subgraph signIn["1. 登入"]
        direction TD
        openLogin(["開啟登入畫面"])
        submitLogin["輸入帳密<br/>取得 CSRF Token 並登入"]
        loginResult{"登入成功？"}
        loginFailed["顯示 ProblemDetails<br/>停留在登入畫面"]

        openLogin --> submitLogin --> loginResult
        loginResult -->|"否"| loginFailed
    end

    subgraph browse["2. 瀏覽 Work Item"]
        direction TD
        loadItems["載入全部有效 Work Item<br/>與個人確認狀態"]
        chooseAction{"選擇操作方式"}

        loadItems --> chooseAction
    end

    subgraph confirmation["3. 個人確認"]
        direction TD
        singleFlow["查看詳情並依目前狀態<br/>確認或撤銷確認"]
        batchFlow["Checkbox 暫存在瀏覽器<br/>送出最多 100 筆批次確認"]
        persisted[("UserWorkItemStates<br/>依 UserId＋WorkItemId 保存")]
        refreshed["重新整理清單<br/>顯示最新個人狀態"]

        singleFlow --> persisted
        batchFlow --> persisted
        persisted --> refreshed
    end

    subgraph revisit["4. 離開後再次使用"]
        direction TD
        leave["登出或關閉網頁<br/>稍後重新登入"]
        retained(["仍顯示該操作者<br/>先前的確認狀態"])

        leave --> retained
    end

    loginResult -->|"是"| loadItems
    chooseAction -->|"單筆操作"| singleFlow
    chooseAction -->|"批次操作"| batchFlow
    refreshed --> leave

    classDef interaction fill:#EFF6FF,stroke:#2563EB,color:#1E3A8A,stroke-width:1.25px;
    classDef security fill:#FFF7ED,stroke:#EA580C,color:#7C2D12,stroke-width:1.25px;
    classDef decision fill:#FEFCE8,stroke:#CA8A04,color:#713F12,stroke-width:1.25px;
    classDef storage fill:#ECFDF5,stroke:#059669,color:#064E3B,stroke-width:1.5px;
    classDef outcome fill:#F5F3FF,stroke:#7C3AED,color:#4C1D95,stroke-width:1.5px;
    class openLogin,submitLogin,loginFailed security;
    class loadItems,singleFlow,batchFlow,refreshed,leave interaction;
    class loginResult,chooseAction decision;
    class persisted storage;
    class retained outcome;
```

使用情境重點：

- 登入失敗不會進入 Work Item 清單，API 以 ProblemDetails 回傳錯誤。
- 所有登入使用者可看到相同的有效 Work Item；`isConfirmed` 與 `confirmedAt` 依操作者而異。
- 單筆確認、撤銷及批次確認都以 JWT 身分決定操作者，Request 不接受 `UserId`。
- 批次確認最多 100 筆，任一項目無效時整批 Rollback。
- 重新登入後，後端從 `UserWorkItemStates` 還原該操作者先前的確認狀態。
