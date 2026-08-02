# Workflow Test Matrix

| ID | Runtime Workflow | 主要驗證 |
| --- | --- | --- |
| WF-01 | HTTP Pipeline | CORS、CSRF、401、403、400、500與敏感資訊遮蔽 |
| WF-02 | Login | CSRF→Login→Me、錯誤密碼、停用帳號、Cookie |
| WF-03 | Refresh | 輪替、舊 Token 重播、Family 撤銷、Cookie 清除 |
| WF-04 | Query | 全部可見、AssignedUserId 篩選、個人狀態、分頁排序 |
| WF-05 | Confirmation | 單筆確認、撤銷、冪等、重新登入、跨使用者隔離 |
| WF-06 | Batch | 最多100筆、重複正規化、無部分成功、Rollback |
| WF-07 | Work Item Admin | CRUD、指派、RowVersion、軟刪除、History Transaction |
| WF-08 | Permission Admin | Users、Roles、Functions、啟停與立即生效 |
| WF-09 | Logout | Family 撤銷、Cookie 清除、重複登出 |
| WF-10 | End-to-end | Admin→Manager→Worker完整旅程與資料庫稽核 |
| WF-11 | Swagger | 自動CSRF、Cookie Login、受保護寫入、Refresh、Logout |

每個 WorkflowTests 案例只透過 HTTP 操作；可直接讀 DB 驗證 Transaction、History 或 Token，
不可直接呼叫 Controller 或 Application Service。
