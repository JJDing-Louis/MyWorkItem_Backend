using FluentAssertions;
using MyWorkItem.Application;
using NSubstitute;

namespace MyWorkItem.UnitTests;

public sealed class WorkItemServiceTests
{
    [Test]
    public async Task ListAsync_只把目前登入者UserId交給Repository()
    {
        var repository = Substitute.For<IWorkItemRepository>();
        var currentUserId = Guid.NewGuid();
        var item = TestDataFactory.WorkItem(confirmed: true);
        repository.ListAsync(currentUserId, 1, 20, null, true, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<MyWorkItem.Domain.WorkItemRecord>([item], 1, 20, 1));
        var service = new WorkItemService(repository);

        var result = await service.ListAsync(currentUserId, 1, 20, null, true, CancellationToken.None);

        result.Items.Should().ContainSingle().Which.IsConfirmed.Should().BeTrue();
        await repository.Received(1).ListAsync(currentUserId, 1, 20, null, true, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ConfirmBatchAsync_會移除重複WorkItemId並維持同一UserId()
    {
        var repository = Substitute.For<IWorkItemRepository>();
        var service = new WorkItemService(repository);
        var userId = Guid.NewGuid();
        var workItemId = Guid.NewGuid();

        await service.ConfirmBatchAsync([workItemId, workItemId], userId, CancellationToken.None);

        await repository.Received(1).ConfirmBatchAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids != null && ids.Count == 1 && ids.Single() == workItemId),
            userId,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateAsync_Version不是Base64時回報驗證錯誤()
    {
        var service = new WorkItemService(Substitute.For<IWorkItemRepository>());
        var request = new UpdateWorkItemRequest("標題", null, "not-base64");

        var action = () => service.UpdateAsync(Guid.NewGuid(), request, Guid.NewGuid(), CancellationToken.None);

        await action.Should().ThrowAsync<ValidationException>().WithMessage("*Version*");
    }
}
