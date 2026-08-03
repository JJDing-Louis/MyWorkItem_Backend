# API 清單

本文件依目前 `MyWorkItem.Api` 的 Controller、Request／Response Contract 與 Middleware 實作整理。
可執行的正式契約仍以 `/swagger/v1/swagger.json` 產生的 OpenAPI 文件為準。

現況基準：`dev2`／`0f74ec3`。Docker 與 IDE 請只啟動其中一個 API Instance。

## 基本資訊

- Docker Base URL：`http://localhost:5080`
- Rider／`dotnet run` Base URL：`http://localhost:5170`
- API Prefix：`/api/v1`
- Swagger UI：`/swagger`
- OpenAPI JSON：`/swagger/v1/swagger.json`
- Health Check：`/health`
- Controller API：28 個
- 加上 Health Check：共 29 個 HTTP Endpoint

## 共通安全規則

- Access Token 與 Refresh Token 使用 HttpOnly Cookie，不使用 Bearer Header。
- 除 Health Check 與標示匿名的 Authentication API 外，其餘 API 均需登入。
- 所有 `POST`、`PUT`、`PATCH`、`DELETE` 必須先呼叫 `GET /api/v1/auth/csrf`，並在 Request 帶入 Cookie 與 `X-CSRF-TOKEN` Header。
- API 不接受前端傳入 `UserId` 決定目前操作者；身分一律取自已驗證 JWT。
- 錯誤回應使用 ProblemDetails；依情境可能回傳 `400`、`401`、`403`、`404`、`409`、`429`、`500`。
- Authentication Controller 使用固定視窗 Rate Limit：每分鐘最多 100 次 Request。

## Authentication

| Method | Path | 登入 | CSRF | 成功 | 說明 |
| --- | --- | ---: | ---: | ---: | --- |
| `GET` | `/api/v1/auth/csrf` | 不需要 | 不需要 | `204` | 產生 Antiforgery Cookie 與前端可讀取的 `XSRF-TOKEN` |
| `POST` | `/api/v1/auth/login` | 不需要 | 需要 | `200` | 驗證帳密並設定 Access／Refresh Cookie |
| `POST` | `/api/v1/auth/refresh` | 不需要 | 需要 | `200` | 使用 Refresh Cookie 輪替 Token |
| `POST` | `/api/v1/auth/logout` | 不需要 | 需要 | `204` | 撤銷 Refresh Token Family 並清除 Cookie |
| `GET` | `/api/v1/auth/me` | 需要 | 不需要 | `200` | 取得目前使用者、角色與 Function |

### Login Request

```json
{
  "loginName": "Worker",
  "password": "本機環境設定的密碼"
}
```

限制：

- `loginName`：必填，最長 100 字元。
- `password`：必填；登入時不套用新密碼複雜度規則。

### Current User Response

```json
{
  "userId": "00000000-0000-0000-0000-000000000000",
  "accountId": "00000000-0000-0000-0000-000000000000",
  "loginName": "Worker",
  "name": "一般操作者",
  "roles": ["Worker"],
  "functions": ["WorkItems.Read", "WorkItems.Confirm"]
}
```

## Work Items

| Method | Path | Function 權限 | 成功 | 說明 |
| --- | --- | --- | ---: | --- |
| `GET` | `/api/v1/work-items` | `WorkItems.Read` | `200` | 分頁查詢有效 Work Item 與目前使用者的確認狀態 |
| `GET` | `/api/v1/work-items/user-options` | `WorkItems.Read` | `200` | 取得 Work Item 顯示、指派與篩選所需的精簡使用者清單 |
| `GET` | `/api/v1/work-items/{workItemId}` | `WorkItems.Read` | `200` | 取得 Work Item 詳情 |
| `POST` | `/api/v1/work-items` | `WorkItems.Manage` | `201` | 建立 Work Item |
| `PUT` | `/api/v1/work-items/{workItemId}` | `WorkItems.Manage` | `200` | 修改 Work Item，使用 RowVersion 控制並行更新 |
| `DELETE` | `/api/v1/work-items/{workItemId}` | `WorkItems.Manage` | `204` | 軟刪除 Work Item |
| `PUT` | `/api/v1/work-items/{workItemId}/confirmation` | `WorkItems.Confirm` | `204` | 確認目前使用者的 Work Item |
| `DELETE` | `/api/v1/work-items/{workItemId}/confirmation` | `WorkItems.Confirm` | `204` | 撤銷目前使用者的確認 |
| `POST` | `/api/v1/work-items/confirmations/batch` | `WorkItems.Confirm` | `204` | 批次確認 1 至 100 筆 Work Item |

