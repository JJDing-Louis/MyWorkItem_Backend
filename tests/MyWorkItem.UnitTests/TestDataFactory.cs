using Bogus;
using MyWorkItem.Application;
using MyWorkItem.Domain;

namespace MyWorkItem.UnitTests;

internal static class TestDataFactory
{
    private static readonly Faker Faker = new("zh_TW");

    public static AccessProfile AccessProfile() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Faker.Internet.UserName(),
        Faker.Name.FullName(),
        "hashed-password",
        true,
        [RoleNames.User],
        [PermissionCodes.WorkItemsRead, PermissionCodes.WorkItemsConfirm]);

    public static WorkItemRecord WorkItem(Guid? id = null, bool confirmed = false) => new(
        id ?? Guid.NewGuid(),
        Faker.Lorem.Sentence(4),
        Faker.Lorem.Paragraph(),
        Guid.NewGuid(),
        DateTimeOffset.UtcNow.AddDays(-1),
        DateTimeOffset.UtcNow,
        Faker.Random.Bytes(8),
        confirmed,
        confirmed ? DateTimeOffset.UtcNow : null);
}
