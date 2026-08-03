# Quickstart

## IDE 模式

1. 複製 `.env.example` 為 `.env`，設定本機 Secret，不提交 `.env`。
2. 啟動 SQL Server 與 Migrator。
3. 設定 API 的 ConnectionStrings、JWT Key、Frontend Origin 與 Development Seed 密碼。
4. 執行 `dotnet run --project src/MyWorkItem.Api`。
5. 開啟 `http://localhost:5080/swagger`。

## Docker 模式

```bash
docker compose config
docker compose up --build
curl --fail http://localhost:5080/health
```

成功條件：SQL Server healthy、Migrator exit 0、API healthy。

## Swagger 流程

1. 開啟 Swagger UI；頁面載入時自動取得 CSRF Cookie。
2. 執行 `POST /api/v1/auth/login`。
3. 執行 `GET /api/v1/auth/me`。
4. 使用 Manager 或 Admin 建立、指派及修改 Work Item。
5. 使用 Worker 確認與撤銷。
6. 執行 Refresh 與 Logout。

Swagger Request Interceptor 會自動攜帶 Cookie 與 `X-CSRF-TOKEN`。
