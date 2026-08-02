---
description: "MyWorkItem Backend MVP dependency-ordered implementation tasks"
---

# Tasks: MyWorkItem Backend MVP

**Input**: `specs/001-myworkitem-backend/` 內的 spec、plan、data-model、contracts、workflow matrix  
**Tests**: 採 TDD；每個 Story 先建立可失敗的測試，再完成實作。

## Phase 1: Setup

- [x] T001 建立 `MyWorkItem.Backend.sln` 與五個正式、三個測試專案
- [x] T002 建立 `Directory.Build.props`、`Directory.Packages.props` 與專案參考
- [x] T003 [P] 完成 `.gitignore`、`.dockerignore`、`.env.example` 與基礎 README
- [x] T004 [P] 建立 Api／Application／Domain／Infrastructure Composition Root 骨架

## Phase 2: Foundational

- [x] T005 建立 `database/migrations/001_InitialSchema.sql` 實作 Schema V1.1
- [x] T006 建立 `database/migrations/002_StaticData.sql` 與固定 GUID／權限矩陣
- [x] T007 建立 `src/MyWorkItem.DatabaseMigrator` DbUp runner 與環境 Seeder
- [x] T008 [P] 建立 Domain entities、Code constants 與共用結果型別
- [x] T009 [P] 建立 Application DTO、驗證、Repository／Clock／CurrentUser／Transaction 抽象
- [x] T010 建立 Infrastructure ConnectionFactory、SqlKata compiler 與 Dapper 基礎
- [x] T011 建立 Api ProblemDetails、Exception Handler、Health、CORS、Options validation
- [x] T012 建立 Schema Integration Tests 驗證 Migration、約束、Index 與重跑

## Phase 3: User Story 1 - 登入與查詢（P1）

- [x] T013 [P] [US1] 建立 Authentication Unit／Integration tests
- [x] T014 [P] [US1] 建立 Work Item query Unit／Integration tests
- [x] T015 [US1] 實作 Password、JWT、Refresh Token repository 與 token rotation
- [x] T016 [US1] 實作 CSRF、Cookie Authentication、Function Authorization Handler
- [x] T017 [US1] 實作 `AuthenticationController` 的 csrf/login/refresh/logout/me
- [x] T018 [US1] 實作 Work Item list/detail SqlKata query 與 `WorkItemController`

## Phase 4: User Story 2 - 個人確認（P1）

- [x] T019 [P] [US2] 建立單筆、撤銷、批次、跨使用者與Rollback tests
- [x] T020 [US2] 實作 UserWorkItemStates repository 與確認／撤銷 Use Cases
- [x] T021 [US2] 實作批次確認單一 Transaction 與最多100筆驗證
- [x] T022 [US2] 實作 confirmation endpoints

## Phase 5: User Story 3 - Work Item 管理（P2）

- [x] T023 [P] [US3] 建立 CRUD、AssignedUserId、RowVersion、軟刪除與History tests
- [x] T024 [US3] 實作 Work Item create/update/delete repository transaction
- [x] T025 [US3] 實作 after-snapshot WorkItemHistories
- [x] T026 [US3] 實作管理端 Work Item endpoints 與 Function policies

## Phase 6: User Story 4 - 使用者與權限管理（P3）

- [x] T027 [P] [US4] 建立 Users／Roles／Functions 管理與即時授權 tests
- [x] T028 [US4] 實作 Users repository、建立、修改、啟停、密碼與角色覆寫
- [x] T029 [US4] 實作 Roles／Functions repository、啟停與配置覆寫
- [x] T030 [US4] 實作 Users／Roles／Functions Controllers

## Phase 7: User Story 5 - Swagger（P3）

- [x] T031 [P] [US5] 建立 OpenAPI contract、Production 關閉與敏感欄位 tests
- [x] T032 [US5] 設定 Swashbuckle、XML docs、Cookie security 與 ProblemDetails responses
- [x] T033 [US5] 實作 CSRF header OperationFilter 與 Swagger request interceptor
- [x] T034 [US5] 加入 Request／Response examples 與 Controller tags

## Phase 8: Workflow Tests

- [x] T035 建立 WorkflowTestApplicationFactory、CookieSession、CSRF 與 API clients
- [x] T036 [P] 實作 WF-01～WF-03 Pipeline／Login／Refresh
- [x] T037 [P] 實作 WF-04～WF-06 Query／Confirmation／Batch
- [x] T038 [P] 實作 WF-07～WF-09 CRUD／Permissions／Logout
- [x] T039 實作 WF-10 完整旅程與 WF-11 Swagger 旅程

## Phase 9: Docker、文件與驗證

- [x] T040 [P] 建立 multi-stage non-root `Dockerfile`
- [x] T041 建立 SQL Server→Migrator→API `compose.yaml` 與 Health Check
- [x] T042 [P] 更新 README、ERD、Workflow、Swagger 與 Secret 說明
- [x] T043 執行 restore、build、test、format 並修正全部失敗
- [x] T044 執行 Docker Compose、Migration、Health、Swagger 與 Smoke Flow
- [x] T045 執行敏感資料掃描、`git diff --check` 並記錄最終證據

## Dependencies

- Phase 1 → Phase 2 → US1 → US2 → US3 → US4 → US5 → Workflow → Docker／驗證。
- 同一 Story 內測試先於實作；Infrastructure 實作不得早於 Application 抽象。
- T039 依賴 T036～T038；T044 依賴全部功能與 Docker artifacts。

## Independent test criteria

- **US1**：登入後取得身分並查看全部有效 Work Item 與自己的狀態。
- **US2**：A 確認不影響 B，重新登入持久化，批次無部分成功。
- **US3**：管理 CRUD、指派、並行衝突與 History Transaction 正確。
- **US4**：Admin／Manager／Worker 權限矩陣及停用立即生效。
- **US5**：Swagger 不複製 Token 即完成 CSRF、Login 與受保護寫入。
