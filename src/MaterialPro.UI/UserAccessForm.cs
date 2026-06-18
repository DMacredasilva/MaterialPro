using MaterialPro.Domain;
using MaterialPro.Infrastructure;

namespace MaterialPro.UI;

public sealed class UserAccessForm : Form
{
    private readonly MaterialProDbContext _db;
    private readonly SecurityService _security;
    private readonly DataGridView _grid = UiKit.Grid();
    private readonly TextBox _name = new() { Width = 220, PlaceholderText = "Nome completo" };
    private readonly TextBox _username = new() { Width = 160, PlaceholderText = "Usuario" };
    private readonly TextBox _email = new() { Width = 220, PlaceholderText = "E-mail" };
    private readonly TextBox _password = new() { Width = 150, PlaceholderText = "Senha inicial" };
    private readonly ComboBox _role = new() { Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckedListBox _modules = new() { Dock = DockStyle.Fill, CheckOnClick = true, BorderStyle = BorderStyle.None };

    public UserAccessForm(MaterialProDbContext db, SecurityService security)
    {
        _db = db;
        _security = security;

        Text = "MaterialPro - Acesso por usuario";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1120, 700);
        BackColor = UiKit.Surface;
        Font = new Font("Segoe UI", 10F);

        _role.DisplayMember = nameof(RoleOption.Name);
        _role.ValueMember = nameof(RoleOption.Role);
        _role.Items.AddRange(
        [
            new RoleOption(UserRole.Admin, "Administrador"),
            new RoleOption(UserRole.Manager, "Gerente"),
            new RoleOption(UserRole.Cashier, "Caixa"),
            new RoleOption(UserRole.Stock, "Estoque")
        ]);
        UiKit.SelectIfAvailable(_role, 2);
        _role.SelectedIndexChanged += (_, _) =>
        {
            if (SelectedUser() is null)
            {
                CheckDefaultModules(SelectedRole());
            }
        };

        LoadModuleChecklist();

        var editor = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 104, Padding = new Padding(18, 12, 18, 6), BackColor = Color.White, WrapContents = true };
        editor.Controls.AddRange([_name, _username, _email, _password, _role]);
        editor.Controls.Add(UiKit.Button("Criar usuario", UiKit.Green, (_, _) => CreateUser()));
        editor.Controls.Add(UiKit.Button("Salvar modulos", UiKit.Green, (_, _) => SaveModules()));
        editor.Controls.Add(UiKit.Button("Resetar senha", UiKit.Orange, (_, _) => ResetPassword()));
        editor.Controls.Add(UiKit.Button("Ativar/Inativar", UiKit.Blue, (_, _) => ToggleActive()));
        editor.Controls.Add(UiKit.Button("Bloquear", UiKit.Brick, (_, _) => LockSelected()));
        editor.Controls.Add(UiKit.Button("Desbloquear", UiKit.Green, (_, _) => UnlockSelected()));

        var modulePanel = new Panel { Dock = DockStyle.Right, Width = 310, BackColor = Color.White, Padding = new Padding(14), Margin = new Padding(14, 0, 0, 0) };
        modulePanel.Controls.Add(_modules);
        modulePanel.Controls.Add(new Label { Text = "Modulos liberados para o usuario selecionado", Dock = DockStyle.Top, Height = 46, ForeColor = UiKit.Ink, Font = new Font("Segoe UI", 11F, FontStyle.Bold) });

