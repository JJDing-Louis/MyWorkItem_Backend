CREATE TABLE UserRoles
(
    UserId uniqueidentifier NOT NULL,
    RoleId uniqueidentifier NOT NULL,
    IsEnabled bit NOT NULL CONSTRAINT DF_UserRoles_IsEnabled DEFAULT 1,
    CONSTRAINT PK_UserRoles PRIMARY KEY (UserId, RoleId),
    CONSTRAINT FK_UserRoles_Users FOREIGN KEY (UserId) REFERENCES Users(UserId),
    CONSTRAINT FK_UserRoles_Roles FOREIGN KEY (RoleId) REFERENCES Roles(RoleId)
);
GO

INSERT INTO UserRoles (UserId, RoleId, IsEnabled)
SELECT u.UserId, ar.RoleId, 1
FROM AccountRoles ar
INNER JOIN Users u ON u.AccountId = ar.AccountId;

ALTER TABLE RoleFunctions
ADD IsEnabled bit NOT NULL CONSTRAINT DF_RoleFunctions_IsEnabled DEFAULT 1 WITH VALUES;

CREATE TABLE WorkItemStatuses
(
    WorkItemStatusId uniqueidentifier NOT NULL CONSTRAINT PK_WorkItemStatuses PRIMARY KEY,
    Code nvarchar(100) NOT NULL,
    Name nvarchar(200) NOT NULL,
    IsEnabled bit NOT NULL CONSTRAINT DF_WorkItemStatuses_IsEnabled DEFAULT 1,
    CONSTRAINT UQ_WorkItemStatuses_Code UNIQUE (Code)
);
GO

INSERT INTO WorkItemStatuses (WorkItemStatusId, Code, Name, IsEnabled)
VALUES
    (NEWID(), N'Active', N'進行中', 1),
    (NEWID(), N'Closed', N'已結束', 1);

ALTER TABLE WorkItems
ADD CreatedUserId uniqueidentifier NULL,
    AssignedUserId uniqueidentifier NULL,
    WorkItemStatusId uniqueidentifier NULL,
    DeletedByUserId uniqueidentifier NULL;
GO

UPDATE w
SET CreatedUserId = creator.UserId,
    DeletedByUserId = deleter.UserId,
    WorkItemStatusId = (SELECT WorkItemStatusId FROM WorkItemStatuses WHERE Code = N'Active')
FROM WorkItems w
INNER JOIN Users creator ON creator.AccountId = w.CreatedBy
LEFT JOIN Users deleter ON deleter.AccountId = w.DeletedBy;

ALTER TABLE WorkItems ALTER COLUMN CreatedUserId uniqueidentifier NOT NULL;
ALTER TABLE WorkItems ALTER COLUMN WorkItemStatusId uniqueidentifier NOT NULL;

ALTER TABLE WorkItems DROP CONSTRAINT FK_WorkItems_CreatedBy;
ALTER TABLE WorkItems DROP CONSTRAINT FK_WorkItems_DeletedBy;
ALTER TABLE WorkItems DROP COLUMN CreatedBy;
ALTER TABLE WorkItems DROP COLUMN DeletedBy;

ALTER TABLE WorkItems
ADD CONSTRAINT FK_WorkItems_CreatedUser FOREIGN KEY (CreatedUserId) REFERENCES Users(UserId),
    CONSTRAINT FK_WorkItems_AssignedUser FOREIGN KEY (AssignedUserId) REFERENCES Users(UserId),
    CONSTRAINT FK_WorkItems_Status FOREIGN KEY (WorkItemStatusId) REFERENCES WorkItemStatuses(WorkItemStatusId),
    CONSTRAINT FK_WorkItems_DeletedByUser FOREIGN KEY (DeletedByUserId) REFERENCES Users(UserId);

CREATE INDEX IX_WorkItems_AssignedUserStatus
ON WorkItems(AssignedUserId, WorkItemStatusId, DeletedAt, CreatedAt DESC);

ALTER TABLE Accounts ADD UserId uniqueidentifier NULL;
GO

UPDATE a
SET UserId = u.UserId
FROM Accounts a
INNER JOIN Users u ON u.AccountId = a.AccountId;

ALTER TABLE Accounts ALTER COLUMN UserId uniqueidentifier NOT NULL;
ALTER TABLE Users DROP CONSTRAINT FK_Users_Accounts;
ALTER TABLE Users DROP CONSTRAINT UQ_Users_AccountId;
ALTER TABLE Users DROP COLUMN AccountId;

ALTER TABLE Accounts
ADD CONSTRAINT UQ_Accounts_UserId UNIQUE (UserId),
    CONSTRAINT FK_Accounts_Users FOREIGN KEY (UserId) REFERENCES Users(UserId);

DROP TABLE AccountRoles;
