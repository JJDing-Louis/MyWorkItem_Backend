CREATE TABLE dbo.Users
(
    UserId uniqueidentifier NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
    Name nvarchar(200) NOT NULL,
    Email nvarchar(320) NULL,
    NormalizedEmail nvarchar(320) NULL,
    Remark nvarchar(1000) NULL,
    CreatedAt datetimeoffset(7) NOT NULL,
    UpdatedAt datetimeoffset(7) NOT NULL
);

CREATE UNIQUE INDEX UX_Users_NormalizedEmail
    ON dbo.Users (NormalizedEmail)
    WHERE NormalizedEmail IS NOT NULL;

CREATE TABLE dbo.Accounts
(
    AccountId uniqueidentifier NOT NULL CONSTRAINT PK_Accounts PRIMARY KEY,
    UserId uniqueidentifier NOT NULL,
    LoginName nvarchar(100) NOT NULL,
    NormalizedLoginName nvarchar(100) NOT NULL,
    PasswordHash nvarchar(500) NOT NULL,
    IsEnabled bit NOT NULL CONSTRAINT DF_Accounts_IsEnabled DEFAULT (1),
    CreatedAt datetimeoffset(7) NOT NULL,
    UpdatedAt datetimeoffset(7) NOT NULL,
    CONSTRAINT FK_Accounts_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId),
    CONSTRAINT UX_Accounts_UserId UNIQUE (UserId),
    CONSTRAINT UX_Accounts_NormalizedLoginName UNIQUE (NormalizedLoginName)
);

CREATE TABLE dbo.Roles
(
    RoleId uniqueidentifier NOT NULL CONSTRAINT PK_Roles PRIMARY KEY,
    Code nvarchar(100) NOT NULL,
    Name nvarchar(200) NOT NULL,
    Description nvarchar(1000) NULL,
    IsEnabled bit NOT NULL CONSTRAINT DF_Roles_IsEnabled DEFAULT (1),
    CreatedAt datetimeoffset(7) NOT NULL,
    UpdatedAt datetimeoffset(7) NOT NULL,
    CONSTRAINT UX_Roles_Code UNIQUE (Code)
);

CREATE TABLE dbo.Functions
(
    FunctionId uniqueidentifier NOT NULL CONSTRAINT PK_Functions PRIMARY KEY,
    Code nvarchar(100) NOT NULL,
    Name nvarchar(200) NOT NULL,
    Description nvarchar(1000) NULL,
    IsEnabled bit NOT NULL CONSTRAINT DF_Functions_IsEnabled DEFAULT (1),
    CreatedAt datetimeoffset(7) NOT NULL,
    UpdatedAt datetimeoffset(7) NOT NULL,
    CONSTRAINT UX_Functions_Code UNIQUE (Code)
);

CREATE TABLE dbo.UserRoles
(
    UserId uniqueidentifier NOT NULL,
    RoleId uniqueidentifier NOT NULL,
    IsEnabled bit NOT NULL CONSTRAINT DF_UserRoles_IsEnabled DEFAULT (1),
    AssignedAt datetimeoffset(7) NOT NULL,
    AssignedByUserId uniqueidentifier NULL,
    CONSTRAINT PK_UserRoles PRIMARY KEY (UserId, RoleId),
    CONSTRAINT FK_UserRoles_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId),
    CONSTRAINT FK_UserRoles_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles (RoleId),
    CONSTRAINT FK_UserRoles_AssignedBy FOREIGN KEY (AssignedByUserId) REFERENCES dbo.Users (UserId)
);

CREATE INDEX IX_UserRoles_RoleId ON dbo.UserRoles (RoleId, IsEnabled);

CREATE TABLE dbo.RoleFunctions
(
    RoleId uniqueidentifier NOT NULL,
    FunctionId uniqueidentifier NOT NULL,
    IsEnabled bit NOT NULL CONSTRAINT DF_RoleFunctions_IsEnabled DEFAULT (1),
    UpdatedAt datetimeoffset(7) NOT NULL,
    UpdatedByUserId uniqueidentifier NULL,
    CONSTRAINT PK_RoleFunctions PRIMARY KEY (RoleId, FunctionId),
    CONSTRAINT FK_RoleFunctions_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles (RoleId),
    CONSTRAINT FK_RoleFunctions_Functions FOREIGN KEY (FunctionId) REFERENCES dbo.Functions (FunctionId),
    CONSTRAINT FK_RoleFunctions_UpdatedBy FOREIGN KEY (UpdatedByUserId) REFERENCES dbo.Users (UserId)
);

CREATE INDEX IX_RoleFunctions_FunctionId ON dbo.RoleFunctions (FunctionId, IsEnabled);

CREATE TABLE dbo.RefreshTokens
(
    RefreshTokenId uniqueidentifier NOT NULL CONSTRAINT PK_RefreshTokens PRIMARY KEY,
    AccountId uniqueidentifier NOT NULL,
    TokenHash varbinary(32) NOT NULL,
    FamilyId uniqueidentifier NOT NULL,
    ExpiresAt datetimeoffset(7) NOT NULL,
    CreatedAt datetimeoffset(7) NOT NULL,
    RevokedAt datetimeoffset(7) NULL,
    ReplacedByTokenId uniqueidentifier NULL,
    RevocationReason nvarchar(200) NULL,
    CONSTRAINT FK_RefreshTokens_Accounts FOREIGN KEY (AccountId) REFERENCES dbo.Accounts (AccountId),
    CONSTRAINT FK_RefreshTokens_ReplacedBy FOREIGN KEY (ReplacedByTokenId) REFERENCES dbo.RefreshTokens (RefreshTokenId),
    CONSTRAINT UX_RefreshTokens_TokenHash UNIQUE (TokenHash)
);