### 列表 Query Parameters

| 參數 | 型別 | 預設值 | 限制／用途 |
| --- | --- | --- | --- |
| `page` | `int` | `1` | 必須大於等於 1 |
| `pageSize` | `int` | `20` | 1 至 100 |
| `keyword` | `string?` | 無 | 搜尋 Title 與 Description |
| `sortDirection` | `string` | `desc` | 只接受 `asc` 或 `desc` |
| `assignedUserId` | `UUID?` | 無 | 依指派使用者篩選 |

範例：

```http
GET /api/v1/work-items?page=1&pageSize=20&keyword=文件&sortDirection=desc&assignedUserId={UUID}
```

### Create Work Item Request

```json
{
  "title": "準備上線文件",
  "description": "確認上線步驟及 Rollback 計畫",
  "assignedUserId": null
}
```

- `title`：必填，最長 200 字元。
- `assignedUserId`：可為 `null`，代表尚未指派。

### Work Item User Options

回傳 `userId`、`loginName`、`name` 與 `isEnabled`。清單包含停用帳號，以便顯示及篩選歷史資料；建立或修改 Work Item 時只能指派啟用帳號。

### Update Work Item Request

```json
{
  "title": "準備正式上線文件",
  "description": "補充資料庫 Migration 步驟",
  "assignedUserId": "00000000-0000-0000-0000-000000000000",
  "rowVersion": "AAAAAAAAB9E="
}
```

- `rowVersion` 為 Base64 字串。
- 資料已被其他使用者修改時回傳 `409 Conflict`。

### Batch Confirmation Request

```json
{
  "workItemIds": [
    "00000000-0000-0000-0000-000000000001",
    "00000000-0000-0000-0000-000000000002"
  ]
}
```

- 必須包含 1 至 100 筆 ID。
- 任一 Work Item 不存在或已刪除時整批失敗，不允許部分成功。

## Users

本 Controller 的所有 Endpoint 均需要 `Users.Manage`。

| Method | Path | 成功 | 說明 |
| --- | --- | ---: | --- |
| `GET` | `/api/v1/users` | `200` | 取得全部使用者；目前尚未實作分頁 |
| `GET` | `/api/v1/users/{userId}` | `200` | 取得使用者詳情 |
| `POST` | `/api/v1/users` | `201` | 建立 User、Account 並配置角色 |
| `PUT` | `/api/v1/users/{userId}` | `200` | 修改姓名、Email 與備註 |
| `PATCH` | `/api/v1/users/{userId}/status` | `204` | 啟用或停用帳號 |
| `POST` | `/api/v1/users/{userId}/reset-password` | `204` | 重設密碼並撤銷現有 Refresh Token |
| `PUT` | `/api/v1/users/{userId}/roles` | `204` | 覆寫使用者角色 |

### Create User Request

```json
{
  "loginName": "Worker02",
  "password": "符合安全規則的密碼",
  "name": "第二位操作者",
  "email": "worker02@example.com",
  "remark": null,
  "roleIds": ["00000000-0000-0000-0000-000000000000"]
}
```

- 密碼至少 12 字元，並須符合大寫、小寫、數字、符號四類中的三類。
- 使用者至少需要一個存在且啟用的角色。
- LoginName 或 Email 重複時回傳 `409 Conflict`。

### Update User Request

```json
{
  "name": "修改後姓名",
  "email": "worker02@example.com",
  "remark": "備註"
}
```

### Set User Status Request

```json
{
  "isEnabled": false
}
```

### Reset Password Request

```json
{
  "password": "新的安全密碼"
}
```

