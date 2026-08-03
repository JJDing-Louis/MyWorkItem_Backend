DECLARE @Now datetimeoffset(7) = TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00');

IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Code = N'Admin')
    INSERT dbo.Roles (RoleId, Code, Name, Description, IsEnabled, CreatedAt, UpdatedAt)
    VALUES ('11111111-1111-1111-1111-111111111111', N'Admin', N'Admin', N'系統管理員', 1, @Now, @Now);
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Code = N'Manager')
    INSERT dbo.Roles (RoleId, Code, Name, Description, IsEnabled, CreatedAt, UpdatedAt)
    VALUES ('11111111-1111-1111-1111-111111111112', N'Manager', N'Manager', N'後台管理員', 1, @Now, @Now);
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Code = N'Worker')
    INSERT dbo.Roles (RoleId, Code, Name, Description, IsEnabled, CreatedAt, UpdatedAt)
    VALUES ('11111111-1111-1111-1111-111111111113', N'Worker', N'Worker', N'一般使用者', 1, @Now, @Now);

IF NOT EXISTS (SELECT 1 FROM dbo.Functions WHERE Code = N'WorkItems.Read')
    INSERT dbo.Functions VALUES ('22222222-2222-2222-2222-222222222221', N'WorkItems.Read', N'讀取 Work Item', NULL, 1, @Now, @Now);
IF NOT EXISTS (SELECT 1 FROM dbo.Functions WHERE Code = N'WorkItems.Confirm')
    INSERT dbo.Functions VALUES ('22222222-2222-2222-2222-222222222222', N'WorkItems.Confirm', N'確認 Work Item', NULL, 1, @Now, @Now);
IF NOT EXISTS (SELECT 1 FROM dbo.Functions WHERE Code = N'WorkItems.Manage')
    INSERT dbo.Functions VALUES ('22222222-2222-2222-2222-222222222223', N'WorkItems.Manage', N'管理 Work Item', NULL, 1, @Now, @Now);
IF NOT EXISTS (SELECT 1 FROM dbo.Functions WHERE Code = N'Users.Manage')
    INSERT dbo.Functions VALUES ('22222222-2222-2222-2222-222222222224', N'Users.Manage', N'管理使用者', NULL, 1, @Now, @Now);
IF NOT EXISTS (SELECT 1 FROM dbo.Functions WHERE Code = N'Roles.Manage')
    INSERT dbo.Functions VALUES ('22222222-2222-2222-2222-222222222225', N'Roles.Manage', N'管理角色', NULL, 1, @Now, @Now);
IF NOT EXISTS (SELECT 1 FROM dbo.Functions WHERE Code = N'Functions.Manage')
    INSERT dbo.Functions VALUES ('22222222-2222-2222-2222-222222222226', N'Functions.Manage', N'管理功能', NULL, 1, @Now, @Now);

IF NOT EXISTS (SELECT 1 FROM dbo.WorkItemStatuses WHERE Code = N'Pending')
    INSERT dbo.WorkItemStatuses VALUES ('33333333-3333-3333-3333-333333333331', N'Pending', N'Pending', N'待確認', 1);
IF NOT EXISTS (SELECT 1 FROM dbo.WorkItemStatuses WHERE Code = N'Confirm')
    INSERT dbo.WorkItemStatuses VALUES ('33333333-3333-3333-3333-333333333332', N'Confirm', N'Confirm', N'已確認', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Actions WHERE Code = N'INSERT')
    INSERT dbo.Actions VALUES ('44444444-4444-4444-4444-444444444441', N'INSERT', N'INSERT', N'新增', 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Actions WHERE Code = N'UPDATE')
    INSERT dbo.Actions VALUES ('44444444-4444-4444-4444-444444444442', N'UPDATE', N'UPDATE', N'更新', 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Actions WHERE Code = N'DELETE')
    INSERT dbo.Actions VALUES ('44444444-4444-4444-4444-444444444443', N'DELETE', N'DELETE', N'刪除', 1);

INSERT dbo.RoleFunctions (RoleId, FunctionId, IsEnabled, UpdatedAt, UpdatedByUserId)
SELECT r.RoleId, f.FunctionId, 1, @Now, NULL
FROM dbo.Roles r
CROSS JOIN dbo.Functions f
WHERE r.Code = N'Admin'
  AND NOT EXISTS (SELECT 1 FROM dbo.RoleFunctions rf WHERE rf.RoleId = r.RoleId AND rf.FunctionId = f.FunctionId);

INSERT dbo.RoleFunctions (RoleId, FunctionId, IsEnabled, UpdatedAt, UpdatedByUserId)
SELECT r.RoleId, f.FunctionId, 1, @Now, NULL
FROM dbo.Roles r
JOIN dbo.Functions f ON f.Code IN (N'WorkItems.Read', N'WorkItems.Confirm', N'WorkItems.Manage', N'Users.Manage')
WHERE r.Code = N'Manager'
  AND NOT EXISTS (SELECT 1 FROM dbo.RoleFunctions rf WHERE rf.RoleId = r.RoleId AND rf.FunctionId = f.FunctionId);

INSERT dbo.RoleFunctions (RoleId, FunctionId, IsEnabled, UpdatedAt, UpdatedByUserId)
SELECT r.RoleId, f.FunctionId, 1, @Now, NULL
FROM dbo.Roles r
JOIN dbo.Functions f ON f.Code IN (N'WorkItems.Read', N'WorkItems.Confirm')
WHERE r.Code = N'Worker'
  AND NOT EXISTS (SELECT 1 FROM dbo.RoleFunctions rf WHERE rf.RoleId = r.RoleId AND rf.FunctionId = f.FunctionId);