CREATE INDEX IX_RefreshTokens_AccountFamily ON dbo.RefreshTokens (AccountId, FamilyId);
CREATE INDEX IX_RefreshTokens_FamilyId ON dbo.RefreshTokens (FamilyId);

CREATE TABLE dbo.WorkItemStatuses
(
    WorkItemStatusId uniqueidentifier NOT NULL CONSTRAINT PK_WorkItemStatuses PRIMARY KEY,
    Code nvarchar(50) NOT NULL,
    Name nvarchar(100) NOT NULL,
    Description nvarchar(500) NULL,
    IsEnabled bit NOT NULL CONSTRAINT DF_WorkItemStatuses_IsEnabled DEFAULT (1),
    CONSTRAINT UX_WorkItemStatuses_Code UNIQUE (Code)
);

CREATE TABLE dbo.Actions
(
    ActionId uniqueidentifier NOT NULL CONSTRAINT PK_Actions PRIMARY KEY,
    Code nvarchar(50) NOT NULL,
    Name nvarchar(100) NOT NULL,
    Description nvarchar(500) NULL,
    IsEnabled bit NOT NULL CONSTRAINT DF_Actions_IsEnabled DEFAULT (1),
    CONSTRAINT UX_Actions_Code UNIQUE (Code)
);

CREATE TABLE dbo.WorkItems
(
    WorkItemId uniqueidentifier NOT NULL CONSTRAINT PK_WorkItems PRIMARY KEY,
    Title nvarchar(200) NOT NULL,
    Description nvarchar(max) NULL,
    CreatedByUserId uniqueidentifier NOT NULL,
    AssignedUserId uniqueidentifier NULL,
    CreatedAt datetimeoffset(7) NOT NULL,
    UpdatedAt datetimeoffset(7) NOT NULL,
    DeletedAt datetimeoffset(7) NULL,
    DeletedByUserId uniqueidentifier NULL,
    RowVersion rowversion NOT NULL,
    CONSTRAINT FK_WorkItems_CreatedBy FOREIGN KEY (CreatedByUserId) REFERENCES dbo.Users (UserId),
    CONSTRAINT FK_WorkItems_AssignedUser FOREIGN KEY (AssignedUserId) REFERENCES dbo.Users (UserId),
    CONSTRAINT FK_WorkItems_DeletedBy FOREIGN KEY (DeletedByUserId) REFERENCES dbo.Users (UserId),
    CONSTRAINT CK_WorkItems_DeletedPair CHECK
    (
        (DeletedAt IS NULL AND DeletedByUserId IS NULL)
        OR (DeletedAt IS NOT NULL AND DeletedByUserId IS NOT NULL)
    )
);

CREATE INDEX IX_WorkItems_Active_CreatedAt
    ON dbo.WorkItems (CreatedAt DESC)
    INCLUDE (Title, AssignedUserId, UpdatedAt)
    WHERE DeletedAt IS NULL;

CREATE INDEX IX_WorkItems_AssignedUserId
    ON dbo.WorkItems (AssignedUserId, CreatedAt DESC)
    WHERE DeletedAt IS NULL;

CREATE TABLE dbo.UserWorkItemStates
(
    UserId uniqueidentifier NOT NULL,
    WorkItemId uniqueidentifier NOT NULL,
    WorkItemStatusId uniqueidentifier NOT NULL,
    ConfirmedAt datetimeoffset(7) NULL,
    UpdatedAt datetimeoffset(7) NOT NULL,
    CONSTRAINT PK_UserWorkItemStates PRIMARY KEY (UserId, WorkItemId),
    CONSTRAINT FK_UserWorkItemStates_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId),
    CONSTRAINT FK_UserWorkItemStates_WorkItems FOREIGN KEY (WorkItemId) REFERENCES dbo.WorkItems (WorkItemId),
    CONSTRAINT FK_UserWorkItemStates_Statuses FOREIGN KEY (WorkItemStatusId) REFERENCES dbo.WorkItemStatuses (WorkItemStatusId)
);

CREATE INDEX IX_UserWorkItemStates_WorkItemId
    ON dbo.UserWorkItemStates (WorkItemId, UserId);

CREATE TABLE dbo.WorkItemHistories
(
    HistoryId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_WorkItemHistories PRIMARY KEY,
    WorkItemId uniqueidentifier NOT NULL,
    ActionId uniqueidentifier NOT NULL,
    ChangedByUserId uniqueidentifier NOT NULL,
    ChangedAt datetimeoffset(7) NOT NULL,
    SnapshotTitle nvarchar(200) NOT NULL,
    SnapshotDescription nvarchar(max) NULL,
    SnapshotCreatedByUserId uniqueidentifier NOT NULL,
    SnapshotAssignedUserId uniqueidentifier NULL,
    SnapshotCreatedAt datetimeoffset(7) NOT NULL,
    SnapshotUpdatedAt datetimeoffset(7) NOT NULL,
    SnapshotDeletedAt datetimeoffset(7) NULL,
    SnapshotDeletedByUserId uniqueidentifier NULL,
    SourceRowVersion binary(8) NOT NULL,
    CONSTRAINT FK_WorkItemHistories_WorkItems FOREIGN KEY (WorkItemId) REFERENCES dbo.WorkItems (WorkItemId),
    CONSTRAINT FK_WorkItemHistories_Actions FOREIGN KEY (ActionId) REFERENCES dbo.Actions (ActionId),
    CONSTRAINT FK_WorkItemHistories_ChangedBy FOREIGN KEY (ChangedByUserId) REFERENCES dbo.Users (UserId)
);

CREATE INDEX IX_WorkItemHistories_WorkItemChangedAt
    ON dbo.WorkItemHistories (WorkItemId, ChangedAt DESC, HistoryId DESC);
