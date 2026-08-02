# Implementation Plan: MyWorkItem Backend MVP

**Branch**: `dev2` | **Date**: 2026-08-03 | **Spec**: [spec.md](spec.md)

## Summary

建立分層的 .NET 10 Web API，以安全 Cookie 提供 JWT 登入，以 Dapper＋SqlKata 存取 SQL Server，
以 DbUp 管理 Schema V1.1，並完成個人確認、管理端 CRUD、完整權限、Swagger 及三層自動化測試。

## Technical Context

**Language/Version**: C# / .NET 10  
**Primary Dependencies**: ASP.NET Core、Dapper、SqlKata、Microsoft.Data.SqlClient、DbUp、Swashbuckle  
**Storage**: SQL Server 2022 Developer  
**Testing**: NUnit、FluentAssertions、Bogus、NSubstitute、WebApplicationFactory、Testcontainers  
**Target Platform**: Linux container 與 macOS IDE 開發  
**Project Type**: ASP.NET Core Web API  
**Performance Goals**: 一般列表與寫入在本機開發環境保持互動式回應；分頁避免無界查詢  
**Constraints**: 無 EF Core、1 MiB Request Body、批次最多 100 筆、Cookie Auth 必須 CSRF  
**Scale/Scope**: 單一後端服務、11 張主要資料表、5 組 Controller、3 個測試專案

## Constitution Check

- PASS：Schema V1.1 已核准且 Migration 可追溯。
- PASS：Domain／Application 不依賴 Infrastructure。
- PASS：Cookie、CSRF、Refresh Token、Secret 與授權策略符合安全預設。
- PASS：Unit、Integration、Workflow 與 Docker 驗證均納入完成條件。
- PASS：不修改前端、不使用 EF Core、不執行未授權 Git 或資料刪除。

## Project Structure

### Documentation

```text
specs/001-myworkitem-backend/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── workflow-test-matrix.md
├── contracts/openapi.yaml
├── checklists/requirements.md
└── tasks.md
```

### Source Code

```text
src/
├── MyWorkItem.Api/
├── MyWorkItem.Application/
├── MyWorkItem.Domain/
├── MyWorkItem.Infrastructure/
└── MyWorkItem.DatabaseMigrator/
tests/
├── MyWorkItem.UnitTests/
├── MyWorkItem.IntegrationTests/
└── MyWorkItem.WorkflowTests/
```

**Structure Decision**: 採五個正式專案與三個測試專案；API 只處理 HTTP，Application 管理 Use Case，
Domain 保存核心型別，Infrastructure 實作 SQL／Token／Password，Migrator 獨立執行 DbUp 與 Seeder。

## Design Decisions

- UUID Entity 主鍵；`WorkItemHistories.HistoryId` 例外使用 bigint identity。
- `AssignedUserId` 可為 NULL，只作資訊與篩選，不限制可見性或確認。
- 無 `UserWorkItemStates` 資料列代表 Pending；確認 Upsert Confirm；撤銷刪除。
- `DeletedAt` 是軟刪除唯一來源，不保存 `IsDeleted`。
- Work Item CRUD 與修改後快照 History 共用 Transaction。
- Access Token 15 分鐘、Refresh Token 7 天且每次輪替。
- Swagger 在 Development 啟用，Production 預設完全關閉。

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
| --- | --- | --- |
| 三個測試專案 | WorkflowTests 必須驗證完整 HTTP 旅程且與單點 IntegrationTests 分離 | 混在同一專案會模糊測試責任與執行成本 |
| 五個正式專案 | 隔離 HTTP、Use Case、Domain、Infrastructure 與 Migration | 單一專案會破壞已核准的責任邊界與可測試性 |
