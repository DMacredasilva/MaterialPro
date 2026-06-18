using MaterialPro.Application;
using MaterialPro.Infrastructure;

namespace MaterialPro.UI;

public sealed class SecurityCenterForm : Form
{
    private readonly ISecurityService _securityService;
    private readonly DataGridView _auditGrid;
    private readonly DataGridView _attemptGrid;
    private readonly DataGridView _sessionGrid;
    private readonly TextBox _userIdBox;

    public SecurityCenterForm(ISecurityService securityService)
    {
        _securityService = securityService;

        Text = "Auditoria e seguranca";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1200, 760);
        BackColor = Color.FromArgb(246, 248, 252);
        Font = new Font("Segoe UI", 10F);

        var topBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 54,
            Padding = new Padding(16, 12, 16, 8),
            WrapContents = false
        };

        var refreshButton = MakeButton("Atualizar", Color.FromArgb(30, 78, 140));
        refreshButton.Click += (_, _) => LoadData();
        var lockButton = MakeButton("Bloquear", Color.FromArgb(180, 40, 40));
        lockButton.Click += (_, _) => LockUser();
        var unlockButton = MakeButton("Desbloquear", Color.FromArgb(28, 120, 84));
        unlockButton.Click += (_, _) => UnlockUser();

        _userIdBox = new TextBox { Width = 320, PlaceholderText = "ID do usuario (GUID) para bloquear/desbloquear" };
        topBar.Controls.Add(refreshButton);
        topBar.Controls.Add(lockButton);
        topBar.Controls.Add(unlockButton);
        topBar.Controls.Add(_userIdBox);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        _auditGrid = CreateGrid();
        _attemptGrid = CreateGrid();
        _sessionGrid = CreateGrid();
        tabs.TabPages.Add(new TabPage("Auditoria") { Controls = { _auditGrid } });
        tabs.TabPages.Add(new TabPage("Tentativas") { Controls = { _attemptGrid } });
        tabs.TabPages.Add(new TabPage("Sessoes") { Controls = { _sessionGrid } });

        Controls.Add(tabs);
        Controls.Add(topBar);

        LoadData();
    }

    private static Button MakeButton(string text, Color color) => new()
    {
        Text = text,
        Width = 120,
        Height = 34,
        BackColor = color,
        ForeColor = Color.White,
        FlatStyle = FlatStyle.Flat,
        Margin = new Padding(0, 0, 10, 0)
    };

    private static DataGridView CreateGrid()
    {
        return new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            ReadOnly = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Color.White
        };
    }

    private void LoadData()
    {
        Bind(_auditGrid, _securityService.GetAudits().Select(x => new
        {
            x.CreatedAtUtc,
            x.EventType,
            x.Area,
            x.Action,
            x.EntityName,
            x.EntityId,
            x.Details,
            x.UserId
        }).ToList());

        Bind(_attemptGrid, _securityService.GetLoginAttempts().Select(x => new
        {
            x.AttemptedAtUtc,
            x.Username,
            x.Success,
            x.FailureReason,
            x.MachineName,
            x.IpAddress
        }).ToList());

        Bind(_sessionGrid, _securityService.GetSessions().Select(x => new
        {
            x.StartedAtUtc,
            x.SessionKey,
            x.UserId,
            x.MachineName,
            x.IpAddress,
            x.IsClosed,
            x.EndedAtUtc
        }).ToList());
    }

    private static void Bind(DataGridView grid, object data)
    {
        grid.DataSource = data;
    }

    private void LockUser()
    {
        if (Guid.TryParse(_userIdBox.Text, out var userId))
        {
            _securityService.LockUser(userId, "Bloqueio manual");
            LoadData();
        }
    }

    private void UnlockUser()
    {
        if (Guid.TryParse(_userIdBox.Text, out var userId))
        {
            _securityService.UnlockUser(userId);
            LoadData();
        }
    }
}
