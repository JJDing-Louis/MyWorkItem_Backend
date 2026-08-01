# MyWorkItem API 規格

## 可執行規格

Development 環境啟動 API 後：

| 規格 | URL |
| --- | --- |
| Swagger UI | <http://localhost:5080/swagger> |
| Swagger JSON | <http://localhost:5080/swagger/v1/swagger.json> |
| ASP.NET Core OpenAPI JSON | <http://localhost:5080/openapi/v1.json> |

本文件提供端點索引與安全性操作說明；Request／Response Schema 的可執行版本以
Swagger JSON 為準。

## 基本約定

- Base URL：`http://localhost:5080`
- Content-Type：`application/json`
- Access Token：`mwi_access` HttpOnly Cookie
- Refresh Token：`mwi_refresh` HttpOnly Cookie
- CSRF Cookie：`XSRF-TOKEN`
- CSRF Header：`X-CSRF-TOKEN`
- 錯誤格式：RFC 7807 `ProblemDetails`
- 時間：UTC `datetimeoffset`
- 識別碼：UUID

除了 `GET`、`HEAD`、`OPTIONS`、`TRACE`，所有 HTTP Method 都必須攜帶有效的
`X-CSRF-TOKEN` Header；這也包含 Login、Refresh 與 Logout。

## 權限

| Function | 用途 |
| --- | --- |
| `WorkItems.Read` | 查看有效 Work Item |
| `WorkItems.Confirm` | 確認、撤銷或批次確認自己的 Work Item |
| `WorkItems.Manage` | 新增、修改及軟刪除 Work Item |
| `Users.Manage` | 管理使用者、狀態、密碼及角色 |
| `Roles.Manage` | 管理角色及角色 Function |
| `Functions.Manage` | 管理 Function |

| 角色 | 預設 Function |
| --- | --- |
| Admin | 全部 Function |
| User | `WorkItems.Read`、`WorkItems.Confirm` |
| BackOffice | `WorkItems.Read`、`WorkItems.Manage` |
| PowerUser | User 與 BackOffice 的聯集 |

## Auth API

| Method | Path | 驗證 | CSRF | 說明 |
| --- | --- | --- | --- | --- |
| GET | `/api/v1/auth/csrf` | 匿名 | 不需要 | 建立 Antiforgery Cookie 並回傳可讀的 `XSRF-TOKEN` Cookie |
| POST | `/api/v1/auth/login` | 匿名 | 需要 | 驗證帳密，寫入 Access／Refresh Cookie |
| POST | `/api/v1/auth/refresh` | Refresh Cookie | 需要 | 輪替 Refresh Token 並更新兩個 Token Cookie |
| POST | `/api/v1/auth/logout` | 匿名 | 需要 | 撤銷目前 Token Family 並刪除 Cookie |
| GET | `/api/v1/auth/me` | 已登入 | 不需要 | 取得目前帳號、使用者、角色與 Function |

Login Request：

```json
{
  "userName": "User",
  "password": "User"
}
```

## Work Item API

| Method | Path | Function | CSRF | 說明 |
| --- | --- | --- | --- | --- |
| GET | `/api/v1/work-items` | `WorkItems.Read` | 不需要 | 分頁取得有效 Work Item 與目前使用者的確認狀態 |
| GET | `/api/v1/work-items/{workItemId}` | `WorkItems.Read` | 不需要 | 取得詳情與目前使用者的確認狀態 |
| POST | `/api/v1/work-items` | `WorkItems.Manage` | 需要 | 新增 Work Item |
| PUT | `/api/v1/work-items/{workItemId}` | `WorkItems.Manage` | 需要 | 以 `version` 做並行檢查後更新 |
| DELETE | `/api/v1/work-items/{workItemId}` | `WorkItems.Manage` | 需要 | 軟刪除 Work Item |
| PUT | `/api/v1/work-items/{workItemId}/confirmation` | `WorkItems.Confirm` | 需要 | 冪等設為目前使用者已確認 |
| DELETE | `/api/v1/work-items/{workItemId}/confirmation` | `WorkItems.Confirm` | 需要 | 冪等撤銷目前使用者確認 |
| POST | `/api/v1/work-items/confirmations/batch` | `WorkItems.Confirm` | 需要 | 單一交易確認 1 至 100 筆 Work Item |

清單 Query：

| 參數 | 預設值 | 說明 |
| --- | --- | --- |
| `page` | `1` | 頁碼 |
| `pageSize` | `20` | 每頁筆數 |
| `keyword` | 空 | Title／Description 關鍵字 |
| `direction` | `desc` | 依 `CreatedAt` 使用 `asc` 或 `desc` 排序 |

