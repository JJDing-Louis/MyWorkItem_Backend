# Data Model: Schema V1.1

## Users

- `UserId uniqueidentifier` PK
- `Name nvarchar(200)` NOT NULL
- `Email nvarchar(320)` NULL
- `NormalizedEmail nvarchar(320)` NULL，filtered UNIQUE
- `Remark nvarchar(1000)` NULL
- `CreatedAt`、`UpdatedAt datetimeoffset(7)` NOT NULL

## Accounts

- `AccountId uniqueidentifier` PK
- `UserId uniqueidentifier` NOT NULL，FK Users，UNIQUE
- `LoginName`、`NormalizedLoginName nvarchar(100)` NOT NULL；Normalized UNIQUE
- `PasswordHash nvarchar(500)` NOT NULL
- `IsEnabled bit` NOT NULL
- `CreatedAt`、`UpdatedAt datetimeoffset(7)` NOT NULL

## Roles / Functions

- UUID PK、唯一且不可修改的 Code、Name、Description、IsEnabled、CreatedAt、UpdatedAt。
- `UserRoles(UserId, RoleId)` 複合 PK，另含 IsEnabled、AssignedAt、AssignedByUserId。
- `RoleFunctions(RoleId, FunctionId)` 複合 PK，另含 IsEnabled、UpdatedAt、UpdatedByUserId。

## RefreshTokens

- UUID PK、AccountId FK、`TokenHash varbinary(32)` UNIQUE、FamilyId。
- ExpiresAt、CreatedAt、RevokedAt、ReplacedByTokenId、RevocationReason。
- Token Family 重播時整組撤銷。

## WorkItems

- `WorkItemId uniqueidentifier` PK
- `Title nvarchar(200)` NOT NULL
- `Description nvarchar(max)` NULL
- `CreatedByUserId uniqueidentifier` NOT NULL，FK Users
- `AssignedUserId uniqueidentifier` NULL，FK Users
- CreatedAt、UpdatedAt NOT NULL；DeletedAt、DeletedByUserId NULL
- `RowVersion rowversion` NOT NULL
- CHECK：DeletedAt 與 DeletedByUserId 同時 NULL 或同時有值
- filtered index：未刪除資料依 CreatedAt DESC

## WorkItemStatuses / UserWorkItemStates

- Status UUID PK、唯一 Code；seed `Pending`、`Confirm`。
- `UserWorkItemStates(UserId, WorkItemId)` 複合 PK，另含 StatusId、ConfirmedAt、UpdatedAt。
- 無資料列視為 Pending；v1 實際只保存 Confirm。

## Actions / WorkItemHistories

- Action UUID PK、唯一 Code；seed `INSERT`、`UPDATE`、`DELETE`。
- HistoryId bigint identity PK。
- WorkItemId、ActionId、ChangedByUserId、ChangedAt。
- SnapshotTitle、SnapshotDescription、SnapshotCreatedByUserId、SnapshotAssignedUserId。
- SnapshotCreatedAt、SnapshotUpdatedAt、SnapshotDeletedAt、SnapshotDeletedByUserId、SourceRowVersion。
- WorkItem FK 不 Cascade；快照與 CRUD 共用 Transaction。

## Static permissions

- Admin：全部 Functions。
- Manager：WorkItems.Read／Confirm／Manage、Users.Manage。
- Worker：WorkItems.Read／Confirm。
