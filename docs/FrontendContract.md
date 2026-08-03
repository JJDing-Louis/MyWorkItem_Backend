# 前端／API 契約

## 連線資訊

| 模式 | Base URL | 設定來源 |
| --- | --- | --- |
| 全 Docker | `http://localhost:5080` | Compose `.env` |
| Rider／`dotnet run` | `http://localhost:5170` | `appsettings.json`＋User Secrets |

- API Prefix：`/api/v1`
- Content-Type：`application/json`
- OpenAPI：`/swagger/v1/swagger.json`
- Swagger UI：`/swagger`（非 Production 且啟用時）

前端只應連到一個 API Instance。Docker API 與 IDE API 同時執行時，兩者使用相同 `localhost` Cookie 範圍，容易互相覆蓋 JWT／CSRF Cookie。

## Authentication 與 CSRF

- Access Token Cookie：`mwi_access`，HttpOnly。
- Refresh Token Cookie：`mwi_refresh`，HttpOnly。
- Antiforgery Cookie：`mwi_antiforgery`，HttpOnly。
- 前端可讀 CSRF Cookie：`XSRF-TOKEN`。
- CSRF Header：`X-CSRF-TOKEN`。
- 所有 Request 必須使用 `credentials: "include"`。
- 所有 `POST`、`PUT`、`PATCH`、`DELETE` 都必須攜帶有效 CSRF Header，包含 Login、Refresh 與 Logout。

標準登入流程：

```text
GET /api/v1/auth/csrf
  → 讀取 XSRF-TOKEN Cookie
POST /api/v1/auth/login
  → Cookie 自動帶入
  → X-CSRF-TOKEN: <XSRF-TOKEN 值>
  → 成功後取得 mwi_access／mwi_refresh
GET /api/v1/auth/csrf
  → 重新取得綁定已登入身分的 CSRF Token
```

Access Token 過期時，前端明確呼叫 `/auth/refresh` 後再重送原 Request；不得無限自動重試。

## 權限與身分

- API 不接受前端傳入 `UserId` 決定目前操作者；身分只能來自 JWT Cookie。
- Function Authorization 每個 Request 都查詢目前 Account、Role 與 Function 聯集，因此停用帳號或權限調整會立即生效。
- `Worker`：`WorkItems.Read`、`WorkItems.Confirm`。
- `Manager`：Worker 權限＋`WorkItems.Manage`、`Users.Manage`。
- `Admin`：全部 Functions。

## Work Item UI 契約

- 所有登入使用者取得相同的有效 Work Item；`isConfirmed`、`confirmedAt` 依目前使用者計算。
- `assignedUserId` 可為 `null`，只用於管理資訊、顯示與篩選，不限制查看或確認。
- Checkbox 是前端暫存狀態，不寫入後端；批次送出後才保存 Confirm。
- 單筆確認與撤銷是冪等操作；批次確認最多 100 筆且必須全部成功或全部 Rollback。
- Response 與 Update Request 的 `rowVersion` 都是 Base64 字串；`409` 時必須重新取得最新資料再讓使用者決定。
- Description 使用 `nvarchar(max)`，但整體 Request Body 上限為 1 MiB。

## 分頁與錯誤

Work Item 列表回傳：

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalCount": 0,
  "totalPages": 0
}
```

錯誤使用 `application/problem+json`。前端至少處理：

| Status | 處理建議 |
| --- | --- |
| `400` | 顯示欄位驗證或 CSRF 錯誤；CSRF 錯誤重新取得 Token |
| `401` | 嘗試一次 Refresh；仍失敗則導回 Login |
| `403` | 顯示無權限並重新取得 `/auth/me` |
| `404` | 顯示資源不存在或已被軟刪除 |
| `409` | 顯示資料衝突並重新載入最新資料 |
| `429` | 顯示稍後重試，不要立即密集重送 |
| `500` | 顯示一般錯誤，不呈現內部例外或 SQL |

欄位與 Response Schema 的可執行契約以 `/swagger/v1/swagger.json` 為準；完整 Endpoint 清單見 [ApiList.md](ApiList.md)。