Create Request：

```json
{
  "title": "確認部署檢查表",
  "description": "完成正式環境部署前檢查"
}
```

Update Request 中的 `version` 必須使用前一次查詢回傳的值，不可自行產生：

```json
{
  "title": "確認部署檢查表（更新）",
  "description": "完成正式環境部署前檢查",
  "version": "AAAAAAAAB9E="
}
```

Batch Confirmation Request：

```json
{
  "workItemIds": [
    "00000000-0000-0000-0000-000000000001",
    "00000000-0000-0000-0000-000000000002"
  ]
}
```

API 不接受 `UserId` 決定確認者；確認者只會取自已驗證 JWT。

## User API

全部端點需要 `Users.Manage`。

| Method | Path | CSRF | 說明 |
| --- | --- | --- | --- |
| GET | `/api/v1/users` | 不需要 | 分頁查詢使用者，支援 `page`、`pageSize`、`keyword` |
| GET | `/api/v1/users/{userId}` | 不需要 | 取得使用者詳情與角色 |
| POST | `/api/v1/users` | 需要 | 建立帳號、個人資料並指派角色 |
| PUT | `/api/v1/users/{userId}` | 需要 | 修改姓名、Email、Remark |
| PATCH | `/api/v1/users/{userId}/status` | 需要 | 啟用或停用帳號 |
| POST | `/api/v1/users/{userId}/reset-password` | 需要 | 重設至少 12 字元的密碼 |
| PUT | `/api/v1/users/{userId}/roles` | 需要 | 覆寫帳號的角色集合 |

## Role API

全部端點需要 `Roles.Manage`。

| Method | Path | CSRF | 說明 |
| --- | --- | --- | --- |
| GET | `/api/v1/roles` | 不需要 | 取得角色與所含 Function |
| POST | `/api/v1/roles` | 需要 | 建立角色 |
| PUT | `/api/v1/roles/{roleId}` | 需要 | 修改名稱與啟用狀態 |
| PUT | `/api/v1/roles/{roleId}/functions` | 需要 | 覆寫角色的 Function 集合 |

## Function API

全部端點需要 `Functions.Manage`。

| Method | Path | CSRF | 說明 |
| --- | --- | --- | --- |
| GET | `/api/v1/functions` | 不需要 | 取得 Function 清單 |
| POST | `/api/v1/functions` | 需要 | 建立 Function |
| PUT | `/api/v1/functions/{functionId}` | 需要 | 修改名稱與啟用狀態 |

## Demo 呼叫流程

以下命令使用 Cookie Jar 保存 CSRF、Access 與 Refresh Cookie：

```bash
curl -sS -c cookies.txt http://localhost:5080/api/v1/auth/csrf -o /dev/null

csrf_token=$(awk '$6 == "XSRF-TOKEN" { print $7 }' cookies.txt)

curl -sS -b cookies.txt -c cookies.txt \
  -H "Content-Type: application/json" \
  -H "X-CSRF-TOKEN: ${csrf_token}" \
  -d '{"userName":"User","password":"User"}' \
  http://localhost:5080/api/v1/auth/login

curl -sS -b cookies.txt http://localhost:5080/api/v1/auth/me

curl -sS -b cookies.txt \
  "http://localhost:5080/api/v1/work-items?page=1&pageSize=20&direction=desc"
```

`cookies.txt` 含有本機登入 Cookie，不得提交版本控制；Demo 完成後應刪除。

## 回應狀態與錯誤

| Status | 用途 |
| --- | --- |
| `200 OK` | 查詢、登入、更新成功 |
| `201 Created` | 建立成功 |
| `204 No Content` | 無回應內容的操作成功 |
| `400 Bad Request` | DTO 驗證或 CSRF 驗證失敗 |
| `401 Unauthorized` | 未登入、Token 或帳密無效 |
| `403 Forbidden` | 已登入但缺少 Function |
| `404 Not Found` | 資源不存在或 Work Item 已軟刪除 |
| `409 Conflict` | 唯一值或 `rowversion` 並行衝突 |

ProblemDetails 範例：

```json
{
  "type": "about:blank",
  "title": "資料衝突",
  "status": 409,
  "detail": "Work Item 已被其他使用者修改，請重新載入後再試。",
  "instance": "/api/v1/work-items/00000000-0000-0000-0000-000000000001"
}
```
