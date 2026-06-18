using MaterialPro.Domain;

namespace MaterialPro.Application;

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly ISecurityService? _security;

    public AuthService(IUserRepository users, IPasswordHasher hasher, ISecurityService? security = null)
    {
        _users = users;
        _hasher = hasher;
        _security = security;
    }

    public AuthenticationResult Login(LoginRequest request)
    {
        var username = request.Username.Trim();
        var user = _users.FindByUsername(username) ?? _users.FindByEmail(username);
        if (user is null)
        {
            _security?.RecordLoginAttempt(new SecurityLoginAttemptRequest(username, null, false, "Usuario nao encontrado", Environment.MachineName, string.Empty));
            return new AuthenticationResult(false, "Usuário não encontrado.");
        }

        if (!user.IsActive)
        {
            _security?.RecordLoginAttempt(new SecurityLoginAttemptRequest(username, user.Id, false, "Usuario inativo", Environment.MachineName, string.Empty));
            return new AuthenticationResult(false, "Usuário inativo.");
        }

        if (user.LockedUntilUtc is not null && user.LockedUntilUtc > DateTime.UtcNow)
        {
            _security?.RecordLoginAttempt(new SecurityLoginAttemptRequest(username, user.Id, false, "Usuario bloqueado", Environment.MachineName, string.Empty));
            return new AuthenticationResult(false, "Usuário bloqueado temporariamente.");
        }

        if (!_hasher.Verify(request.Password, user.PasswordSalt, user.PasswordHash))
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= 5)
            {
                user.LockedUntilUtc = DateTime.UtcNow.AddMinutes(15);
                user.FailedLoginCount = 0;
                user.UpdatedAtUtc = DateTime.UtcNow;
                _security?.RecordAudit(new SecurityAuditRequest(user.Id, MaterialPro.Domain.SecurityEventType.UserLocked, "Auth", "AutoLock", nameof(AppUser), user.Id.ToString(), "Bloqueio automatico por tentativas", Environment.MachineName, string.Empty));
            }

            _security?.RecordLoginAttempt(new SecurityLoginAttemptRequest(username, user.Id, false, "Senha invalida", Environment.MachineName, string.Empty));
            user.UpdatedAtUtc = DateTime.UtcNow;
            _users.Update(user);
            return new AuthenticationResult(false, "Senha inválida.");
        }

        user.LastLoginAtUtc = DateTime.UtcNow;
        user.MustChangePassword = false;
        user.FailedLoginCount = 0;
        user.LockedUntilUtc = null;
        user.UpdatedAtUtc = DateTime.UtcNow;
        var sessionKey = Guid.NewGuid().ToString("N");
        _security?.RecordLoginAttempt(new SecurityLoginAttemptRequest(username, user.Id, true, string.Empty, Environment.MachineName, string.Empty));
        _security?.OpenSession(new SecuritySessionRequest(user.Id, sessionKey, Environment.MachineName, string.Empty));
        _security?.RecordAudit(new SecurityAuditRequest(user.Id, MaterialPro.Domain.SecurityEventType.LoginSuccess, "Auth", "Login", nameof(AppUser), user.Id.ToString(), "Login realizado com sucesso", Environment.MachineName, string.Empty));
        _users.Update(user);
        return new AuthenticationResult(true, "Login realizado com sucesso.", user, sessionKey);
    }

    public AppUser CreateAdmin(string fullName, string username, string email, string password)
    {
        if (_users.FindByUsername(username) is not null || _users.FindByEmail(email) is not null)
        {
            throw new InvalidOperationException("Usuário administrador já existe.");
        }

        var salt = _hasher.CreateSalt();
        var user = new AppUser
        {
            FullName = fullName.Trim(),
            Username = username.Trim(),
            Email = email.Trim(),
            PasswordSalt = salt,
            PasswordHash = _hasher.Hash(password, salt),
            Role = UserRole.Admin,
            MustChangePassword = true,
            PasswordChangedAtUtc = DateTime.UtcNow
        };

        _users.Add(user);
        _security?.RecordAudit(new SecurityAuditRequest(user.Id, MaterialPro.Domain.SecurityEventType.Audit, "User", "CreateAdmin", nameof(AppUser), user.Id.ToString(), "Administrador criado", Environment.MachineName, string.Empty));
        return user;
    }
}

public sealed class AuthorizationService : IAuthorizationService
{
    public bool HasPermission(AppUser user, MaterialPro.Domain.Permission permission)
    {
        return MaterialPro.Domain.RolePermissions.For(user.Role).Contains(permission);
    }
}
