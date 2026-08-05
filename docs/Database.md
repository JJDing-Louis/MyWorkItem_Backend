# Database 結構與 ERD

正式 DDL 以 `database/migrations/` 為唯一來源。DbUp 從空資料庫依序執行 `001_InitialSchema.sql`、`002_StaticData.sql`，並在 Journal 記錄已套用版本。

目前包含 12 張業務資料表；實際資料庫另有 1 張 DbUp Journal Table。

## 身分與權限

```mermaid
%%{init: {"flowchart": {"defaultRenderer": "elk", "nodeSpacing": 50, "rankSpacing": 75}}}%%
erDiagram
    USERS ||--|| ACCOUNTS : "一對一"
    USERS ||--o{ USER_ROLES : "配置角色"
    ROLES ||--o{ USER_ROLES : "包含使用者"
    ROLES ||--o{ ROLE_FUNCTIONS : "配置功能"
    FUNCTIONS ||--o{ ROLE_FUNCTIONS : "被角色使用"
    ACCOUNTS ||--o{ REFRESH_TOKENS : "持有 Token"
    REFRESH_TOKENS o|--o| REFRESH_TOKENS : "輪替取代"

    USERS {
        uniqueidentifier UserId PK
        nvarchar Name
        nvarchar Email UK "NULL 可重複"
        nvarchar Remark
        datetimeoffset CreatedAt
        datetimeoffset UpdatedAt
    }
    ACCOUNTS {
        uniqueidentifier AccountId PK
        uniqueidentifier UserId FK,UK
        nvarchar LoginName
        nvarchar NormalizedLoginName UK
        nvarchar PasswordHash
        bit IsEnabled
    }
    ROLES {
        uniqueidentifier RoleId PK
        nvarchar Code UK
        nvarchar Name
        bit IsEnabled
    }
    FUNCTIONS {
        uniqueidentifier FunctionId PK
        nvarchar Code UK
        nvarchar Name
        bit IsEnabled
    }
    USER_ROLES {
        uniqueidentifier UserId PK,FK
        uniqueidentifier RoleId PK,FK
        bit IsEnabled
        datetimeoffset AssignedAt
    }
    ROLE_FUNCTIONS {
        uniqueidentifier RoleId PK,FK
        uniqueidentifier FunctionId PK,FK
        bit IsEnabled
        datetimeoffset UpdatedAt
    }
    REFRESH_TOKENS {
        uniqueidentifier RefreshTokenId PK
        uniqueidentifier AccountId FK
        uniqueidentifier FamilyId
        nvarchar TokenHash UK
        datetimeoffset ExpiresAt
        datetimeoffset RevokedAt
    }
```

## Work Item 領域

```mermaid
%%{init: {"flowchart": {"defaultRenderer": "elk", "nodeSpacing": 55, "rankSpacing": 80}}}%%
erDiagram
    USERS ||--o{ WORK_ITEMS : "建立／指派／刪除"
    USERS ||--o{ USER_WORK_ITEM_STATES : "個人確認"
    WORK_ITEMS ||--o{ USER_WORK_ITEM_STATES : "每人狀態"
    WORK_ITEM_STATUSES ||--o{ USER_WORK_ITEM_STATES : "狀態代碼"
    WORK_ITEMS ||--o{ WORK_ITEM_HISTORIES : "CRUD 歷程"
    ACTIONS ||--o{ WORK_ITEM_HISTORIES : "異動類型"
    USERS ||--o{ WORK_ITEM_HISTORIES : "異動者"

    WORK_ITEMS {
        uniqueidentifier WorkItemId PK
        nvarchar Title
        nvarchar_max Description
        uniqueidentifier CreatedByUserId FK
        uniqueidentifier AssignedUserId FK "可為 NULL"
        datetimeoffset CreatedAt
        datetimeoffset UpdatedAt
        datetimeoffset DeletedAt "可為 NULL"
        uniqueidentifier DeletedByUserId FK "可為 NULL"
        rowversion RowVersion
    }
    USER_WORK_ITEM_STATES {
        uniqueidentifier UserId PK,FK
        uniqueidentifier WorkItemId PK,FK
        uniqueidentifier WorkItemStatusId FK
        datetimeoffset ConfirmedAt
        datetimeoffset UpdatedAt
    }
    WORK_ITEM_STATUSES {
        uniqueidentifier WorkItemStatusId PK
        nvarchar Code UK
        nvarchar Name
        bit IsEnabled
    }
    WORK_ITEM_HISTORIES {
        bigint HistoryId PK
        uniqueidentifier WorkItemId FK
        uniqueidentifier ActionId FK
        uniqueidentifier ChangedByUserId FK
        datetimeoffset ChangedAt
        nvarchar SnapshotTitle
        uniqueidentifier SnapshotAssignedUserId
        binary SourceRowVersion
    }
    ACTIONS {
        uniqueidentifier ActionId PK
        nvarchar Code UK
        nvarchar Name
        bit IsEnabled
    }
```

