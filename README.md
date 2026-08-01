# MyWorkItem Backend

MyWorkItem Backend 是以 .NET 10 建立的 ASP.NET Core Web API。登入使用者可以查看所有
有效 Work Item，並保存只屬於自己的確認狀態；後台角色可管理 Work Item，系統管理員
則可維護使用者、角色與 Function 權限。

本儲存庫只包含後端 API，前端為獨立專案。

## 技術摘要

- ASP.NET Core Web API、JWT HttpOnly Cookie、CSRF Double Submit Cookie
- Dapper、SqlKata、Microsoft SQL Server
- DbUp SQL Migration
- Swagger／OpenAPI
- NUnit、FluentAssertions、Bogus、NSubstitute、Testcontainers
- Docker Compose、SQL Server 2022 Developer

## 文件

| 項目 | 文件或入口 |
| --- | --- |
| 啟動方式、Demo 路徑、開發帳號 | 本文件 |
| C4 Context／Container 架構圖 | [docs/Architecture.md](docs/Architecture.md) |
| API 規格與呼叫流程 | [docs/API.md](docs/API.md) |
| Swagger UI | Development 啟動後開啟 `/swagger` |
| Swagger JSON | Development 啟動後取得 `/swagger/v1/swagger.json` |
| DB ERD 與 Table Schema | [database/Schema.md](database/Schema.md) |
| DB Migration | [src/MyWorkItem.DatabaseMigrator/Scripts](src/MyWorkItem.DatabaseMigrator/Scripts) |

## 前端串接狀態

`MyWorkItem_FrontEnd` 已依 [API 規格](docs/API.md) 完成串接，涵蓋登入、Session 還原、
Token Refresh、Work Item CRUD、批次確認、撤銷確認及 `rowversion` 併發更新。

本機開發時先啟動本專案的 API（預設 `http://localhost:5080`），再於前端專案執行
`npm run dev`。前端預設透過 Vite Proxy 將 `/api` 轉送至本 API；跨來源部署則必須
同步設定前端 `VITE_API_BASE_URL` 與本專案的 `Cors:AllowedOrigins`。

串接採 HttpOnly Cookie 保存 Access／Refresh Token。登入後前端會重新取得 CSRF Token，
並在所有寫入請求附加 `X-CSRF-TOKEN` Header；前端路由限制不取代後端 Function 授權。

## 必要環境

- .NET SDK 10
- Docker Desktop（Docker 全棧模式或整合測試需要）
- 可用的 1433、5080 Port，或在 `.env` 修改 `MSSQL_PORT`、`API_PORT`

Apple Silicon 會以 `linux/amd64` 模擬執行 SQL Server 2022。此方式只供本機開發，
不是 Microsoft 正式支援的 ARM SQL Server 環境。

## Docker 全棧模式

建立本機設定檔：

```bash
cp .env.example .env
```

修改 `.env`：

```dotenv
MSSQL_SA_PASSWORD=請設定符合SQLServer複雜度規則的本機密碼
JWT_SIGNING_KEY=請設定至少32bytes的本機隨機字串
FRONTEND_ORIGIN=http://localhost:5173
MSSQL_PORT=1433
API_PORT=5080
```

SA 密碼至少需要 8 個字元，並包含大寫、小寫、數字、符號四類中的至少三類。
`.env` 已由 Git 排除，不得提交任何真實 Secret。

啟動完整環境：

```bash
docker compose up --build
```

Compose 會依序等待 SQL Server 健康、執行 DbUp Migration 與 Development Seed，最後
啟動 API。另開終端確認：

```bash
curl http://localhost:5080/health
```

預期回應：

```json
{"status":"Healthy"}
```

停止並保留資料：

```bash
docker compose down
```

若確定不需要現有本機資料，可一併刪除 SQL Server Volume：

```bash
docker compose down --volumes
```

## IDE 偵錯模式

先啟動 SQL Server 並執行 Migration：

```bash
docker compose up -d sqlserver
docker compose run --rm migrator
```

在 Rider 建立 `.NET Project` Run Configuration，專案選擇 `MyWorkItem.Api`，並加入：

| 環境變數 | 本機值 |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Development` |
| `ASPNETCORE_URLS` | `http://localhost:5080` |
| `Jwt__SigningKey` | 至少 32 bytes 的本機隨機字串 |
| `ConnectionStrings__DefaultConnection` | `Server=localhost,1433;Database=MyWorkItem;User Id=sa;Password=你的SA密碼;TrustServerCertificate=True;Encrypt=True` |

`.env` 是 Docker Compose 的設定來源，Rider 直接執行 ASP.NET Core 時不會自動載入。
不要把真實 Secret 寫入會進版控的 `appsettings.json` 或 `launchSettings.json`。

## Demo 路徑

API 啟動後可使用：

| 用途 | URL |
| --- | --- |
| Swagger UI | <http://localhost:5080/swagger> |
| Swagger JSON | <http://localhost:5080/swagger/v1/swagger.json> |
| 原生 OpenAPI JSON | <http://localhost:5080/openapi/v1.json> |
| Health Check | <http://localhost:5080/health> |
| 取得 CSRF Token | <http://localhost:5080/api/v1/auth/csrf> |

完整登入與 Work Item Demo 流程請參考 [API 規格](docs/API.md#demo-呼叫流程)。

## Development／Test 帳號

以下固定帳號只會由 Migrator 在 Development／Test 建立：

| 帳號 | 密碼 | 角色 | 主要權限 |
| --- | --- | --- | --- |
| `Admin` | `Admin` | Admin | 全部權限 |
| `User` | `User` | User | 查看及確認 Work Item |
| `BackOffice` | `BackOffice` | BackOffice | 查看及管理 Work Item |
| `PowerUser` | `PowerUser` | User、BackOffice | 查看、確認及管理 Work Item |

以上弱密碼不得用於 Production。Production 必須透過 `Bootstrap__AdminPassword` 提供
至少 12 字元的初始管理員密碼，否則 Migrator 會停止 Bootstrap。

## Cookie 與 CSRF

- Access Token Cookie：`mwi_access`
- Refresh Token Cookie：`mwi_refresh`
- 前端可讀 CSRF Cookie：`XSRF-TOKEN`
- CSRF Header：`X-CSRF-TOKEN`

前端必須先呼叫 `GET /api/v1/auth/csrf`，再將 `XSRF-TOKEN` Cookie 的值放入所有
`POST`、`PUT`、`PATCH`、`DELETE` 請求的 `X-CSRF-TOKEN` Header。Access／Refresh
Token 由 HttpOnly Cookie 傳送，跨來源前端請求必須啟用 Credentials。

## 建置與測試

```bash
dotnet restore MyWorkItem.Backend.sln
dotnet build MyWorkItem.Backend.sln --no-restore
dotnet test MyWorkItem.Backend.sln --no-build
dotnet format MyWorkItem.Backend.sln --verify-no-changes
```
