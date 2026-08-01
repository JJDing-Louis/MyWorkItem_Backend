namespace MyWorkItem.Application;

public static class PermissionCodes
{
    public const string WorkItemsRead = "WorkItems.Read";
    public const string WorkItemsConfirm = "WorkItems.Confirm";
    public const string WorkItemsManage = "WorkItems.Manage";
    public const string UsersManage = "Users.Manage";
    public const string RolesManage = "Roles.Manage";
    public const string FunctionsManage = "Functions.Manage";

    public static readonly string[] All =
    [
        WorkItemsRead,
        WorkItemsConfirm,
        WorkItemsManage,
        UsersManage,
        RolesManage,
        FunctionsManage
    ];
}

public static class RoleNames
{
    public const string Admin = "Admin";
    public const string User = "User";
    public const string BackOffice = "BackOffice";
}
