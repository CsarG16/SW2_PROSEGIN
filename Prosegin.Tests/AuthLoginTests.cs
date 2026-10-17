using Prosegin.Data;
using Xunit;

namespace Prosegin.Tests;

public class AuthLoginTests
{
    [Fact]
    public void PasswordHashHelper_Verify_HaceMatchConPasswordOriginal()
    {
        var password = "test-password";
        var hash = PasswordHashHelper.Hash(password);

        Assert.NotEqual(password, hash);
        Assert.True(PasswordHashHelper.Verify(password, hash));
        Assert.False(PasswordHashHelper.Verify("otro-password", hash));
    }
}
