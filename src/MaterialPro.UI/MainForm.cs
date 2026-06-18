using MaterialPro.Application;
using MaterialPro.Domain;
using MaterialPro.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MaterialPro.UI;

public sealed class MainForm : Form
{
    private static readonly Color Ink = Color.FromArgb(31, 41, 55);
    private static readonly Color Muted = Color.FromArgb(95, 108, 124);
    private static readonly Color Surface = Color.FromArgb(245, 247, 250);
    private static readonly Color Navy = Color.FromArgb(23, 52, 87);
    private static readonly Color Cement = Color.FromArgb(226, 231, 237);
    private static readonly Color SafetyOrange = Color.FromArgb(218, 124, 38);
    private static readonly Color SteelBlue = Color.FromArgb(38, 89, 143);
    private static readonly Color StockGreen = Color.FromArgb(45, 126, 86);

    private readonly Label _brandTitleLabel;
    private readonly Label _brandSubtitleLabel;
    private readonly PictureBox _brandLogoBox;
    private readonly Panel _contentPanel;
    private readonly TextBox _usernameBox;
    private readonly TextBox _passwordBox;
    private readonly Label _statusLabel;
    private readonly IAuthService _authService;
    private readonly IStoreProfileService _storeProfileService;
    private readonly ISecurityService _securityService;
    private readonly IInternalDocumentService _internalDocumentService;
    private readonly INonFiscalNoteService _nonFiscalNoteService;
    private readonly IProductService _productService;
    private readonly IInventoryService _inventoryService;
    private readonly IStockImportService _stockImportService;
    private readonly IStockReportService _stockReportService;
    private readonly IProductImportService _productImportService;
    private readonly IProductReportService _productReportService;
    private readonly ICustomerService _customerService;
    private readonly ICustomerReportService _customerReportService;
    private readonly ISupplierService _supplierService;
    private readonly ISupplierImportService _supplierImportService;
    private readonly ISupplierReportService _supplierReportService;
    private readonly ISalesService _salesService;
    private readonly IPdvService _pdvService;
    private readonly ISalesReportService _salesReportService;
    private readonly ISaleCancellationService _saleCancellationService;
    private readonly ICashService _cashService;
    private readonly ICashReportService _cashReportService;
    private readonly IFinancialService _financialService;
    private readonly IReportsCenterService _reportsCenterService;
    private readonly IPrintService _printService;
    private readonly IPrinterManagementService _printerManagementService;
    private readonly MaterialProDbContext _db;
    private readonly IDbfImportService _dbfImportService;
    private readonly MaterialProSettings _settings;
    private AppUser? _currentUser;
    private string? _currentSessionKey;

    public MainForm()
    {
        Text = "MaterialPro";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1180, 760);
        BackColor = Surface;
        Font = new Font("Segoe UI", 10F);

        _settings = MaterialProSettingsLoader.Load(AppContext.BaseDirectory);
        if (string.IsNullOrWhiteSpace(_settings.ConnectionString))
        {
            throw new InvalidOperationException("ConnectionString do MySQL não configurada.");
        }

        _db = MaterialProDbContextFactory.CreateMySql(_settings.ConnectionString);
        var hasher = new Sha256PasswordHasher();
        var repository = new EfUserRepository(_db);
        _securityService = new SecurityService(_db);
        _authService = new AuthService(repository, hasher, _securityService);
        _storeProfileService = new StoreProfileService(_db);
        _internalDocumentService = new InternalDocumentService();
        _printService = new PrintService();
        _nonFiscalNoteService = new NonFiscalNoteService(_db);
        _productService = new ProductService(_db);
        _inventoryService = new InventoryService(_db, _securityService);
        _stockImportService = new StockImportService(_db, _securityService);
        _stockReportService = new StockReportService(_db);
        _productImportService = new ProductImportService(_db);
        _productReportService = new ProductReportService(_db);
        _customerService = new CustomerService(_db);
        _customerReportService = new CustomerReportService(_db);
        _supplierService = new SupplierService(_db, _securityService);
        _supplierImportService = new SupplierImportService(_db, _securityService);
        _supplierReportService = new SupplierReportService(_db);
        _salesService = new SalesService(_db);
        _pdvService = new PdvService(_db, hasher, _securityService);
        _salesReportService = new SalesReportService(_db);
        _saleCancellationService = new SaleCancellationService(_db, hasher, _securityService);
        _cashService = new CashService(_db, hasher, _securityService);
        _cashReportService = new CashReportService(_db);
        _financialService = new FinancialService(_db);
        _reportsCenterService = new ReportsCenterService(_db);
        _printerManagementService = new PrinterManagementService(_db);
        _dbfImportService = new DbfImportService(_db);
        new MaterialProDatabaseInitializer(_db, _authService).EnsureCreated();

        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(shell);

