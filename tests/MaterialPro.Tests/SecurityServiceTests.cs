using MaterialPro.Application;
using MaterialPro.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MaterialPro.Tests;

public sealed class SecurityServiceTests
{
    [Fact]
    public void Login_Locks_User_After_Five_Failed_Attempts()
    {
        using var db = CreateDbContext();
        var security = new SecurityService(db);
        var auth = new AuthService(new EfUserRepository(db), new Sha256PasswordHasher(), security);
        auth.CreateAdmin("Admin", "admin", "admin@test.local", "123456");

        for (var i = 0; i < 5; i++)
        {
            var result = auth.Login(new LoginRequest("admin", "wrong"));
            Assert.False(result.Success);
        }

        var user = db.Users.First();
        Assert.NotNull(user.LockedUntilUtc);
        Assert.Equal(0, user.FailedLoginCount);
        Assert.Equal(5, db.SecurityLoginAttempts.Count());
    }

    [Fact]
    public void ChangePassword_Updates_Hash_And_Timestamp()
    {
        using var db = CreateDbContext();
        var security = new SecurityService(db);
        var hasher = new Sha256PasswordHasher();
        var auth = new AuthService(new EfUserRepository(db), hasher, security);
        var user = auth.CreateAdmin("Admin", "admin", "admin@test.local", "123456");
        var passwordService = new UserSecurityService(db, hasher, security);

        passwordService.ChangePassword(new ChangePasswordRequest(user.Id, "123456", "654321"));

        var updated = db.Users.First(x => x.Id == user.Id);
        Assert.True(hasher.Verify("654321", updated.PasswordSalt, updated.PasswordHash));
        Assert.NotNull(updated.PasswordChangedAtUtc);
    }

    private static MaterialProDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MaterialProDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new MaterialProDbContext(options);
    }
}