        Controls.Add(_grid);
        Controls.Add(modulePanel);
        Controls.Add(editor);
        Controls.Add(UiKit.Header("Acesso por usuario", "Cadastre operadores, gerentes e estoque com perfis simples para a rotina da loja."));
        _grid.SelectionChanged += (_, _) => LoadSelectedUserModules();
        LoadUsers();
        CheckDefaultModules(SelectedRole());
    }

    private void LoadUsers()
    {
        _grid.DataSource = _db.Users
            .OrderBy(x => x.FullName)
            .Select(x => new
            {
                x.Id,
                Nome = x.FullName,
                Usuario = x.Username,
                x.Email,
                Perfil = RoleName(x.Role),
                Ativo = x.IsActive,
                Modulos = x.Role == UserRole.Admin ? "Todos" : string.Join(", ", ModuleNames(x.EffectiveModules())),
                BloqueadoAte = x.LockedUntilUtc,
                UltimoLogin = x.LastLoginAtUtc
            })
            .ToList();
    }

    private void CreateUser()
    {
        if (string.IsNullOrWhiteSpace(_name.Text) || string.IsNullOrWhiteSpace(_username.Text) || string.IsNullOrWhiteSpace(_password.Text))
        {
            MessageBox.Show(this, "Informe nome, usuario e senha inicial.", "Acesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_db.Users.Any(x => x.Username == _username.Text.Trim()))
        {
            MessageBox.Show(this, "Ja existe usuario com este login.", "Acesso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var hasher = new Sha256PasswordHasher();
        var salt = hasher.CreateSalt();
        _db.Users.Add(new AppUser
        {
            FullName = _name.Text.Trim(),
            Username = _username.Text.Trim(),
            Email = string.IsNullOrWhiteSpace(_email.Text) ? $"{_username.Text.Trim()}@materialpro.local" : _email.Text.Trim(),
            PasswordSalt = salt,
            PasswordHash = hasher.Hash(_password.Text, salt),
            Role = _role.SelectedItem is RoleOption option ? option.Role : UserRole.Cashier,
            AllowedModules = SelectedRole() == UserRole.Admin ? string.Empty : string.Join(',', CheckedModuleKeys()),
            MustChangePassword = true,
            PasswordChangedAtUtc = DateTime.UtcNow
        });
        _db.SaveChanges();
        ClearFields();
        LoadUsers();
    }

    private void SaveModules()
    {
        var user = SelectedUser();
        if (user is null) return;
        user.AllowedModules = user.Role == UserRole.Admin ? string.Empty : string.Join(',', CheckedModuleKeys());
        user.UpdatedAtUtc = DateTime.UtcNow;
        _db.SaveChanges();
        MessageBox.Show(this, "Modulos liberados para este usuario.", "Acesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
        LoadUsers();
    }

    private void ResetPassword()
    {
        var user = SelectedUser();
        if (user is null) return;
        var newPassword = string.IsNullOrWhiteSpace(_password.Text) ? "Material@123" : _password.Text.Trim();
        var hasher = new Sha256PasswordHasher();
        user.PasswordSalt = hasher.CreateSalt();
        user.PasswordHash = hasher.Hash(newPassword, user.PasswordSalt);
        user.MustChangePassword = true;
        user.PasswordChangedAtUtc = DateTime.UtcNow;
        user.UpdatedAtUtc = DateTime.UtcNow;
        _db.SaveChanges();
        MessageBox.Show(this, $"Senha redefinida para: {newPassword}", "Acesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
        LoadUsers();
    }

    private void ToggleActive()
    {
        var user = SelectedUser();
        if (user is null) return;
        user.IsActive = !user.IsActive;
        user.UpdatedAtUtc = DateTime.UtcNow;
        _db.SaveChanges();
        LoadUsers();
    }

    private void LockSelected()
    {
        var user = SelectedUser();
        if (user is null) return;
        _security.LockUser(user.Id, "Bloqueio pelo modulo de acesso", DateTime.UtcNow.AddDays(30));
        LoadUsers();
    }

    private void UnlockSelected()
    {
        var user = SelectedUser();
        if (user is null) return;
        _security.UnlockUser(user.Id);
        LoadUsers();
    }

    private AppUser? SelectedUser()
    {
        var id = SelectedId();
        return id.HasValue ? _db.Users.FirstOrDefault(x => x.Id == id.Value) : null;
    }

    private Guid? SelectedId()
    {
        if (_grid.CurrentRow?.DataBoundItem is null) return null;
        var property = _grid.CurrentRow.DataBoundItem.GetType().GetProperty("Id");
        return property?.GetValue(_grid.CurrentRow.DataBoundItem) is Guid id ? id : null;
    }

    private void ClearFields()
    {
        _name.Clear();
        _username.Clear();
        _email.Clear();
        _password.Clear();
        CheckDefaultModules(SelectedRole());
    }

    private void LoadModuleChecklist()
    {
        _modules.Items.Clear();
        foreach (var module in ModuleCatalog())
        {
            _modules.Items.Add(module);
        }
    }

    private void LoadSelectedUserModules()
    {
        var user = SelectedUser();
        if (user is null)
        {
            CheckDefaultModules(SelectedRole());
            return;
        }

        _role.SelectedItem = _role.Items.Cast<RoleOption>().FirstOrDefault(x => x.Role == user.Role) ?? _role.SelectedItem;
        CheckModules(user.EffectiveModules());
        _modules.Enabled = user.Role != UserRole.Admin;
    }

    private void CheckDefaultModules(UserRole role)
    {
        CheckModules(SystemModules.DefaultsFor(role));
        _modules.Enabled = role != UserRole.Admin;
    }

    private void CheckModules(IEnumerable<string> keys)
    {
        var allowed = keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < _modules.Items.Count; i++)
        {
            var item = (ModuleOption)_modules.Items[i];
            _modules.SetItemChecked(i, allowed.Contains(item.Key));
        }
    }

    private IEnumerable<string> CheckedModuleKeys()
    {
        return _modules.CheckedItems.Cast<ModuleOption>().Select(x => x.Key);
    }

    private UserRole SelectedRole() => _role.SelectedItem is RoleOption option ? option.Role : UserRole.Cashier;

    private static IEnumerable<string> ModuleNames(IEnumerable<string> keys)
    {
        var names = ModuleCatalog().ToDictionary(x => x.Key, x => x.Name, StringComparer.OrdinalIgnoreCase);
        return keys.Select(key => names.TryGetValue(key, out var name) ? name : key);
    }

    private static IReadOnlyList<ModuleOption> ModuleCatalog() =>
    [
        new(SystemModules.Products, "Produtos"),
        new(SystemModules.Stock, "Estoque"),
        new(SystemModules.Customers, "Clientes"),
        new(SystemModules.Suppliers, "Fornecedores"),
        new(SystemModules.Pdv, "PDV"),
        new(SystemModules.Cash, "Caixa"),
        new(SystemModules.Financial, "Financeiro"),
        new(SystemModules.Reports, "Relatorios"),
        new(SystemModules.Updates, "Atualizacoes"),
        new(SystemModules.Backup, "Backup"),
        new(SystemModules.UserAccess, "Acesso por usuario"),
        new(SystemModules.RemoteSupport, "Suporte remoto"),
        new(SystemModules.InternalDocuments, "Documentos e impressao"),
        new(SystemModules.Printers, "Impressoras"),
        new(SystemModules.Cancellation, "Cancelamento"),
        new(SystemModules.NonFiscalNote, "Nota avulsa"),
        new(SystemModules.DbfImport, "Importacao DBF"),
        new(SystemModules.Security, "Seguranca"),
        new(SystemModules.StoreProfile, "Dados da loja")
    ];

    private static string RoleName(UserRole role) => role switch
    {
        UserRole.Admin => "Administrador",
        UserRole.Manager => "Gerente",
        UserRole.Cashier => "Caixa",
        UserRole.Stock => "Estoque",
        _ => role.ToString()
    };

    private sealed record RoleOption(UserRole Role, string Name);
    private sealed record ModuleOption(string Key, string Name)
    {
        public override string ToString() => Name;
    }
}
