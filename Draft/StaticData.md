# 靜態資料與 Development Seed

正式靜態資料由 `database/migrations/002_StaticData.sql` 建立；Development Account 由 `DevelopmentSeeder` 建立。Production 不執行 Development Seeder。

## Roles

| RoleId | Code | 說明 |
| --- | --- | --- |
| `11111111-1111-1111-1111-111111111111` | `Admin` | 系統管理員，擁有全部 Functions |
| `11111111-1111-1111-1111-111111111112` | `Manager` | 後台管理員，可管理 Work Item 與 Users |
| `11111111-1111-1111-1111-111111111113` | `Worker` | 一般使用者，可讀取及確認 Work Item |

## Functions 與 Role Matrix

| Function | Admin | Manager | Worker |
| --- | ---: | ---: | ---: |
| `WorkItems.Read` | ✓ | ✓ | ✓ |
| `WorkItems.Confirm` | ✓ | ✓ | ✓ |
| `WorkItems.Manage` | ✓ | ✓ |  |
| `Users.Manage` | ✓ | ✓ |  |
| `Roles.Manage` | ✓ |  |  |
| `Functions.Manage` | ✓ |  |  |

## WorkItemStatuses

| Code | Name | 說明 |
| --- | --- | --- |
| `Pending` | `Pending` | 無 `UserWorkItemStates` 資料列時的衍生狀態 |
| `Confirm` | `Confirm` | 使用者已確認；資料列保存 ConfirmedAt |

## Actions

| Code | 說明 | History 快照 |
| --- | --- | --- |
| `INSERT` | 新增 Work Item | 新增後快照 |
| `UPDATE` | 修改內容或指派 | 修改後快照 |
| `DELETE` | 軟刪除 | 包含刪除欄位的快照 |

## Development Accounts

| 類型 | LoginName | Role | 密碼來源 |
| --- | --- | --- | --- |
| 基本帳號 | `Admin` | Admin | `SEED_ADMIN_PASSWORD`，未設定時 Development 預設 `Admin` |
| 基本帳號 | `Manager` | Manager | `SEED_MANAGER_PASSWORD`，未設定時 Development 預設 `manager` |
| 基本帳號 | `Worker` | Worker | `SEED_WORKER_PASSWORD`，未設定時 Development 預設 `Worker` |
| 測試資料 | `Lisa1150803`、`James1150803`、`Emily1150803`、`Daniel1150803`、`Sophia1150803` | Worker | Development 固定測試密碼 |
| 測試資料 | `Michael1150803`、`Olivia1150803`、`Ethan1150803`、`Ava1150803`、`Noah1150803` | Manager | Development 固定測試密碼 |

注意事項：

- 固定短密碼只為 Development／Integration／Workflow Test 相容；不得用於 Production。
- Login API 只驗證既有 Password Hash，不套用建立密碼規則；建立使用者與重設密碼仍要求至少 12 字元且符合四類字元中的三類。
- Seeder 具有冪等性：相同 LoginName 已存在時不重複建立。
