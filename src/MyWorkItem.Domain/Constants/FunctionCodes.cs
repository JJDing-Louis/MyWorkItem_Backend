namespace MyWorkItem.Domain.Constants;

public static class FunctionCodes
{
    public const string WorkItemsRead = "WorkItems.Read";
    public const string WorkItemsConfirm = "WorkItems.Confirm";
    public const string WorkItemsManage = "WorkItems.Manage";
    public const string UsersManage = "Users.Manage";
    public const string RolesManage = "Roles.Manage";
    public const string FunctionsManage = "Functions.Manage";

    public static IReadOnlyCollection<string> All { get; } =
    [
        WorkItemsRead,
        WorkItemsConfirm,
        WorkItemsManage,
        UsersManage,
        RolesManage,
        FunctionsManage
    ];
}
