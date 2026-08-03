namespace MyWorkItem.Infrastructure.Security;

public static class PasswordPolicy
{
    public static bool IsValid(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
        {
            return false;
        }

        var categories = 0;
        categories += password.Any(char.IsUpper) ? 1 : 0;
        categories += password.Any(char.IsLower) ? 1 : 0;
        categories += password.Any(char.IsDigit) ? 1 : 0;
        categories += password.Any(character => !char.IsLetterOrDigit(character)) ? 1 : 0;
        return categories >= 3;
    }
}
