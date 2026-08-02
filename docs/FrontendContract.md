# 前端／API 契約

- Base path：`/api/v1`
- Content-Type：`application/json`
- 驗證：`mwi_access`／`mwi_refresh` HttpOnly Cookie
- CSRF：先呼叫 `GET /auth/csrf`，將可讀的 `XSRF-TOKEN` Cookie 值送入 `X-CSRF-TOKEN` Header。
- 登入後需重新取得一次 CSRF Token，因 Token 會綁定目前登入身分。
- 寫入端點均需 CSRF；API 不接受確認者 `UserId`。
- RowVersion：Response 與 Update Request 均使用 Base64 字串。
- Description 上限受整體 Request Body 1 MiB 限制。
- Checkbox 暫選只存前端；後端只保存 Confirm 狀態。
- 錯誤使用 ProblemDetails；常用狀態碼為 400、401、403、404、409。

可執行 OpenAPI 以 `/swagger/v1/swagger.json` 為準，不另維護第二份手寫欄位契約。
