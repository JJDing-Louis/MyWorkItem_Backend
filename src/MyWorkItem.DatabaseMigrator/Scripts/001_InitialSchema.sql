CREATE TABLE Accounts
(
    AccountId uniqueidentifier NOT NULL CONSTRAINT PK_Accounts PRIMARY KEY,
    UserName nvarchar(100) NOT NULL,
    PasswordHash nvarchar(500) NOT NULL,
    IsEnabled bit NOT NULL CONSTRAINT DF_Accounts_IsEnabled DEFAULT 1,
    CreatedAt datetimeoffset(7) NOT NULL,
    UpdatedAt datetimeoffset(7) NOT NULL,
    CONSTRAINT UQ_Accounts_UserName UNIQUE (UserName)
);

CREATE TABLE Users
(
    UserId uniqueidentifier NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
    AccountId uniqueidentifier NOT NULL,
    Name nvarchar(200) NOT NULL,
    Email nvarchar(320) NULL,
    Remark nvarchar(1000) NULL,
    CONSTRAINT FK_Users_Accounts FOREIGN KEY (AccountId) REFERENCES Accounts(AccountId),
    CONSTRAINT UQ_Users_AccountId UNIQUE (AccountId)
);

CREATE UNIQUE INDEX UX_Users_Email ON Users(Email) WHERE Email IS NOT NULL;

CREATE TABLE Roles
(
    RoleId uniqueidentifier NOT NULL CONSTRAINT PK_Roles PRIMARY KEY,
    Code nvarchar(100) NOT NULL,
    Name nvarchar(200) NOT NULL,
    IsEnabled bit NOT NULL CONSTRAINT DF_Roles_IsEnabled DEFAULT 1,
    CONSTRAINT UQ_Roles_Code UNIQUE (Code)
);

CREATE TABLE [Functions]
(
    FunctionId uniqueidentifier NOT NULL CONSTRAINT PK_Functions PRIMARY KEY,
    Code nvarchar(100) NOT NULL,
    Name nvarchar(200) NOT NULL,
    IsEnabled bit NOT NULL CONSTRAINT DF_Functions_IsEnabled DEFAULT 1,
    CONSTRAINT UQ_Functions_Code UNIQUE (Code)
);

CREATE TABLE AccountRoles
(
    AccountId uniqueidentifier NOT NULL,
    RoleId uniqueidentifier NOT NULL,
    CONSTRAINT PK_AccountRoles PRIMARY KEY (AccountId, RoleId),
    CONSTRAINT FK_AccountRoles_Accounts FOREIGN KEY (AccountId) REFERENCES Accounts(AccountId),
    CONSTRAINT FK_AccountRoles_Roles FOREIGN KEY (RoleId) REFERENCES Roles(RoleId)
);

CREATE TABLE RoleFunctions
(
    RoleId uniqueidentifier NOT NULL,
    FunctionId uniqueidentifier NOT NULL,
    CONSTRAINT PK_RoleFunctions PRIMARY KEY (RoleId, FunctionId),
    CONSTRAINT FK_RoleFunctions_Roles FOREIGN KEY (RoleId) REFERENCES Roles(RoleId),
    CONSTRAINT FK_RoleFunctions_Functions FOREIGN KEY (FunctionId) REFERENCES [Functions](FunctionId)
);

CREATE TABLE RefreshTokens
(
    RefreshTokenId uniqueidentifier NOT NULL CONSTRAINT PK_RefreshTokens PRIMARY KEY,
    AccountId uniqueidentifier NOT NULL,
    TokenHash char(64) NOT NULL,
    TokenFamily uniqueidentifier NOT NULL,
    ExpiresAt datetimeoffset(7) NOT NULL,
    CreatedAt datetimeoffset(7) NOT NULL,
    RevokedAt datetimeoffset(7) NULL,
    ReplacedByTokenId uniqueidentifier NULL,
    CONSTRAINT FK_RefreshTokens_Accounts FOREIGN KEY (AccountId) REFERENCES Accounts(AccountId),
    CONSTRAINT FK_RefreshTokens_ReplacedBy FOREIGN KEY (ReplacedByTokenId) REFERENCES RefreshTokens(RefreshTokenId),
    CONSTRAINT UQ_RefreshTokens_TokenHash UNIQUE (TokenHash)
);

CREATE INDEX IX_RefreshTokens_Family ON RefreshTokens(TokenFamily);

CREATE TABLE WorkItems
(
    WorkItemId uniqueidentifier NOT NULL CONSTRAINT PK_WorkItems PRIMARY KEY,
    Title nvarchar(200) NOT NULL,
    Description nvarchar(4000) NULL,
    CreatedBy uniqueidentifier NOT NULL,
    CreatedAt datetimeoffset(7) NOT NULL,
    UpdatedAt datetimeoffset(7) NOT NULL,
    DeletedAt datetimeoffset(7) NULL,
    DeletedBy uniqueidentifier NULL,
    RowVersion rowversion NOT NULL,
    CONSTRAINT FK_WorkItems_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES Accounts(AccountId),
    CONSTRAINT FK_WorkItems_DeletedBy FOREIGN KEY (DeletedBy) REFERENCES Accounts(AccountId)
);

CREATE INDEX IX_WorkItems_ActiveCreatedAt ON WorkItems(DeletedAt, CreatedAt DESC);

CREATE TABLE UserWorkItemStates
(
    UserId uniqueidentifier NOT NULL,
    WorkItemId uniqueidentifier NOT NULL,
    IsConfirmed bit NOT NULL,
    ConfirmedAt datetimeoffset(7) NULL,
    UpdatedAt datetimeoffset(7) NOT NULL,
    CONSTRAINT PK_UserWorkItemStates PRIMARY KEY (UserId, WorkItemId),
    CONSTRAINT FK_UserWorkItemStates_Users FOREIGN KEY (UserId) REFERENCES Users(UserId),
    CONSTRAINT FK_UserWorkItemStates_WorkItems FOREIGN KEY (WorkItemId) REFERENCES WorkItems(WorkItemId)
);
