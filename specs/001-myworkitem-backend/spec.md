# Feature Specification: MyWorkItem Backend MVP

**Feature Branch**: `dev2`  
**Created**: 2026-08-03  
**Status**: Approved for implementation  
**Input**: 建立具備登入、個人確認、後台管理、權限與可操作 API 文件的 Work Item 後端。

## User Scenarios & Testing

### User Story 1 - 登入並查看 Work Item（Priority: P1）

使用者能安全登入，查看所有有效 Work Item 的列表與詳情，並看見自己的確認狀態。

**Why this priority**: 這是所有前台操作的入口與核心價值。

**Independent Test**: 使用有效帳號登入後能查看列表與詳情；不同使用者看到相同項目，個人狀態互不影響。

**Acceptance Scenarios**:

1. **Given** 啟用帳號，**When** 使用正確帳密登入，**Then** 可取得目前身分、角色與功能。
2. **Given** 多筆有效項目，**When** 使用者查詢列表，**Then** 預設依建立時間由新到舊並回傳自己的狀態。
3. **Given** 已軟刪除項目，**When** 查詢列表或詳情，**Then** 該項目不可讀取。

---

### User Story 2 - 保存個人確認（Priority: P1）

使用者能確認、撤銷或批次確認 Work Item，結果在重新登入後仍保留且不影響其他使用者。

**Independent Test**: 使用者 A 確認後重新登入仍為 Confirm；使用者 B 查看同一項目仍為 Pending。

**Acceptance Scenarios**:

1. **Given** Pending 項目，**When** 使用者確認，**Then** 只保存該使用者的 Confirm 狀態。
2. **Given** 已確認項目，**When** 使用者撤銷，**Then** 狀態回到 Pending。
3. **Given** 多筆有效項目，**When** 批次確認，**Then** 全部成功或全部失敗，不得部分寫入。

---

### User Story 3 - 管理 Work Item（Priority: P2）

具有管理權限的使用者能新增、修改、選擇性指派、取消指派及軟刪除 Work Item，並保留完整異動歷程。

**Independent Test**: Manager 建立、指派、修改、軟刪除項目後，每次成功操作都有正確快照；一般使用者被拒絕。

**Acceptance Scenarios**:

1. **Given** 管理權限，**When** 建立未指派或已指派項目，**Then** 所有登入者都能查看。
2. **Given** 最新版本，**When** 修改項目，**Then** 內容與 History 在同一交易完成。
3. **Given** 過期版本，**When** 修改項目，**Then** 回傳衝突且不新增 History。
4. **Given** 有效項目，**When** 軟刪除，**Then** 一般查詢不可讀但 History 與個人狀態保留。

---

### User Story 4 - 管理使用者與權限（Priority: P3）

管理者能管理使用者、角色、功能與配置，停用或權限變更立即影響後續請求。

**Independent Test**: Admin 完成全部管理；Manager 只能管理使用者；Worker 無管理權限。

**Acceptance Scenarios**:

1. **Given** Admin，**When** 管理角色與功能，**Then** 新配置套用於下一個請求。
2. **Given** Manager，**When** 管理使用者，**Then** 成功；管理角色或功能則被拒絕。
3. **Given** 已停用帳號，**When** 使用既有登入憑證呼叫 API，**Then** 請求被拒絕。

---

### User Story 5 - 透過 Swagger 試用 API（Priority: P3）

開發者能在開發環境使用 Swagger 完成 CSRF、登入及受保護 API 操作，不需手動複製 Token。

**Independent Test**: 開啟 Swagger 後登入、查詢身分並建立一筆 Work Item，Cookie 與 CSRF 自動帶入。

**Acceptance Scenarios**:

1. **Given** Development，**When** 開啟 Swagger，**Then** UI 與 OpenAPI JSON 可用且自動取得 CSRF Cookie。
2. **Given** Swagger 已登入，**When** 執行 unsafe endpoint，**Then** 自動攜帶 Cookie 與 CSRF Header。
3. **Given** Production 預設設定，**When** 存取 Swagger，**Then** UI 與 JSON 不公開。

### Edge Cases

- 批次確認包含重複、不存在或已刪除的識別碼。
- Refresh Token 過期、撤銷或被重播。
- 指派使用者不存在或已停用。
- 同一 Work Item 被兩位管理者同時修改。
- Email、登入名稱、Role Code 或 Function Code 重複。
- Description 很長、Request Body 超過限制或分頁參數超界。
- unsafe request 缺少或提供錯誤 CSRF Token。

## Requirements

### Functional Requirements

- **FR-001**: 系統必須以帳號密碼驗證使用者，並支援登入、刷新、登出與目前身分查詢。
- **FR-002**: 系統必須隔離每位使用者對 Work Item 的確認狀態。
- **FR-003**: 所有登入使用者必須能查看全部未軟刪除 Work Item。
- **FR-004**: 指派使用者為選填管理資訊，不得限制查看或確認。
- **FR-005**: 系統必須支援單筆確認、撤銷及最多 100 筆的原子批次確認。
- **FR-006**: 管理者必須能新增、修改、指派、取消指派及軟刪除 Work Item。
- **FR-007**: 成功的 Work Item CRUD 必須在同一交易保存修改後完整快照。
- **FR-008**: 系統必須以版本值防止並行修改互相覆蓋。
- **FR-009**: 系統必須支援使用者、角色、功能及其關係的管理與啟停。
- **FR-010**: 權限必須由使用者所有啟用角色的啟用功能聯集決定。
- **FR-011**: 所有狀態變更請求必須驗證 CSRF，且身分只能來自伺服器驗證的憑證。
- **FR-012**: Refresh Token 必須輪替，重播時撤銷整個 Token Family。
- **FR-013**: API 錯誤必須使用一致且不洩漏敏感資訊的格式。
- **FR-014**: Development 必須提供可操作的 API 文件，Production 預設不得公開。
- **FR-015**: 系統必須提供健康檢查與可重複建立資料庫的升級流程。

### Key Entities

- **User／Account**: 人員資料與一對一登入帳號。
- **Role／Function**: 多角色及功能型授權。
- **Refresh Token**: 可輪替、可撤銷的登入工作階段。
- **Work Item**: 所有人可見、可選擇指派、可軟刪除的工作項目。
- **User Work Item State**: 使用者與項目的個人確認狀態。
- **Work Item History**: 管理端成功 CRUD 後的完整快照。

## Success Criteria

### Measurable Outcomes

- **SC-001**: 使用者可在一次登入流程後查看 Work Item 列表與詳情。
- **SC-002**: 100% 的跨使用者確認測試都維持狀態隔離。
- **SC-003**: 批次確認在任何錯誤下都不產生部分成功。
- **SC-004**: 100% 成功的管理端 CRUD 都有一筆對應 History，失敗操作則沒有。
- **SC-005**: 開發者可在 Swagger 不手動複製 Token 的情況下完成至少一個受保護寫入操作。
- **SC-006**: 所有必要 Unit、Integration、Workflow 情境及全棧健康檢查均通過。

## Assumptions

- 前端為獨立專案，本功能只提供後端 API。
- Description 為長文字，單一 Request Body 上限 1 MiB。
- 一筆 Work Item 最多一位指派者；不建立多指派。
- 沒有個人狀態資料列時視為 Pending；不建立個人狀態歷程。
- Production 不建立固定弱密碼，所有 Secret 由外部設定提供。
