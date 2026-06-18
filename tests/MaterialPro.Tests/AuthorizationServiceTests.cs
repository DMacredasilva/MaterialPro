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

    [Fact]
    public void User_EffectiveModules_Uses_Custom_Module_List()
    {
        var user = new AppUser
        {
            Role = UserRole.Cashier,
            AllowedModules = $"{SystemModules.Pdv},{SystemModules.Cash}"
        };

        Assert.Equal([SystemModules.Pdv, SystemModules.Cash], user.EffectiveModules());
    }

    [Fact]
    public void User_EffectiveModules_Falls_Back_To_Role_Defaults()
    {
        var user = new AppUser { Role = UserRole.Stock };

        Assert.Contains(SystemModules.Stock, user.EffectiveModules());
        Assert.DoesNotContain(SystemModules.Cash, user.EffectiveModules());
    }
}
