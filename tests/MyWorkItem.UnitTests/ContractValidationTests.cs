using System.ComponentModel.DataAnnotations;
using Bogus;
using FluentAssertions;
using MyWorkItem.Application.Contracts;

namespace MyWorkItem.UnitTests;

public sealed class ContractValidationTests
{
    [Test]
    public void LoginRequest_短密碼應驗證失敗()
    {
        var request = new LoginRequest(new Faker().Internet.UserName(), "TooShort1!");
        Validate(request).Should().Contain(result => result.MemberNames.Contains(nameof(LoginRequest.Password)));
    }

    [Test]
    public void BatchConfirmation_超過一百筆應驗證失敗()
    {
        var request = new BatchConfirmationRequest(Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()).ToArray());
        Validate(request).Should().ContainSingle();
    }

    [TestCase(200, true)]
    [TestCase(201, false)]
    public void CreateWorkItem_Title長度限制應為兩百字(int length, bool valid)
    {
        var request = new CreateWorkItemRequest(new string('工', length), null, null);
        Validate(request).Any().Should().Be(!valid);
    }

    private static IReadOnlyCollection<System.ComponentModel.DataAnnotations.ValidationResult> Validate(object instance)
    {
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var constructor = instance.GetType().GetConstructors().Single();
        foreach (var parameter in constructor.GetParameters())
        {
            var property = instance.GetType().GetProperty(parameter.Name!,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase)!;
            var value = property.GetValue(instance);
            var context = new ValidationContext(instance) { MemberName = property.Name };
            foreach (var attribute in parameter.GetCustomAttributes(typeof(ValidationAttribute), true).Cast<ValidationAttribute>())
            {
                var result = attribute.GetValidationResult(value, context);
                if (result is not null)
                {
                    results.Add(result);
                }
            }
        }
        return results;
    }
}