### Replace User Roles Request

```json
{
  "roleIds": [
    "00000000-0000-0000-0000-000000000001",
    "00000000-0000-0000-0000-000000000002"
  ]
}
```

## Roles

本 Controller 的所有 Endpoint 均需要 `Roles.Manage`。

| Method | Path | 成功 | 說明 |
| --- | --- | ---: | --- |
| `GET` | `/api/v1/roles` | `200` | 取得角色及其 Functions |
| `POST` | `/api/v1/roles` | `201` | 建立角色 |
| `PUT` | `/api/v1/roles/{roleId}` | `200` | 修改角色名稱、說明及啟用狀態 |
| `PUT` | `/api/v1/roles/{roleId}/functions` | `204` | 覆寫角色 Functions |

### Create Role Request

```json
{
  "code": "Reviewer",
  "name": "審核人員",
  "description": "負責 Work Item 審核"
}
```

### Update Role Request

```json
{
  "name": "資深審核人員",
  "description": "修改後說明",
  "isEnabled": true
}
```

### Replace Role Functions Request

```json
{
  "functionIds": [
    "00000000-0000-0000-0000-000000000001",
    "00000000-0000-0000-0000-000000000002"
  ]
}
```

Role Code 建立後不可修改。

## Functions

本 Controller 的所有 Endpoint 均需要 `Functions.Manage`。

| Method | Path | 成功 | 說明 |
| --- | --- | ---: | --- |
| `GET` | `/api/v1/functions` | `200` | 取得全部 Functions |
| `POST` | `/api/v1/functions` | `201` | 建立 Function |
| `PUT` | `/api/v1/functions/{functionId}` | `200` | 修改 Function 名稱、說明及啟用狀態 |

Function 使用與 Role 相同的 Create／Update Request 格式，且 Code 建立後不可修改。

目前定義的 Function Code：

| Code | 用途 |
| --- | --- |
| `WorkItems.Read` | 查看 Work Item |
| `WorkItems.Confirm` | 確認或撤銷個人 Work Item 狀態 |
| `WorkItems.Manage` | 建立、修改、指派及軟刪除 Work Item |
| `Users.Manage` | 管理使用者與角色配置 |
| `Roles.Manage` | 管理角色與 Function 配置 |
| `Functions.Manage` | 管理 Function 定義 |

## Health Check 與開發文件

| Method | Path | 是否計入 API 數量 | 說明 |
| --- | --- | ---: | --- |
| `GET` | `/health` | 是 | 回傳 API 健康狀態 |
| `GET` | `/swagger` | 否 | Swagger UI；僅非 Production 且已啟用時提供 |
| `GET` | `/swagger/v1/swagger.json` | 否 | OpenAPI v1 JSON；僅非 Production 且已啟用時提供 |

Production 環境強制不公開 Swagger UI 與 OpenAPI JSON。

## 啟動設定來源

| 執行模式 | Database 連線 | JWT Key |
| --- | --- | --- |
| Docker Compose | `.env` 的 `MSSQL_APP_PASSWORD` 組成 `myworkitem` 連線字串 | `.env` 的 `JWT_SIGNING_KEY` |
| Rider／`dotnet run` | ASP.NET Core User Secrets 的 `ConnectionStrings:DefaultConnection` | User Secrets 的 `Jwt:SigningKey` |

`MSSQL_SA_PASSWORD` 只供 SQL Server 與 `sqlserver-init` 使用；API 與 Migrator 不直接使用 `sa`。修改 `.env` 不會自動變更既有 Volume 內已生效的 SQL Login 密碼，需讓 `sqlserver-init` 成功執行。

## 目前實作限制

- `GET /api/v1/users` 尚未提供分頁、排序與關鍵字查詢。
- Roles 與 Functions 沒有個別詳情 Endpoint。
- Users、Roles、Functions 不提供硬刪除 Endpoint；使用啟用狀態管理生命週期。
- Work Item 指派只作管理資訊與篩選，不限制其他已登入使用者查看或確認。
- Checkbox 暫選狀態不寫入後端；後端只保存送出後的個人確認狀態。
