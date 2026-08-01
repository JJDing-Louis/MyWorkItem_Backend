using FluentAssertions;
using MyWorkItem.Infrastructure;

namespace MyWorkItem.UnitTests;

public sealed class PasswordServiceTests
{
    [Test]
    public void Hash_同一密碼可驗證且不保存明文()
    {
        var service = new PasswordService();

        var hash = service.Hash("A-valid-password-123");

        hash.Should().NotBe("A-valid-password-123");
        service.Verify(hash, "A-valid-password-123").Should().BeTrue();
        service.Verify(hash, "wrong-password").Should().BeFalse();
    }
}
