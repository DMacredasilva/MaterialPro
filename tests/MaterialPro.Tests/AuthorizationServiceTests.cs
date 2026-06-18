using MaterialPro.Application;
using MaterialPro.Domain;
using Xunit;

namespace MaterialPro.Tests;

public sealed class AuthorizationServiceTests
{
    [Fact]
    public void Admin_Has_All_Permissions()
    {
        var authz = new AuthorizationService();
        var user = new AppUser { Role = UserRole.Admin };

        Assert.True(authz.HasPermission(user, Permission.ManageUsers));
        Assert.True(authz.HasPermission(user, Permission.ViewReports));
    }
}
