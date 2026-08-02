# DB 結構與 ERD

正式 DDL 位於 `database/migrations/`。為避免跨領域連線互相穿越，Schema V1.1 依責任拆成兩張關聯摘要；第二張圖的 `Users（外部參照）` 是同一張 `Users` Table，不是重複資料表。

## 身分與權限

```mermaid
%%{init: {"flowchart": {"defaultRenderer": "elk", "nodeSpacing": 50, "rankSpacing": 75}}}%%
flowchart LR
    accounts["Accounts<br/><small>PK AccountId<br/>UK UserId、LoginName</small>"]
    refreshTokens["RefreshTokens<br/><small>Token Hash＋Family</small>"]
    users["Users<br/><small>PK UserId</small>"]
    userRoles["UserRoles<br/><small>PK UserId＋RoleId</small>"]
    roles["Roles<br/><small>UK Code</small>"]
    roleFunctions["RoleFunctions<br/><small>PK RoleId＋FunctionId</small>"]
    functions["Functions<br/><small>UK Code</small>"]

    refreshTokens -->|"N：1"| accounts
    accounts -->|"1：1"| users
    users -->|"1：N"| userRoles
    userRoles -->|"N：1"| roles
    roles -->|"1：N"| roleFunctions
    roleFunctions -->|"N：1"| functions

    classDef entity fill:#F8FAFC,stroke:#475569,color:#0F172A,stroke-width:1.25px;
    classDef junction fill:#FFF7ED,stroke:#EA580C,color:#7C2D12,stroke-width:1.25px;
    class accounts,refreshTokens,users,roles,functions entity;
    class userRoles,roleFunctions junction;
```

## Work Item 領域

```mermaid
%%{init: {"flowchart": {"defaultRenderer": "elk", "nodeSpacing": 55, "rankSpacing": 80}}}%%
flowchart TB
    usersRef["Users<br/><small>外部參照：同一張 Users Table</small>"]

    subgraph workItems["Work Item 領域"]
        direction LR
        workItem["WorkItems<br/><small>PK WorkItemId<br/>AssignedUserId 可為 NULL</small>"]
        states["UserWorkItemStates<br/><small>PK UserId＋WorkItemId</small>"]
        histories["WorkItemHistories<br/><small>After Snapshot</small>"]
        statuses["WorkItemStatuses<br/><small>Pending、Confirm</small>"]
        actions["Actions<br/><small>INSERT、UPDATE、DELETE</small>"]

        workItem -->|"1：N"| states
        workItem -->|"1：N"| histories
        states -->|"N：1"| statuses
        histories -->|"N：1"| actions
    end

    usersRef -->|"建立者／指派者"| workItem
    usersRef -->|"個人確認"| states
    usersRef -->|"異動者"| histories

    classDef entity fill:#F8FAFC,stroke:#475569,color:#0F172A,stroke-width:1.25px;
    classDef junction fill:#FFF7ED,stroke:#EA580C,color:#7C2D12,stroke-width:1.25px;
    classDef audit fill:#F5F3FF,stroke:#7C3AED,color:#4C1D95,stroke-width:1.25px;
    classDef lookup fill:#ECFDF5,stroke:#059669,color:#064E3B,stroke-width:1.25px;
    class usersRef,workItem entity;
    class states junction;
    class histories audit;
    class statuses,actions lookup;
```

關鍵規則：

- `Accounts.UserId` UNIQUE，確保 User／Account 一對一。
- `AssignedUserId` 可為 NULL；不限制其他登入者查看或確認。
- `UserWorkItemStates` 以 `(UserId, WorkItemId)` 為 PK；無資料代表 Pending，Confirm 時 Upsert，撤銷時刪除。
- Work Item 無 `IsDeleted` 與全域 Status；`DeletedAt` 表示軟刪除。
- `rowversion` 防止 Lost Update；CRUD 與 after-snapshot History 在同一 Transaction。
- 個人確認不寫入 Work Item History。
- 所有 FK 使用 NO ACTION，不使用 Cascade Delete。
