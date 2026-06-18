using MaterialPro.Application;
using MaterialPro.Domain;
using MaterialPro.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MaterialPro.Tests;

public sealed class AuthServiceTests
{
    [Fact]
    public void Login_ReturnsSuccess_WhenPasswordMatches()
    {
        var db = CreateDbContext();
        var repo = new EfUserRepository(db);
        var hasher = new Sha256PasswordHasher();
        var auth = new AuthService(repo, hasher);
        auth.CreateAdmin("Admin", "admin", "admin@test.local", "123456");

        var result = auth.Login(new LoginRequest("admin", "123456"));

        Assert.True(result.Success);
        Assert.NotNull(result.User);
        Assert.Equal(UserRole.Admin, result.User!.Role);
    }

    [Fact]
    public void Login_IsCaseInsensitive_ForUsername()
    {
        var db = CreateDbContext();
        var repo = new EfUserRepository(db);
        var hasher = new Sha256PasswordHasher();
        var auth = new AuthService(repo, hasher);
        auth.CreateAdmin("Admin", "admin", "admin@test.local", "123456");

        var result = auth.Login(new LoginRequest("ADMIN", "123456"));

        Assert.True(result.Success);
    }

    private static MaterialProDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MaterialProDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new MaterialProDbContext(options);
    }
}