        var leftPane = BuildBrandPane(out _brandTitleLabel, out _brandSubtitleLabel, out _brandLogoBox);
        _contentPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(34), BackColor = Surface, AutoScroll = true };
        shell.Controls.Add(leftPane, 0, 0);
        shell.Controls.Add(_contentPanel, 1, 0);

        _usernameBox = Input("Usuario ou e-mail");
        _passwordBox = Input("Senha");
        _passwordBox.UseSystemPasswordChar = true;
        _statusLabel = new Label { Dock = DockStyle.Top, Height = 34, ForeColor = Muted, TextAlign = ContentAlignment.MiddleLeft };

        ShowLogin();
        ApplyStoreProfile();
    }

    private void ShowLogin()
    {
        _contentPanel.Controls.Clear();

        var card = new Panel
        {
            Width = 520,
            Height = 420,
            BackColor = Color.White,
            Padding = new Padding(34),
            Anchor = AnchorStyles.None
        };
        card.Location = new Point(Math.Max(34, (_contentPanel.ClientSize.Width - card.Width) / 2), Math.Max(34, (_contentPanel.ClientSize.Height - card.Height) / 2));
        _contentPanel.Resize += (_, _) => card.Location = new Point(Math.Max(34, (_contentPanel.ClientSize.Width - card.Width) / 2), Math.Max(34, (_contentPanel.ClientSize.Height - card.Height) / 2));

        var title = new Label
        {
            Text = "Acesso operacional",
            Dock = DockStyle.Top,
            Height = 44,
            Font = new Font("Segoe UI", 20F, FontStyle.Bold),
            ForeColor = Ink
        };

        var subtitle = new Label
        {
            Text = "Entre para gerenciar balcão, estoque, clientes, relatórios e documentos da loja.",
            Dock = DockStyle.Top,
            Height = 56,
            ForeColor = Muted
        };

        var loginButton = PrimaryButton("Entrar no MaterialPro", SteelBlue);
        loginButton.Dock = DockStyle.Top;
        loginButton.Click += OnLogin;

        card.Controls.Add(_statusLabel);
        card.Controls.Add(Spacer(10));
        card.Controls.Add(loginButton);
        card.Controls.Add(Spacer(14));
        card.Controls.Add(_passwordBox);
        card.Controls.Add(LabelFor("Senha"));
        card.Controls.Add(Spacer(14));
        card.Controls.Add(_usernameBox);
        card.Controls.Add(LabelFor("Usuario"));
        card.Controls.Add(subtitle);
        card.Controls.Add(title);

        _contentPanel.Controls.Add(card);
        AcceptButton = loginButton;
    }

    private void ShowDashboard()
    {
        _contentPanel.Controls.Clear();

        var root = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 4 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _contentPanel.Controls.Add(root);

        var header = new Panel { Dock = DockStyle.Top, Height = 132, Padding = new Padding(0, 0, 0, 18), BackColor = Surface };
        var headerActions = new FlowLayoutPanel
        {
            Width = 330,
            Height = 48,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Surface
        };
        PositionHeaderActions(header, headerActions);
        header.Resize += (_, _) => PositionHeaderActions(header, headerActions);

        var closeButton = PrimaryButton("Fechar", Color.FromArgb(170, 70, 50), 120);
        closeButton.Click += (_, _) => Close();
        var logoutButton = PrimaryButton("Deslogar", SafetyOrange, 140);
        logoutButton.Click += (_, _) => Logout();
        headerActions.Controls.Add(closeButton);
        headerActions.Controls.Add(logoutButton);
        header.Controls.Add(headerActions);
        header.Controls.Add(new Label
        {
            Text = $"Bem-vindo, {_currentUser?.FullName ?? "Operador"}",
            Dock = DockStyle.Top,
            Height = 48,
            Font = new Font("Segoe UI", 24F, FontStyle.Bold),
            ForeColor = Ink
        });
        header.Controls.Add(new Label
        {
            Text = "Painel para loja de material de construcao: cadastro, estoque, clientes, vendas e documentos.",
            Dock = DockStyle.Bottom,
            Height = 36,
            ForeColor = Muted
        });
        root.Controls.Add(header);

        var metrics = new TableLayoutPanel { Dock = DockStyle.Top, Height = 94, ColumnCount = 4, Padding = new Padding(0, 0, 0, 18) };
        metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        metrics.Controls.Add(Metric("Produtos cadastrados", _productService.List().Count.ToString(), StockGreen), 0, 0);
        metrics.Controls.Add(Metric("Clientes ativos", _customerService.Search(new CustomerSearchRequest()).Count.ToString(), SteelBlue), 1, 0);
        metrics.Controls.Add(Metric("Fornecedores ativos", _supplierService.Search(new SupplierSearchRequest()).Count.ToString(), SafetyOrange), 2, 0);
        metrics.Controls.Add(Metric("Base da loja", DateTime.Now.ToString("dd/MM/yyyy"), Navy), 3, 0);
        root.Controls.Add(metrics);
        root.Controls.Add(SystemStatusPanel());

        var modules = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            MinimumSize = new Size(0, 520),
            AutoScroll = false,
            WrapContents = true,
            Padding = new Padding(0, 14, 0, 20)
        };

        AddModule(modules, SystemModules.Products, ModuleTile("Produtos", "Cadastro, estoque minimo, importacao e relatorios.", "CADASTRO", StockGreen, () =>
        {
            using var form = new ProductsForm(_productService, _inventoryService, _productImportService, _productReportService, _supplierService);
            form.ShowDialog(this);
            ShowDashboard();
        }));
        AddModule(modules, SystemModules.Stock, ModuleTile("Estoque", "Entradas, saidas, ajustes, inventario, reservas e transferencias.", "ESTOQUE", StockGreen, () =>
        {
            using var form = new StockForm(_inventoryService, _stockImportService, _stockReportService);
            form.ShowDialog(this);
            ShowDashboard();
        }));
        AddModule(modules, SystemModules.Customers, ModuleTile("Clientes", "Ficha, limite de credito, bloqueio e historicos.", "CRM", SteelBlue, () =>
        {
            using var form = new CustomersForm(_customerService, _customerReportService);
            form.ShowDialog(this);
            ShowDashboard();
        }));
        AddModule(modules, SystemModules.Suppliers, ModuleTile("Fornecedores", "Compras, produtos vinculados, contas a pagar e relatorios.", "COMPRAS", SafetyOrange, () =>
        {
            using var form = new SuppliersForm(_supplierService, _supplierImportService, _supplierReportService);
            form.ShowDialog(this);
            ShowDashboard();
        }));
        AddModule(modules, SystemModules.Pdv, ModuleTile("PDV", "Venda rapida, pagamento, caixa, cupom e segunda via.", "VENDA", Color.FromArgb(28, 120, 84), () =>
        {
            if (_currentUser is null) return;
            using var form = new PdvForm(_pdvService, _productService, _customerService, _currentUser);
            form.ShowDialog(this);
            ShowDashboard();
        }));
        AddModule(modules, SystemModules.Cash, ModuleTile("Caixa", "Abertura, sangria, suprimento, fechamento e historico.", "CAIXA", Color.FromArgb(23, 52, 87), () =>
        {
            if (_currentUser is null) return;
            using var form = new CashForm(_cashService, _cashReportService, _currentUser);
            form.ShowDialog(this);
            ShowDashboard();
        }));
        AddModule(modules, SystemModules.Financial, ModuleTile("Financeiro", "Contas a pagar, receber, duplicatas, baixas e fluxo de caixa.", "FIN", Color.FromArgb(44, 104, 110), () =>
        {
            if (_currentUser is null) return;
            using var form = new FinancialForm(_financialService, _currentUser);
            form.ShowDialog(this);
            ShowDashboard();
        }));
        AddModule(modules, SystemModules.Reports, ModuleTile("Relatorios", "Central com vendas, caixa, estoque, financeiro e sistema.", "REL", Color.FromArgb(90, 84, 154), () =>
        {
            if (_currentUser is null) return;
            using var form = new ReportsCenterForm(_reportsCenterService, _currentUser);
            form.ShowDialog(this);
            ShowDashboard();
        }));
        AddModule(modules, SystemModules.Updates, ModuleTile("Atualizacoes", "Cliente e servidor com status da instalacao e pacote de update.", "SISTEMA", Color.FromArgb(42, 111, 128), () =>
        {
            using var form = new UpdateStatusForm(_db, _currentUser);
            form.ShowDialog(this);
        }));
        AddModule(modules, SystemModules.Backup, ModuleTile("Backup", "Copia dos arquivos do sistema e banco MySQL para emergencia.", "SISTEMA", Color.FromArgb(42, 111, 128), () =>
        {
            using var form = new BackupForm(_currentUser, _settings.ConnectionString);
            form.ShowDialog(this);
        }));
        AddModule(modules, SystemModules.UserAccess, ModuleTile("Acesso por usuario", "Administrador libera quais modulos cada usuario pode ver.", "ADM", Color.FromArgb(92, 70, 150), () =>
        {
            using var form = new UserAccessForm(_db, new SecurityService(_db));
            form.ShowDialog(this);
            ShowDashboard();
        }));
        AddModule(modules, SystemModules.RemoteSupport, ModuleTile("Suporte remoto assistido", "ID, senha temporaria, nivel de acesso e ferramentas de conexao.", "SUPORTE", Color.FromArgb(44, 104, 110), () =>
        {
            using var form = new RemoteAccessForm();
            form.ShowDialog(this);
        }));
        AddModule(modules, SystemModules.InternalDocuments, ModuleTile("Documentos internos", "Cupom, recibo, orcamento e previa antes de imprimir.", "VENDA", SafetyOrange, () =>
        {
            using var form = new InternalDocumentsForm(_internalDocumentService, _printService, _storeProfileService, _currentUser);
            form.ShowDialog(this);
        }));
        AddModule(modules, SystemModules.Printers, ModuleTile("Impressoras", "Detectar, configurar, testar, fila e logs de impressao.", "SISTEMA", Color.FromArgb(42, 111, 128), () =>
        {
            using var form = new PrintersForm(_printerManagementService, _currentUser);
            form.ShowDialog(this);
        }));
        AddModule(modules, SystemModules.Cancellation, ModuleTile("Cancelamento", "Cancelamento controlado com comprovante e auditoria.", "CAIXA", Color.FromArgb(170, 70, 50), () =>
        {
            if (_currentUser is null) return;
            using var form = new SaleCancellationForm(_salesService, _saleCancellationService, _currentUser);
            form.ShowDialog(this);
        }));
        AddModule(modules, SystemModules.NonFiscalNote, ModuleTile("Nota avulsa", "Documento nao fiscal para atendimento rapido.", "DOC", Color.FromArgb(74, 93, 115), () =>
        {
            using var form = new NonFiscalNoteForm(_storeProfileService, _nonFiscalNoteService);
            form.ShowDialog(this);
        }));
        AddModule(modules, SystemModules.DbfImport, ModuleTile("Importacao DBF", "Entrada de dados legados de produtos, clientes e vendas.", "DADOS", Color.FromArgb(90, 101, 120), () =>
        {
            using var form = new DbfImportForm(_dbfImportService);
            form.ShowDialog(this);
        }));
        AddModule(modules, SystemModules.Security, ModuleTile("Seguranca", "Auditoria, sessoes e tentativas de acesso.", "ADM", Color.FromArgb(92, 70, 150), () =>
        {
            using var form = new SecurityCenterForm(_securityService);
            form.ShowDialog(this);
        }));
        AddModule(modules, SystemModules.StoreProfile, ModuleTile("Dados da loja", "Nome, CNPJ, telefone, endereco, logo e identidade.", "LOJA", Navy, () =>
        {
            using var form = new StoreProfileForm(_storeProfileService);
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                ApplyStoreProfile();
            }
        }));
        root.Controls.Add(modules);
    }

    private Control SystemStatusPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 160,
            ColumnCount = 4,
            Padding = new Padding(0, 0, 0, 18)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        var dbOk = CanConnectToDatabase();
        var clientStatus = SafeUpdateStatus("client");
        var serverStatus = SafeUpdateStatus("server");
        panel.Controls.Add(StatusCard("Cliente", $"Instalada: {clientStatus.LocalVersion}", $"GitHub: {clientStatus.RemoteVersion}", SteelBlue, () =>
        {
            using var form = new UpdateStatusForm(_db, _currentUser);
            form.ShowDialog(this);
        }), 0, 0);
        panel.Controls.Add(StatusCard("Servidor", $"Instalada: {serverStatus.LocalVersion}", $"GitHub: {serverStatus.RemoteVersion}", Color.FromArgb(42, 111, 128), () =>
        {
            using var form = new UpdateStatusForm(_db, _currentUser);
            form.ShowDialog(this);
        }), 1, 0);
        panel.Controls.Add(StatusCard("Rede e banco", dbOk ? "Cliente conectado ao servidor" : "Sem conexao confirmada", dbOk ? "Banco MySQL respondendo" : "Clique para diagnosticar", dbOk ? StockGreen : Color.FromArgb(170, 70, 50), () =>
        {
            using var form = new UpdateStatusForm(_db, _currentUser);
            form.ShowDialog(this);
        }), 2, 0);
        panel.Controls.Add(StatusCard("Administrador", IsAdmin() ? "Pode forcar instalar" : "Apenas consulta", "Abrir central de update", SafetyOrange, () =>
        {
            using var form = new UpdateStatusForm(_db, _currentUser);
            form.ShowDialog(this);
        }), 3, 0);
        return panel;
    }

    private bool CanConnectToDatabase()
    {
        try
        {
            return _db.Database.CanConnect();
        }
        catch
        {
            return false;
        }
    }

    private bool IsAdmin() => _currentUser?.Role == UserRole.Admin;

    private static UpdateStatusSnapshot SafeUpdateStatus(string channel)
    {
        try
        {
            return AutoUpdateRunner.GetStatus(channel);
        }
        catch
        {
            return new UpdateStatusSnapshot(channel, string.Empty, "desconhecida", "indisponivel", string.Empty, false, false, "Nao foi possivel consultar.");
        }
    }

    private static Button StatusCard(string title, string status, string action, Color accent, Action open)
    {
        var button = new Button
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 14, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Ink,
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(18, 12, 16, 10),
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            Text = $"{title}\r\n{status}\r\n{action}"
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(205, 214, 224);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(248, 250, 252);
        button.Paint += (_, e) =>
        {
            using var brush = new SolidBrush(accent);
            e.Graphics.FillRectangle(brush, 0, 0, 7, button.Height);
            using var pen = new Pen(Color.FromArgb(235, 239, 244));
            e.Graphics.DrawLine(pen, 18, 40, button.Width - 18, 40);
        };
        button.Click += (_, _) => open();
        return button;
    }

    private static void PositionHeaderActions(Control header, Control actions)
    {
        actions.Location = new Point(Math.Max(0, header.ClientSize.Width - actions.Width), 8);
    }

    private Control BuildBrandPane(out Label title, out Label subtitle, out PictureBox logo)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Navy, Padding = new Padding(28) };
        logo = new PictureBox
        {
            Dock = DockStyle.Top,
            Height = 86,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Navy,
            Visible = false
        };

        title = new Label
        {
            Text = "MaterialPro",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 26F, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 64
        };

        subtitle = new Label
        {
            Text = "Gestao para materiais de construcao",
            ForeColor = Color.FromArgb(220, 230, 240),
            Dock = DockStyle.Top,
            Height = 54
        };

        var bands = new Panel { Dock = DockStyle.Bottom, Height = 270, BackColor = Color.FromArgb(31, 66, 100), Padding = new Padding(22) };
        bands.Controls.Add(new Label
        {
            Text = "Balcao rapido\nEstoque organizado\nClientes com credito\nRelatorios da loja",
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 15F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        });
        bands.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 8, BackColor = SafetyOrange });

        var footer = new Label
        {
            Text = "Construido para rotina de loja: compra, venda, estoque e atendimento.",
            Dock = DockStyle.Bottom,
            Height = 72,
            ForeColor = Color.FromArgb(210, 222, 235)
        };

        panel.Controls.Add(footer);
        panel.Controls.Add(bands);
        panel.Controls.Add(subtitle);
        panel.Controls.Add(title);
        panel.Controls.Add(logo);
        return panel;
    }

    private void OnLogin(object? sender, EventArgs e)
    {
        var result = _authService.Login(new LoginRequest(_usernameBox.Text, _passwordBox.Text));
        _statusLabel.Text = result.Message;
        _statusLabel.ForeColor = result.Success ? StockGreen : Color.FromArgb(180, 40, 40);
        if (!result.Success || result.User is null)
        {
            return;
        }

        _currentUser = result.User;
        _currentSessionKey = result.SessionKey;
        _passwordBox.Clear();
        ShowDashboard();
    }

    private void Logout()
    {
        CloseCurrentSession();
        _currentUser = null;
        _currentSessionKey = null;
        _usernameBox.Clear();
        _passwordBox.Clear();
        ShowLogin();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        CloseCurrentSession();
        base.OnFormClosing(e);
    }

    private void CloseCurrentSession()
    {
        if (string.IsNullOrWhiteSpace(_currentSessionKey))
        {
            return;
        }

        try
        {
            _securityService.CloseSession(_currentSessionKey);
        }
        catch
        {
            // A sessao pode ja ter sido encerrada em outra rotina.
        }
    }

    private void ApplyStoreProfile()
    {
        var profile = _storeProfileService.Get();
        Text = profile.ProgramName;
        _brandTitleLabel.Text = string.IsNullOrWhiteSpace(profile.ProgramName) ? "MaterialPro" : profile.ProgramName;

        var storeName = string.IsNullOrWhiteSpace(profile.StoreName) ? "Loja de materiais" : profile.StoreName;
        var phone = string.IsNullOrWhiteSpace(profile.Phone) ? string.Empty : $" | {profile.Phone}";
        _brandSubtitleLabel.Text = $"{storeName}{phone}";
        ApplyBrandLogo(profile.LogoPath);
    }

    private void ApplyBrandLogo(string? logoPath)
    {
        var previous = _brandLogoBox.Image;
        _brandLogoBox.Image = null;
        previous?.Dispose();

        if (string.IsNullOrWhiteSpace(logoPath) || !File.Exists(logoPath))
        {
            _brandLogoBox.Visible = false;
            return;
        }

        try
        {
            using var image = Image.FromFile(logoPath);
            _brandLogoBox.Image = new Bitmap(image);
            _brandLogoBox.Visible = true;
        }
        catch
        {
            _brandLogoBox.Visible = false;
        }
    }

    private static Button ModuleTile(string title, string description, string tag, Color accent, Action open)
    {
        var button = new Button
        {
            Width = 298,
            Height = 126,
            Margin = new Padding(0, 0, 18, 18),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Ink,
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(20, 13, 18, 12),
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            Text = $"{tag}  |  {title}\r\n{description}"
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(205, 214, 224);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(248, 250, 252);
        button.Paint += (_, e) =>
        {
            using var brush = new SolidBrush(accent);
            e.Graphics.FillRectangle(brush, 0, 0, 7, button.Height);
            using var pen = new Pen(Color.FromArgb(235, 239, 244));
            e.Graphics.DrawLine(pen, 18, 42, button.Width - 18, 42);
        };
        button.Click += (_, _) => open();
        return button;
    }

    private void AddModule(FlowLayoutPanel panel, string moduleKey, Button tile)
    {
        if (HasModule(moduleKey))
        {
            panel.Controls.Add(tile);
        }
    }

    private bool HasModule(string moduleKey) => _currentUser?.EffectiveModules().Contains(moduleKey, StringComparer.OrdinalIgnoreCase) == true;

    private static Panel Metric(string label, string value, Color accent)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(0, 0, 14, 0), Padding = new Padding(16, 10, 16, 10) };
        panel.Controls.Add(new Label
        {
            Text = value,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            ForeColor = accent
        });
        panel.Controls.Add(new Label
        {
            Text = label,
            Dock = DockStyle.Top,
            Height = 24,
            ForeColor = Muted
        });
        panel.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 5, BackColor = accent });
        return panel;
    }

    private static TextBox Input(string placeholder)
    {
        return new TextBox
        {
            Dock = DockStyle.Top,
            Height = 34,
            PlaceholderText = placeholder,
            BorderStyle = BorderStyle.FixedSingle
        };
    }

    private static Label LabelFor(string text)
    {
        return new Label { Text = text, Dock = DockStyle.Top, Height = 24, ForeColor = Ink };
    }

    private static Control Spacer(int height)
    {
        return new Panel { Dock = DockStyle.Top, Height = height };
    }

    private static Button PrimaryButton(string text, Color color, int width = 150)
    {
        return new Button
        {
            Text = text,
            Width = width,
            Height = 44,
            FlatStyle = FlatStyle.Flat,
            BackColor = color,
            ForeColor = Color.White
        };
    }
}