## Table 責任摘要

| Table | 主鍵 | 責任 |
| --- | --- | --- |
| `Users` | `UserId` | 個人資料與稽核時間 |
| `Accounts` | `AccountId` | 登入名稱、Password Hash、啟用狀態；`UserId` UNIQUE |
| `Roles` | `RoleId` | 角色定義，`Code` 建立後不可修改 |
| `Functions` | `FunctionId` | Function 定義，`Code` 建立後不可修改 |
| `UserRoles` | `(UserId, RoleId)` | 使用者與角色多對多配置 |
| `RoleFunctions` | `(RoleId, FunctionId)` | 角色與 Function 多對多配置 |
| `RefreshTokens` | `RefreshTokenId` | Token Hash、Family、期限、撤銷與輪替關係 |
| `WorkItems` | `WorkItemId` | Work Item 內容、可選指派、軟刪除與 RowVersion |
| `UserWorkItemStates` | `(UserId, WorkItemId)` | 每位使用者自己的確認狀態 |
| `WorkItemHistories` | `HistoryId` | Work Item CRUD 的 after-snapshot 稽核 |
| `WorkItemStatuses` | `WorkItemStatusId` | `Pending`／`Confirm` 靜態代碼 |
| `Actions` | `ActionId` | `INSERT`／`UPDATE`／`DELETE` 靜態代碼 |

## 資料一致性規則

- 所有業務 UUID 使用 `uniqueidentifier`；時間使用 UTC `datetimeoffset(7)`。
- `Accounts.UserId` UNIQUE，確保 User／Account 一對一；登入名稱以 `NormalizedLoginName` UNIQUE。
- `AssignedUserId` 可為 `NULL`，只作顯示與篩選，不限制其他登入者查看或確認。
- `UserWorkItemStates` 無資料列代表 `Pending`；確認時 Upsert `Confirm`，撤銷時刪除資料列。
- `WorkItems` 不保存全域 Status 或 `IsDeleted`；`DeletedAt`／`DeletedByUserId` 成對表示軟刪除。
- `rowversion` 用於 Update 的 Optimistic Concurrency；不一致時 API 回傳 `409 Conflict`。
- Work Item CRUD 與 `WorkItemHistories` after-snapshot 在同一 Transaction；個人確認不寫入 Work Item History。
- `WorkItemHistories.ChangedByUserId` 記錄實際操作使用者；例如 Admin 編輯時會保存 Admin 的 `UserId`，查詢時可連接 `Accounts` 顯示 `LoginName = 'Admin'`，不重複保存可變動的帳號名稱。
- FK 未設定 Cascade Delete，刪除行為由應用程式明確管理。
- `IX_WorkItems_Active_CreatedAt` 支援有效項目預設排序；`IX_WorkItems_AssignedUserId` 支援指派篩選。

## Schema 版本控制

新增 Schema 變更時必須增加下一個不可變更的 Migration，不得回頭修改已部署 Migration。測試需從空 SQL Server 驗證 Migration 可建立、可重跑，並核對 PK、FK、UNIQUE、CHECK、Index 與靜態資料。
