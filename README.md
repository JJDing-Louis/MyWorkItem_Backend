# MyWorkItem Backend

MyWorkItem Backend 是 .NET 10 ASP.NET Core Web API。資料存取採 Dapper＋SqlKata，Schema 由 DbUp 管理，驗證採 JWT Access／Refresh Cookie，所有 unsafe request 需通過 CSRF。

## 快速啟動

```bash
cp .env.example .env
# 修改 .env 中的 SA 密碼、JWT Key 與種子帳號密碼
docker compose up --build
```

啟動後：

- Health：<http://localhost:5080/health>
- Swagger UI：<http://localhost:5080/swagger>
- OpenAPI JSON：<http://localhost:5080/swagger/v1/swagger.json>

Development／Test Seeder 會建立 `Admin`、`Manager`、`Worker`；密碼來自 `.env` 的 `SEED_*_PASSWORD`。若未設定，僅 Development／Test 使用 `.env.example` 所示的示範預設值。Production 不執行此 Seeder。

## IDE 模式

IDE 不會自動讀取 Compose 的 `.env`。先啟動資料庫與 Migrator：

```bash
docker compose up sqlserver migrator
```

再於 Rider Run Configuration 或 User Secrets 設定：

```text
ConnectionStrings__DefaultConnection=Server=localhost,14333;Database=MyWorkItem;User Id=sa;Password=<本機密碼>;Encrypt=True;TrustServerCertificate=True
Jwt__SigningKey=<至少 32 bytes 的本機 Key>
Swagger__Enabled=true
```

不要把上述實際值寫入 `appsettings*.json` 或提交 `.env`。

## Swagger 登入流程

Swagger 頁面會取得 `XSRF-TOKEN`，每次 `POST`／`PUT`／`PATCH`／`DELETE` 前重新取得與目前登入身分相符的 Token，並自動加入 `X-CSRF-TOKEN`。執行 `/api/v1/auth/login` 後，瀏覽器會自行攜帶 HttpOnly JWT／Refresh Cookie，不需複製 JWT。

## 驗證

```bash
dotnet restore
dotnet build --no-restore
dotnet test --no-build
dotnet format --verify-no-changes
docker compose --env-file .env.example config
```

完整 Docker 驗證使用 `docker compose up --build`；不會自動刪除 SQL Server Volume。

## 文件

- [架構圖](docs/Architecture.md)
- [DB 結構與 ERD](docs/Database.md)
- [Runtime Workflow](docs/RuntimeWorkflow.md)
- [前端／API 契約](docs/FrontendContract.md)
- [Spec Kit 規格](specs/001-myworkitem-backend/spec.md)
- [Workflow Test Matrix](specs/001-myworkitem-backend/workflow-test-matrix.md)

## 安全注意事項

- Production 強制不公開 Swagger，即使 `Swagger:Enabled=true`。
- CORS 只接受 `Cors:AllowedOrigins`，並允許 Credentials；不得設成 `*`。
- Access Token 15 分鐘、Refresh Token 7 天且每次輪替；重播舊 Token 會撤銷整個 Family。
- Response DTO 不包含 Password、PasswordHash、Refresh Token 或 Token Hash。
