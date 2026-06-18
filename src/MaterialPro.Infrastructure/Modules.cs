using MaterialPro.Application;
using MaterialPro.Domain;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Helpers;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using System.Drawing.Printing;
using System.Drawing;
using System.Runtime.Versioning;

namespace MaterialPro.Infrastructure;

public sealed partial class MaterialProDbContext
{
    public DbSet<StoreProfile> StoreProfiles => Set<StoreProfile>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<BudgetItem> BudgetItems => Set<BudgetItem>();
    public DbSet<CashSession> CashSessions => Set<CashSession>();
    public DbSet<CashMovement> CashMovements => Set<CashMovement>();
    public DbSet<AccountPayable> AccountsPayable => Set<AccountPayable>();
    public DbSet<Duplicate> Duplicates => Set<Duplicate>();
    public DbSet<FinancialMovement> FinancialMovements => Set<FinancialMovement>();
    public DbSet<FinancialSettlement> FinancialSettlements => Set<FinancialSettlement>();
    public DbSet<FinancialCategory> FinancialCategories => Set<FinancialCategory>();
    public DbSet<FinancialLog> FinancialLogs => Set<FinancialLog>();
    public DbSet<ReportAuditLog> ReportAuditLogs => Set<ReportAuditLog>();
    public DbSet<ReportSchedule> ReportSchedules => Set<ReportSchedule>();
    public DbSet<SaleCancellation> SaleCancellations => Set<SaleCancellation>();
    public DbSet<SaleReturn> SaleReturns => Set<SaleReturn>();
    public DbSet<NonFiscalNote> NonFiscalNotes => Set<NonFiscalNote>();
    public DbSet<NonFiscalNoteItem> NonFiscalNoteItems => Set<NonFiscalNoteItem>();
    public DbSet<SecurityAuditEntry> SecurityAudits => Set<SecurityAuditEntry>();
    public DbSet<SecurityLoginAttempt> SecurityLoginAttempts => Set<SecurityLoginAttempt>();
    public DbSet<SecuritySession> SecuritySessions => Set<SecuritySession>();
    public DbSet<StockInventory> StockInventories => Set<StockInventory>();
    public DbSet<StockInventoryItem> StockInventoryItems => Set<StockInventoryItem>();
    public DbSet<StockTransfer> StockTransfers => Set<StockTransfer>();
    public DbSet<StockReservation> StockReservations => Set<StockReservation>();
    public DbSet<SalePayment> SalePayments => Set<SalePayment>();
    public DbSet<SaleLog> SaleLogs => Set<SaleLog>();
}

public sealed class StoreProfileService : IStoreProfileService
{
    private readonly MaterialProDbContext _db;

    public StoreProfileService(MaterialProDbContext db) => _db = db;

    public StoreProfile Get()
    {
        var profile = _db.StoreProfiles.FirstOrDefault();
        if (profile is not null)
        {
            return profile;
        }

        profile = new StoreProfile();
        _db.StoreProfiles.Add(profile);
        _db.SaveChanges();
        return profile;
    }

    public StoreProfile Save(StoreProfileRequest request)
    {
        var profile = Get();
        profile.ProgramName = request.ProgramName.Trim();
        profile.StoreName = request.StoreName.Trim();
        profile.Cnpj = request.Cnpj.Trim();
        profile.Address = request.Address.Trim();
        profile.Phone = request.Phone.Trim();
        profile.LogoPath = request.LogoPath.Trim();
        profile.UpdatedAtUtc = DateTime.UtcNow;
        _db.SaveChanges();
        return profile;
    }
}

public sealed class SecurityService : ISecurityService
{
    private readonly MaterialProDbContext _db;

    public SecurityService(MaterialProDbContext db) => _db = db;

    public void RecordAudit(SecurityAuditRequest request)
    {
        _db.SecurityAudits.Add(new SecurityAuditEntry
        {
            UserId = request.UserId,
            EventType = request.EventType,
            Area = request.Area.Trim(),
            Action = request.Action.Trim(),
            EntityName = request.EntityName.Trim(),
            EntityId = request.EntityId.Trim(),
            Details = request.Details.Trim(),
            MachineName = request.MachineName.Trim(),
            IpAddress = request.IpAddress.Trim()
        });
        _db.SaveChanges();
    }

    public void RecordLoginAttempt(SecurityLoginAttemptRequest request)
    {
        _db.SecurityLoginAttempts.Add(new SecurityLoginAttempt
        {
            Username = request.Username.Trim(),
            UserId = request.UserId,
            Success = request.Success,
            FailureReason = request.FailureReason.Trim(),
            MachineName = request.MachineName.Trim(),
            IpAddress = request.IpAddress.Trim(),
            AttemptedAtUtc = DateTime.UtcNow
        });
        _db.SaveChanges();
    }

    public SecuritySession OpenSession(SecuritySessionRequest request)
    {
        var session = new SecuritySession
        {
            UserId = request.UserId,
            SessionKey = request.SessionKey.Trim(),
            MachineName = request.MachineName.Trim(),
            IpAddress = request.IpAddress.Trim(),
            StartedAtUtc = DateTime.UtcNow
        };
        _db.SecuritySessions.Add(session);
        _db.SaveChanges();
        return session;
    }

    public SecuritySession CloseSession(string sessionKey)
    {
        var session = _db.SecuritySessions.First(x => x.SessionKey == sessionKey);
        session.IsClosed = true;
        session.EndedAtUtc = DateTime.UtcNow;
        session.UpdatedAtUtc = DateTime.UtcNow;
        _db.SaveChanges();
        return session;
    }

    public void LockUser(Guid userId, string reason, DateTime? untilUtc = null)
    {
        var user = _db.Users.First(x => x.Id == userId);
        user.LockedUntilUtc = untilUtc ?? DateTime.UtcNow.AddMinutes(15);
        user.Notes = string.IsNullOrWhiteSpace(user.Notes) ? $"Bloqueado: {reason}" : $"{user.Notes} | Bloqueado: {reason}";
        user.UpdatedAtUtc = DateTime.UtcNow;
        _db.SaveChanges();
        RecordAudit(new SecurityAuditRequest(userId, SecurityEventType.UserLocked, "Auth", "Lock", nameof(AppUser), userId.ToString(), reason, Environment.MachineName, string.Empty));
    }

    public void UnlockUser(Guid userId)
    {
        var user = _db.Users.First(x => x.Id == userId);
        user.LockedUntilUtc = null;
        user.FailedLoginCount = 0;
        user.UpdatedAtUtc = DateTime.UtcNow;
        _db.SaveChanges();
        RecordAudit(new SecurityAuditRequest(userId, SecurityEventType.UserUnlocked, "Auth", "Unlock", nameof(AppUser), userId.ToString(), "Unlock", Environment.MachineName, string.Empty));
    }

    public IReadOnlyList<SecurityAuditEntry> GetAudits() => _db.SecurityAudits.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).ToList();
    public IReadOnlyList<SecurityLoginAttempt> GetLoginAttempts() => _db.SecurityLoginAttempts.AsNoTracking().OrderByDescending(x => x.AttemptedAtUtc).ToList();
    public IReadOnlyList<SecuritySession> GetSessions() => _db.SecuritySessions.AsNoTracking().OrderByDescending(x => x.StartedAtUtc).ToList();
}

public sealed class UserSecurityService : IUserSecurityService
{
    private readonly MaterialProDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ISecurityService? _security;

    public UserSecurityService(MaterialProDbContext db, IPasswordHasher hasher, ISecurityService? security = null)
    {
        _db = db;
        _hasher = hasher;
        _security = security;
    }

    public void ChangePassword(ChangePasswordRequest request)
    {
        var user = _db.Users.First(x => x.Id == request.UserId);
        if (!_hasher.Verify(request.CurrentPassword, user.PasswordSalt, user.PasswordHash))
        {
            throw new InvalidOperationException("Senha atual inválida.");
        }

        var salt = _hasher.CreateSalt();
        user.PasswordSalt = salt;
        user.PasswordHash = _hasher.Hash(request.NewPassword, salt);
        user.PasswordChangedAtUtc = DateTime.UtcNow;
        user.MustChangePassword = false;
        user.UpdatedAtUtc = DateTime.UtcNow;
        _db.SaveChanges();
        _security?.RecordAudit(new SecurityAuditRequest(user.Id, SecurityEventType.PasswordChanged, "User", "ChangePassword", nameof(AppUser), user.Id.ToString(), "Senha alterada", Environment.MachineName, string.Empty));
    }
}

public sealed class FiscalConfigurationService : IFiscalConfigurationService
{
    private readonly FiscalSettings _settings;

    public FiscalConfigurationService(FiscalSettings settings)
    {
        _settings = settings;
    }

    public bool IsReady()
    {
        return Validate().Count == 0;
    }

    public IReadOnlyList<string> Validate()
    {
        var issues = new List<string>();
        if (!_settings.Enabled)
        {
            issues.Add("Fiscal desativado.");
        }
        if (string.IsNullOrWhiteSpace(_settings.Cnpj))
        {
            issues.Add("CNPJ fiscal nao informado.");
        }
        if (string.IsNullOrWhiteSpace(_settings.Uf))
        {
            issues.Add("UF nao informada.");
        }
        if (string.IsNullOrWhiteSpace(_settings.CertificatePath))
        {
            issues.Add("Certificado digital nao configurado.");
        }
        if (string.IsNullOrWhiteSpace(_settings.CscToken) && string.Equals(_settings.Environment, "producao", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add("CSC ausente para NFC-e em producao.");
        }

        return issues;
    }
}

public sealed class EfCustomerRepository : ICustomerRepository
{
    private readonly MaterialProDbContext _db;

    public EfCustomerRepository(MaterialProDbContext db) => _db = db;

    public void Add(Customer customer)
    {
        _db.Customers.Add(customer);
        _db.SaveChanges();
    }

    public void Update(Customer customer)
    {
        _db.Customers.Update(customer);
        _db.SaveChanges();
    }

    public Customer? FindById(Guid id) => _db.Customers.FirstOrDefault(x => x.Id == id);
    public Customer? FindByCode(string code) => _db.Customers.FirstOrDefault(x => x.Code.ToLower() == code.ToLower());
    public Customer? FindByDocument(string documentNumber) => _db.Customers.FirstOrDefault(x => x.DocumentNumber.ToLower() == documentNumber.ToLower());

    public IReadOnlyList<Customer> Search(CustomerSearchRequest request)
    {
        var term = request.Term.Trim().ToLower();
        var query = _db.Customers.AsNoTracking().AsQueryable();
        if (request.OnlyActive)
        {
            query = query.Where(x => x.IsActive);
        }

        if (!request.IncludeBlocked)
        {
            query = query.Where(x => !x.IsBlocked);
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(x =>
                x.Code.ToLower().Contains(term) ||
                x.FullName.ToLower().Contains(term) ||
                x.DocumentNumber.ToLower().Contains(term) ||
                x.Phone.ToLower().Contains(term) ||
                x.WhatsApp.ToLower().Contains(term) ||
                x.Email.ToLower().Contains(term));
        }

        return query.OrderBy(x => x.FullName).ToList();
    }
}

public sealed class CustomerService : ICustomerService
{
    private readonly MaterialProDbContext _db;
    private readonly ICustomerRepository _customers;

    public CustomerService(MaterialProDbContext db) : this(db, new EfCustomerRepository(db))
    {
    }

    public CustomerService(MaterialProDbContext db, ICustomerRepository customers)
    {
        _db = db;
        _customers = customers;
    }

    public Customer Create(CustomerUpsertRequest request)
    {
        Validate(request);
        var code = NormalizeCode(request.Code);
        if (!string.IsNullOrWhiteSpace(code) && _customers.FindByCode(code) is not null)
        {
            throw new InvalidOperationException("Ja existe cliente com este codigo.");
        }

        var document = request.DocumentNumber.Trim();
        if (!string.IsNullOrWhiteSpace(document) && _customers.FindByDocument(document) is not null)
        {
            throw new InvalidOperationException("Ja existe cliente com este CPF/CNPJ.");
        }

        var entity = new Customer
        {
            Code = string.IsNullOrWhiteSpace(code) ? NextCode() : code,
            FullName = request.FullName.Trim(),
            DocumentNumber = document,
            StateRegistration = request.StateRegistration.Trim(),
            Phone = request.Phone.Trim(),
            WhatsApp = request.WhatsApp.Trim(),
            Email = request.Email.Trim(),
            ZipCode = request.ZipCode.Trim(),
            Address = request.Address.Trim(),
            AddressNumber = request.AddressNumber.Trim(),
            Complement = request.Complement.Trim(),
            District = request.District.Trim(),
            City = request.City.Trim(),
            State = request.State.Trim().ToUpperInvariant(),
            CreditLimit = request.CreditLimit,
            Notes = request.Notes.Trim(),
            IsActive = request.IsActive,
            IsBlocked = request.IsBlocked
        };
        _customers.Add(entity);
        return entity;
    }

    public Customer Update(Guid id, CustomerUpsertRequest request)
    {
        Validate(request);
        var entity = _customers.FindById(id) ?? throw new InvalidOperationException("Cliente nao encontrado.");
        var code = NormalizeCode(request.Code);
        var duplicateCode = string.IsNullOrWhiteSpace(code) ? null : _customers.FindByCode(code);
        if (duplicateCode is not null && duplicateCode.Id != id)
        {
            throw new InvalidOperationException("Ja existe cliente com este codigo.");
        }

        var document = request.DocumentNumber.Trim();
        var duplicateDocument = string.IsNullOrWhiteSpace(document) ? null : _customers.FindByDocument(document);
        if (duplicateDocument is not null && duplicateDocument.Id != id)
        {
            throw new InvalidOperationException("Ja existe cliente com este CPF/CNPJ.");
        }

        entity.Code = string.IsNullOrWhiteSpace(code) ? entity.Code : code;
        entity.FullName = request.FullName.Trim();
        entity.DocumentNumber = document;
        entity.StateRegistration = request.StateRegistration.Trim();
        entity.Phone = request.Phone.Trim();
        entity.WhatsApp = request.WhatsApp.Trim();
        entity.Email = request.Email.Trim();
        entity.ZipCode = request.ZipCode.Trim();
        entity.Address = request.Address.Trim();
        entity.AddressNumber = request.AddressNumber.Trim();
        entity.Complement = request.Complement.Trim();
        entity.District = request.District.Trim();
        entity.City = request.City.Trim();
        entity.State = request.State.Trim().ToUpperInvariant();
        entity.CreditLimit = request.CreditLimit;
        entity.Notes = request.Notes.Trim();
        entity.IsActive = request.IsActive;
        entity.IsBlocked = request.IsBlocked;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        _customers.Update(entity);
        return entity;
    }

    public Customer? FindById(Guid id) => _customers.FindById(id);

    public Customer? FindByCodeOrDocument(string value)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return _customers.FindByCode(normalized) ?? _customers.FindByDocument(normalized);
    }

    public IReadOnlyList<Customer> Search(CustomerSearchRequest request) => _customers.Search(request);

    public IReadOnlyList<Customer> List() => Search(new CustomerSearchRequest());

    public Customer Inactivate(Guid id)
    {
        var customer = _customers.FindById(id) ?? throw new InvalidOperationException("Cliente nao encontrado.");
        customer.IsActive = false;
        customer.UpdatedAtUtc = DateTime.UtcNow;
        _customers.Update(customer);
        return customer;
    }

    public Customer Block(Guid id, string reason)
    {
        var customer = _customers.FindById(id) ?? throw new InvalidOperationException("Cliente nao encontrado.");
        customer.IsBlocked = true;
        customer.Notes = AppendText(customer.Notes, $"Bloqueado: {reason.Trim()}");
        customer.UpdatedAtUtc = DateTime.UtcNow;
        _customers.Update(customer);
        return customer;
    }

    public Customer Unblock(Guid id)
    {
        var customer = _customers.FindById(id) ?? throw new InvalidOperationException("Cliente nao encontrado.");
        customer.IsBlocked = false;
        customer.UpdatedAtUtc = DateTime.UtcNow;
        _customers.Update(customer);
        return customer;
    }

    public IReadOnlyList<CustomerPurchaseHistoryItem> PurchaseHistory(Guid customerId)
    {
        return _db.Sales.AsNoTracking()
            .Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.SoldAtUtc)
            .Select(x => new CustomerPurchaseHistoryItem(x.Id, x.ReceiptNumber, x.SoldAtUtc, x.TotalAmount, x.PaymentMethod, x.Status))
            .ToList();
    }

    public IReadOnlyList<CustomerFinancialHistoryItem> FinancialHistory(Guid customerId)
    {
        var customer = _customers.FindById(customerId) ?? throw new InvalidOperationException("Cliente nao encontrado.");
        var saleIds = _db.Sales.AsNoTracking().Where(x => x.CustomerId == customerId).Select(x => x.Id).ToList();

        var receivables = _db.AccountsReceivable.AsNoTracking()
            .Where(x => x.CustomerName == customer.FullName || (x.SaleId.HasValue && saleIds.Contains(x.SaleId.Value)))
            .Select(x => new CustomerFinancialHistoryItem(x.Number, x.Description, x.DueDateUtc, x.OriginalAmount, x.PaidAmount, x.BalanceAmount, x.Status))
            .ToList();

        var duplicates = _db.Duplicates.AsNoTracking()
            .Where(x => x.Type == FinancialType.Receivable && x.SaleId.HasValue && saleIds.Contains(x.SaleId.Value))
            .Select(x => new CustomerFinancialHistoryItem(x.Number, x.Description, x.DueDateUtc, x.Amount, x.PaidAmount, x.BalanceAmount, x.Status))
            .ToList();

        return receivables.Concat(duplicates).OrderByDescending(x => x.DueDateUtc).ToList();
    }

    public CustomerCreditSummary CreditSummary(Guid customerId)
    {
        var customer = _customers.FindById(customerId) ?? throw new InvalidOperationException("Cliente nao encontrado.");
        var openBalance = FinancialHistory(customerId)
            .Where(x => x.Status is FinancialStatus.Open or FinancialStatus.Overdue)
            .Sum(x => x.BalanceAmount);
        return new CustomerCreditSummary(customer.CreditLimit, openBalance, customer.CreditLimit - openBalance, customer.IsBlocked);
    }

    private string NextCode()
    {
        return $"CLI-{(_db.Customers.Count() + 1):D6}";
    }

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    private static void Validate(CustomerUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            throw new InvalidOperationException("Nome obrigatorio.");
        }

        if (request.CreditLimit < 0)
        {
            throw new InvalidOperationException("Limite de credito nao pode ser negativo.");
        }
    }

    private static string AppendText(string value, string suffix)
    {
        return string.IsNullOrWhiteSpace(value) ? suffix : $"{value} | {suffix}";
    }
}

public sealed class SupplierService : ISupplierService
{
    private readonly MaterialProDbContext _db;
    private readonly ISupplierRepository _suppliers;
    private readonly ISecurityService? _security;

    public SupplierService(MaterialProDbContext db, ISecurityService? security = null) : this(db, new EfSupplierRepository(db), security)
    {
    }

    public SupplierService(MaterialProDbContext db, ISupplierRepository suppliers, ISecurityService? security = null)
    {
        _db = db;
        _suppliers = suppliers;
        _security = security;
    }

    public Supplier Create(SupplierUpsertRequest request)
    {
        Validate(request);
        var code = NormalizeCode(request.Code);
        if (!string.IsNullOrWhiteSpace(code) && _suppliers.FindByCode(code) is not null)
        {
            throw new InvalidOperationException("Ja existe fornecedor com este codigo.");
        }

        var cnpj = request.Cnpj.Trim();
        if (!string.IsNullOrWhiteSpace(cnpj) && _suppliers.FindByCnpj(cnpj) is not null)
        {
            throw new InvalidOperationException("Ja existe fornecedor com este CNPJ.");
        }

        var entity = new Supplier();
        Apply(entity, request, string.IsNullOrWhiteSpace(code) ? NextCode() : code);
        _suppliers.Add(entity);
        Audit("Cadastro", entity);
        return entity;
    }

    public Supplier Update(Guid id, SupplierUpsertRequest request)
    {
        Validate(request);
        var entity = _suppliers.FindById(id) ?? throw new InvalidOperationException("Fornecedor nao encontrado.");
        var code = NormalizeCode(request.Code);
        var duplicateCode = string.IsNullOrWhiteSpace(code) ? null : _suppliers.FindByCode(code);
        if (duplicateCode is not null && duplicateCode.Id != id)
        {
            throw new InvalidOperationException("Ja existe fornecedor com este codigo.");
        }

        var cnpj = request.Cnpj.Trim();
        var duplicateCnpj = string.IsNullOrWhiteSpace(cnpj) ? null : _suppliers.FindByCnpj(cnpj);
        if (duplicateCnpj is not null && duplicateCnpj.Id != id)
        {
            throw new InvalidOperationException("Ja existe fornecedor com este CNPJ.");
        }

        Apply(entity, request, string.IsNullOrWhiteSpace(code) ? entity.Code : code);
        entity.UpdatedAtUtc = DateTime.UtcNow;
        _suppliers.Update(entity);
        Audit("Alteracao", entity);
        return entity;
    }

    public Supplier? FindById(Guid id) => _suppliers.FindById(id);

    public Supplier? FindByCodeOrCnpj(string value)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return _suppliers.FindByCode(normalized) ?? _suppliers.FindByCnpj(normalized);
    }

    public IReadOnlyList<Supplier> Search(SupplierSearchRequest request) => _suppliers.Search(request);

    public IReadOnlyList<Supplier> List() => Search(new SupplierSearchRequest());

    public Supplier Inactivate(Guid id)
    {
        var entity = _suppliers.FindById(id) ?? throw new InvalidOperationException("Fornecedor nao encontrado.");
        if (_db.Products.Any(x => x.SupplierId == id && x.IsActive) || _db.AccountsPayable.Any(x => x.SupplierId == id && x.Status != FinancialStatus.Cancelled))
        {
            throw new InvalidOperationException("Nao e permitido excluir fornecedor com produtos ou contas vinculadas. Use inativacao logica.");
        }

        entity.IsActive = false;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        _suppliers.Update(entity);
        Audit("Inativacao", entity);
        return entity;
    }

    public Supplier LinkProduct(Guid supplierId, Guid productId)
    {
        var supplier = _suppliers.FindById(supplierId) ?? throw new InvalidOperationException("Fornecedor nao encontrado.");
        var product = _db.Products.FirstOrDefault(x => x.Id == productId) ?? throw new InvalidOperationException("Produto nao encontrado.");
        product.SupplierId = supplierId;
        product.UpdatedAtUtc = DateTime.UtcNow;
        _db.SaveChanges();
        Audit("VinculoProduto", supplier);
        return supplier;
    }

    public IReadOnlyList<SupplierProductHistoryItem> Products(Guid supplierId)
    {
        var purchases = from item in _db.PurchaseItems.AsNoTracking()
                        join purchase in _db.Purchases.AsNoTracking() on item.PurchaseId equals purchase.Id
                        where purchase.SupplierId == supplierId
                        group new { item, purchase } by item.ProductId into g
                        select new
                        {
                            ProductId = g.Key,
                            Quantity = g.Sum(x => x.item.Quantity),
                            LastDate = g.Max(x => x.purchase.PurchasedAtUtc),
                            LastCost = g.OrderByDescending(x => x.purchase.PurchasedAtUtc).Select(x => x.item.UnitCost).FirstOrDefault()
                        };

        var history = purchases.ToDictionary(x => x.ProductId);
        return _db.Products.AsNoTracking()
            .Where(x => x.SupplierId == supplierId)
            .OrderBy(x => x.Name)
            .Select(x => new SupplierProductHistoryItem(
                x.Id,
                x.Sku,
                x.Name,
                history.ContainsKey(x.Id) ? history[x.Id].LastCost : x.CostPrice,
                history.ContainsKey(x.Id) ? history[x.Id].LastDate : null,
                history.ContainsKey(x.Id) ? history[x.Id].Quantity : 0m))
            .ToList();
    }

    public IReadOnlyList<SupplierPurchaseHistoryItem> PurchaseHistory(Guid supplierId)
    {
        return _db.Purchases.AsNoTracking()
            .Where(x => x.SupplierId == supplierId)
            .OrderByDescending(x => x.PurchasedAtUtc)
            .Select(x => new SupplierPurchaseHistoryItem(x.Id, x.Number, x.PurchasedAtUtc, x.TotalAmount, x.Notes))
            .ToList();
    }

    public SupplierFinancialSummary FinancialSummary(Guid supplierId)
    {
        var payables = Payables(supplierId);
        var now = DateTime.UtcNow;
        var open = payables.Where(x => x.Status == FinancialStatus.Open && x.DueDateUtc >= now).ToList();
        var paid = payables.Where(x => x.Status == FinancialStatus.Paid).ToList();
        var overdue = payables.Where(x => x.Status == FinancialStatus.Overdue || (x.Status == FinancialStatus.Open && x.DueDateUtc < now)).ToList();
        return new SupplierFinancialSummary(
            open.Sum(x => x.BalanceAmount),
            paid.Sum(x => x.PaidAmount),
            overdue.Sum(x => x.BalanceAmount),
            open.Count,
            paid.Count,
            overdue.Count);
    }

    public IReadOnlyList<AccountPayable> Payables(Guid supplierId)
    {
        var supplier = _suppliers.FindById(supplierId) ?? throw new InvalidOperationException("Fornecedor nao encontrado.");
        return _db.AccountsPayable.AsNoTracking()
            .Where(x => x.SupplierId == supplierId || x.SupplierName == supplier.Name || x.SupplierName == supplier.FantasyName || x.SupplierName == supplier.LegalName)
            .OrderByDescending(x => x.DueDateUtc)
            .ToList();
    }

    public AccountPayable CreatePayable(SupplierPayableRequest request)
    {
        var supplier = _suppliers.FindById(request.SupplierId) ?? throw new InvalidOperationException("Fornecedor nao encontrado.");
        var payable = new AccountPayable
        {
            SupplierId = supplier.Id,
            Number = request.Number.Trim(),
            SupplierName = DisplayName(supplier),
            Description = request.Description.Trim(),
            OriginalAmount = request.OriginalAmount,
            BalanceAmount = request.OriginalAmount,
            DueDateUtc = request.DueDateUtc,
            PaymentMethod = request.PaymentMethod.Trim(),
            Status = request.DueDateUtc.Date < DateTime.UtcNow.Date ? FinancialStatus.Overdue : FinancialStatus.Open
        };
        _db.AccountsPayable.Add(payable);
        _db.SaveChanges();
        Audit("ContaAPagar", supplier);
        return payable;
    }

    private void Apply(Supplier entity, SupplierUpsertRequest request, string code)
    {
        entity.Code = code;
        entity.PersonType = request.PersonType;
        entity.FantasyName = Default(request.FantasyName.Trim(), request.Name.Trim());
        entity.LegalName = Default(request.LegalName.Trim(), entity.FantasyName);
        entity.Name = entity.FantasyName;
        entity.Cnpj = request.Cnpj.Trim();
        entity.StateRegistration = request.StateRegistration.Trim();
        entity.MunicipalRegistration = request.MunicipalRegistration.Trim();
        entity.Phone = request.Phone.Trim();
        entity.MobilePhone = request.MobilePhone.Trim();
        entity.WhatsApp = request.WhatsApp.Trim();
        entity.Email = request.Email.Trim();
        entity.Website = request.Website.Trim();
        entity.ZipCode = request.ZipCode.Trim();
        entity.Address = request.Address.Trim();
        entity.AddressNumber = request.AddressNumber.Trim();
        entity.Complement = request.Complement.Trim();
        entity.District = request.District.Trim();
        entity.City = request.City.Trim();
        entity.State = request.State.Trim().ToUpperInvariant();
        entity.ContactName = request.ContactName.Trim();
        entity.ContactRole = request.ContactRole.Trim();
        entity.DefaultPaymentTermDays = request.DefaultPaymentTermDays;
        entity.PurchaseLimit = request.PurchaseLimit;
        entity.Notes = request.Notes.Trim();
        entity.IsActive = request.IsActive;
    }

    private string NextCode() => $"FOR-{(_db.Suppliers.Count() + 1):D6}";

    private void Audit(string action, Supplier supplier)
    {
        _security?.RecordAudit(new SecurityAuditRequest(null, SecurityEventType.Audit, "Fornecedores", action, nameof(Supplier), supplier.Id.ToString(), DisplayName(supplier), Environment.MachineName, string.Empty));
    }

    private static void Validate(SupplierUpsertRequest request)
    {
        var name = Default(request.FantasyName.Trim(), request.Name.Trim());
        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(request.LegalName))
        {
            throw new InvalidOperationException("Fornecedor sem nome nao e permitido.");
        }

        if (request.PersonType == PersonType.Juridica && string.IsNullOrWhiteSpace(request.Cnpj))
        {
            throw new InvalidOperationException("CNPJ obrigatorio para pessoa juridica.");
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            try
            {
                _ = new System.Net.Mail.MailAddress(request.Email);
            }
            catch
            {
                throw new InvalidOperationException("E-mail invalido.");
            }
        }

        if (request.PurchaseLimit < 0)
        {
            throw new InvalidOperationException("Limite de compra nao pode ser negativo.");
        }
    }

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();
    private static string Default(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;
    private static string DisplayName(Supplier supplier) => Default(supplier.FantasyName, Default(supplier.Name, supplier.LegalName));
}

public sealed class EfSupplierRepository : ISupplierRepository
{
    private readonly MaterialProDbContext _db;

    public EfSupplierRepository(MaterialProDbContext db) => _db = db;

    public void Add(Supplier supplier)
    {
        _db.Suppliers.Add(supplier);
        _db.SaveChanges();
    }

    public void Update(Supplier supplier)
    {
        _db.Suppliers.Update(supplier);
        _db.SaveChanges();
    }

    public Supplier? FindById(Guid id) => _db.Suppliers.FirstOrDefault(x => x.Id == id);
    public Supplier? FindByCode(string code) => _db.Suppliers.FirstOrDefault(x => x.Code.ToLower() == code.ToLower());
    public Supplier? FindByCnpj(string cnpj) => _db.Suppliers.FirstOrDefault(x => x.Cnpj != string.Empty && x.Cnpj.ToLower() == cnpj.ToLower());

    public IReadOnlyList<Supplier> Search(SupplierSearchRequest request)
    {
        var term = request.Term.Trim().ToLower();
        var city = request.City.Trim().ToLower();
        var state = request.State.Trim().ToUpperInvariant();
        var query = _db.Suppliers.AsNoTracking().AsQueryable();
        if (request.OnlyActive)
        {
            query = query.Where(x => x.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            query = query.Where(x => x.City.ToLower().Contains(city));
        }

        if (!string.IsNullOrWhiteSpace(state))
        {
            query = query.Where(x => x.State == state);
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(x =>
                x.Code.ToLower().Contains(term) ||
                x.FantasyName.ToLower().Contains(term) ||
                x.LegalName.ToLower().Contains(term) ||
                x.Name.ToLower().Contains(term) ||
                x.Cnpj.ToLower().Contains(term) ||
                x.Phone.ToLower().Contains(term) ||
                x.WhatsApp.ToLower().Contains(term) ||
                x.City.ToLower().Contains(term) ||
                x.State.ToLower().Contains(term));
        }

        return query.OrderBy(x => x.FantasyName).ThenBy(x => x.LegalName).ToList();
    }
}

public sealed class EfProductRepository : IProductRepository
{
    private readonly MaterialProDbContext _db;

    public EfProductRepository(MaterialProDbContext db) => _db = db;

    public void Add(Product product)
    {
        _db.Products.Add(product);
        _db.SaveChanges();
    }

    public void Update(Product product)
    {
        _db.Products.Update(product);
        _db.SaveChanges();
    }

    public Product? FindById(Guid id) => _db.Products.FirstOrDefault(x => x.Id == id);
    public Product? FindBySku(string sku) => _db.Products.FirstOrDefault(x => x.Sku.ToLower() == sku.ToLower());
    public Product? FindByBarcode(string barcode) => _db.Products.FirstOrDefault(x => x.Barcode != string.Empty && x.Barcode.ToLower() == barcode.ToLower());

    public IReadOnlyList<Product> Search(ProductSearchRequest request)
    {
        var term = request.Term.Trim().ToLower();
        var query = _db.Products.AsNoTracking().AsQueryable();
        if (request.OnlyActive)
        {
            query = query.Where(x => x.IsActive);
        }

        if (request.OnlyLowStock)
        {
            query = query.Where(x => x.StockQuantity <= x.MinimumStock);
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(x =>
                x.Sku.ToLower().Contains(term) ||
                x.Name.ToLower().Contains(term) ||
                x.Barcode.ToLower().Contains(term) ||
                x.Category.ToLower().Contains(term) ||
                x.Brand.ToLower().Contains(term));
        }

        return query.OrderBy(x => x.Name).ToList();
    }
}

public sealed class ProductService : IProductService
{
    private readonly IProductRepository _products;

    public ProductService(MaterialProDbContext db) : this(new EfProductRepository(db))
    {
    }

    public ProductService(IProductRepository products)
    {
        _products = products;
    }

    public Product Create(ProductUpsertRequest request)
    {
        Validate(request);
        if (_products.FindBySku(request.Sku.Trim()) is not null)
        {
            throw new InvalidOperationException("Ja existe produto com este SKU.");
        }

        var entity = new Product
        {
            Sku = request.Sku.Trim(),
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            Category = request.Category.Trim(),
            Brand = request.Brand.Trim(),
            Unit = request.Unit.Trim(),
            SalePrice = request.SalePrice,
            CostPrice = request.CostPrice,
            MinimumStock = request.MinimumStock,
            Barcode = request.Barcode.Trim(),
            Ncm = request.Ncm.Trim(),
            Location = request.Location.Trim(),
            SupplierId = request.SupplierId
        };
        _products.Add(entity);
        return entity;
    }

    public Product Update(Guid id, ProductUpsertRequest request)
    {
        Validate(request);
        var entity = _products.FindById(id) ?? throw new InvalidOperationException("Produto nao encontrado.");
        var duplicate = _products.FindBySku(request.Sku.Trim());
        if (duplicate is not null && duplicate.Id != id)
        {
            throw new InvalidOperationException("Ja existe produto com este SKU.");
        }

        entity.Sku = request.Sku.Trim();
        entity.Name = request.Name.Trim();
        entity.Description = request.Description.Trim();
        entity.Category = request.Category.Trim();
        entity.Brand = request.Brand.Trim();
        entity.Unit = request.Unit.Trim();
        entity.SalePrice = request.SalePrice;
        entity.CostPrice = request.CostPrice;
        entity.MinimumStock = request.MinimumStock;
        entity.Barcode = request.Barcode.Trim();
        entity.Ncm = request.Ncm.Trim();
        entity.Location = request.Location.Trim();
        entity.SupplierId = request.SupplierId;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        _products.Update(entity);
        return entity;
    }

    public Product? FindById(Guid id) => _products.FindById(id);

    public Product? FindBySkuOrBarcode(string value)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return _products.FindBySku(normalized) ?? _products.FindByBarcode(normalized);
    }

    public IReadOnlyList<Product> Search(ProductSearchRequest request) => _products.Search(request);

    public IReadOnlyList<Product> List() => Search(new ProductSearchRequest());

    private static void Validate(ProductUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Sku))
        {
            throw new InvalidOperationException("SKU obrigatorio.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Nome obrigatorio.");
        }

        if (request.SalePrice < 0 || request.CostPrice < 0 || request.MinimumStock < 0)
        {
            throw new InvalidOperationException("Valores do produto nao podem ser negativos.");
        }
    }
}

public sealed class InventoryService : IInventoryService
{
    private readonly MaterialProDbContext _db;
    private readonly ISecurityService? _security;

    public InventoryService(MaterialProDbContext db, ISecurityService? security = null)
    {
        _db = db;
        _security = security;
    }

    public StockMovement Move(Guid productId, decimal quantity, string reason, string reference)
    {
        var type = quantity >= 0 ? StockMovementType.ManualEntry : StockMovementType.AdjustmentExit;
        return Register(new StockMoveRequest(productId, quantity, type, reason, reference, AllowNegative: true));
    }

    public decimal GetStock(Guid productId)
    {
        return _db.Products.Where(x => x.Id == productId).Select(x => x.StockQuantity).FirstOrDefault();
    }

    public StockMovement EnterStock(StockMoveRequest request)
    {
        var quantity = Math.Abs(request.Quantity);
        return Register(request with { Quantity = quantity });
    }

    public StockMovement ExitStock(StockMoveRequest request)
    {
        var quantity = -Math.Abs(request.Quantity);
        return Register(request with { Quantity = quantity });
    }

    public StockMovement AdjustStock(StockAdjustRequest request)
    {
        if (!request.UserCanAdjust)
        {
            throw new InvalidOperationException("Usuario sem permissao para ajuste de estoque.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new InvalidOperationException("Motivo obrigatorio para ajuste de estoque.");
        }

        var product = _db.Products.FirstOrDefault(x => x.Id == request.ProductId) ?? throw new InvalidOperationException("Produto nao encontrado.");
        var difference = request.NewStock - product.StockQuantity;
        var type = difference >= 0 ? StockMovementType.AdjustmentEntry : StockMovementType.AdjustmentExit;
        return Register(new StockMoveRequest(request.ProductId, difference, type, request.Reason, "AJUSTE", request.UserId, request.Warehouse, request.Reason, AllowNegative: true));
    }

    public StockInventory StartInventory(StockInventoryRequest request)
    {
        var inventory = new StockInventory
        {
            UserId = request.UserId,
            Observation = request.Observation.Trim(),
            InventoryDateUtc = DateTime.UtcNow,
            Status = StockInventoryStatus.Open
        };
        _db.StockInventories.Add(inventory);
        _db.SaveChanges();
        Audit(request.UserId, "Inventario", nameof(StockInventory), inventory.Id.ToString(), inventory.Observation);
        return inventory;
    }

    public StockInventoryItem CountInventoryItem(Guid inventoryId, StockInventoryItemRequest request)
    {
        var inventory = _db.StockInventories.FirstOrDefault(x => x.Id == inventoryId) ?? throw new InvalidOperationException("Inventario nao encontrado.");
        if (inventory.Status is StockInventoryStatus.Closed or StockInventoryStatus.Cancelled)
        {
            throw new InvalidOperationException("Inventario fechado nao pode receber contagem.");
        }

        var product = _db.Products.FirstOrDefault(x => x.Id == request.ProductId) ?? throw new InvalidOperationException("Produto nao encontrado.");
        var item = _db.StockInventoryItems.FirstOrDefault(x => x.InventoryId == inventoryId && x.ProductId == request.ProductId);
        if (item is null)
        {
            item = new StockInventoryItem { InventoryId = inventoryId, ProductId = request.ProductId };
            _db.StockInventoryItems.Add(item);
        }

        item.SystemStock = product.StockQuantity;
        item.CountedStock = request.CountedStock;
        item.Difference = request.CountedStock - product.StockQuantity;
        item.UpdatedAtUtc = DateTime.UtcNow;
        inventory.Status = StockInventoryStatus.Counted;
        inventory.UpdatedAtUtc = DateTime.UtcNow;
        _db.SaveChanges();
        return item;
    }

    public StockInventory CloseInventory(Guid inventoryId, bool applyAdjustments, Guid? userId = null)
    {
        var inventory = _db.StockInventories.FirstOrDefault(x => x.Id == inventoryId) ?? throw new InvalidOperationException("Inventario nao encontrado.");
        var items = _db.StockInventoryItems.Where(x => x.InventoryId == inventoryId).ToList();
        if (applyAdjustments)
        {
            foreach (var item in items.Where(x => x.Difference != 0))
            {
                Register(new StockMoveRequest(item.ProductId, item.Difference, StockMovementType.InventoryAdjustment, "Ajuste automatico de inventario", inventory.Id.ToString(), userId, Observation: inventory.Observation, AllowNegative: true), save: false);
            }
        }

        inventory.Status = StockInventoryStatus.Closed;
        inventory.UpdatedAtUtc = DateTime.UtcNow;
        _db.SaveChanges();
        Audit(userId ?? inventory.UserId, "FechamentoInventario", nameof(StockInventory), inventory.Id.ToString(), $"Itens: {items.Count}");
        return inventory;
    }

    public StockTransfer Transfer(StockTransferRequest request)
    {
        if (request.Quantity <= 0)
        {
            throw new InvalidOperationException("Quantidade de transferencia deve ser maior que zero.");
        }

        var transfer = new StockTransfer
        {
            ProductId = request.ProductId,
            Quantity = request.Quantity,
            SourceWarehouse = request.SourceWarehouse.Trim(),
            DestinationWarehouse = request.DestinationWarehouse.Trim(),
            UserId = request.UserId,
            TransferDateUtc = DateTime.UtcNow,
            Status = StockTransferStatus.Completed,
            Observation = request.Observation.Trim()
        };
        _db.StockTransfers.Add(transfer);
        Register(new StockMoveRequest(request.ProductId, -request.Quantity, StockMovementType.TransferExit, "Transferencia de estoque", transfer.Id.ToString(), request.UserId, transfer.SourceWarehouse, transfer.Observation, request.AllowNegative), save: false);
        Register(new StockMoveRequest(request.ProductId, request.Quantity, StockMovementType.TransferEntry, "Transferencia de estoque", transfer.Id.ToString(), request.UserId, transfer.DestinationWarehouse, transfer.Observation, AllowNegative: true), save: false);
        _db.SaveChanges();
        Audit(request.UserId, "Transferencia", nameof(StockTransfer), transfer.Id.ToString(), $"{transfer.SourceWarehouse} > {transfer.DestinationWarehouse}");
        return transfer;
    }

    public StockReservation Reserve(StockReservationRequest request)
    {
        if (request.Quantity <= 0)
        {
            throw new InvalidOperationException("Quantidade de reserva deve ser maior que zero.");
        }

        var product = _db.Products.FirstOrDefault(x => x.Id == request.ProductId) ?? throw new InvalidOperationException("Produto nao encontrado.");
        if (product.StockQuantity - product.ReservedStock < request.Quantity)
        {
            throw new InvalidOperationException("Estoque disponivel insuficiente para reserva.");
        }

        product.ReservedStock += request.Quantity;
        product.UpdatedAtUtc = DateTime.UtcNow;
        var reservation = new StockReservation
        {
            ProductId = request.ProductId,
            Quantity = request.Quantity,
            Source = request.Source.Trim(),
            Reference = request.Reference.Trim(),
            UserId = request.UserId,
            Observation = request.Observation.Trim(),
            Status = StockReservationStatus.Active
        };
        _db.StockReservations.Add(reservation);
        AddMovement(product, request.Quantity, StockMovementType.Reservation, "Reserva de estoque", request.Reference, request.UserId, "Loja", request.Observation);
        _db.SaveChanges();
        Audit(request.UserId, "Reserva", nameof(StockReservation), reservation.Id.ToString(), request.Reference);
        return reservation;
    }

    public StockReservation ReleaseReservation(Guid reservationId, Guid? userId = null, string observation = "")
    {
        var reservation = _db.StockReservations.FirstOrDefault(x => x.Id == reservationId) ?? throw new InvalidOperationException("Reserva nao encontrada.");
        if (reservation.Status != StockReservationStatus.Active)
        {
            throw new InvalidOperationException("Reserva nao esta ativa.");
        }

        var product = _db.Products.First(x => x.Id == reservation.ProductId);
        product.ReservedStock = Math.Max(0, product.ReservedStock - reservation.Quantity);
        product.UpdatedAtUtc = DateTime.UtcNow;
        reservation.Status = StockReservationStatus.Released;
        reservation.ReleasedAtUtc = DateTime.UtcNow;
        reservation.Observation = string.IsNullOrWhiteSpace(observation) ? reservation.Observation : observation.Trim();
        reservation.UpdatedAtUtc = DateTime.UtcNow;
        AddMovement(product, -reservation.Quantity, StockMovementType.ReservationRelease, "Liberacao de reserva", reservation.Reference, userId, "Loja", reservation.Observation);
        _db.SaveChanges();
        Audit(userId, "LiberacaoReserva", nameof(StockReservation), reservation.Id.ToString(), reservation.Reference);
        return reservation;
    }

    public IReadOnlyList<StockPositionItem> Query(StockQueryRequest request)
    {
        var term = request.Term.Trim().ToLower();
        var category = request.Category.Trim().ToLower();
        var brand = request.Brand.Trim().ToLower();
        var suppliers = _db.Suppliers.AsNoTracking().ToDictionary(x => x.Id, x => string.IsNullOrWhiteSpace(x.FantasyName) ? x.Name : x.FantasyName);
        var entries = EntryTypes();
        var exits = ExitTypes();

        var query = _db.Products.AsNoTracking().Where(x => x.IsActive).AsQueryable();
        if (request.SupplierId.HasValue)
        {
            query = query.Where(x => x.SupplierId == request.SupplierId);
        }
        if (request.OnlyLowStock)
        {
            query = query.Where(x => x.StockQuantity <= x.MinimumStock);
        }
        if (request.OnlyZeroStock)
        {
            query = query.Where(x => x.StockQuantity <= 0);
        }
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(x => x.Category.ToLower().Contains(category));
        }
        if (!string.IsNullOrWhiteSpace(brand))
        {
            query = query.Where(x => x.Brand.ToLower().Contains(brand));
        }
        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(x => x.Sku.ToLower().Contains(term) || x.Barcode.ToLower().Contains(term) || x.Name.ToLower().Contains(term) || x.Category.ToLower().Contains(term) || x.Brand.ToLower().Contains(term));
        }

        var products = query.OrderBy(x => x.Name).ToList();
        var productIds = products.Select(x => x.Id).ToHashSet();
        var movementGroups = _db.StockMovements.AsNoTracking().Where(x => productIds.Contains(x.ProductId)).ToList().GroupBy(x => x.ProductId).ToDictionary(x => x.Key);

        return products.Select(product =>
        {
            movementGroups.TryGetValue(product.Id, out var group);
            var lastEntry = group?.Where(x => entries.Contains(x.Type)).OrderByDescending(x => x.MovementAtUtc).FirstOrDefault()?.MovementAtUtc;
            var lastExit = group?.Where(x => exits.Contains(x.Type)).OrderByDescending(x => x.MovementAtUtc).FirstOrDefault()?.MovementAtUtc;
            var supplier = product.SupplierId.HasValue && suppliers.TryGetValue(product.SupplierId.Value, out var name) ? name : string.Empty;
            return new StockPositionItem(product.Id, product.Sku, product.Name, product.Category, product.Brand, supplier, product.StockQuantity, product.ReservedStock, product.StockQuantity - product.ReservedStock, product.MinimumStock, lastEntry, lastExit);
        }).ToList();
    }

    public IReadOnlyList<StockMovement> Movements(Guid? productId = null)
    {
        var query = _db.StockMovements.AsNoTracking().AsQueryable();
        if (productId.HasValue)
        {
            query = query.Where(x => x.ProductId == productId);
        }

        return query.OrderByDescending(x => x.MovementAtUtc).ToList();
    }

    public StockDashboardSummary Dashboard()
    {
        var products = _db.Products.AsNoTracking().Where(x => x.IsActive).ToList();
        return new StockDashboardSummary(
            products.Count,
            products.Sum(x => x.StockQuantity),
            products.Count(x => x.StockQuantity <= x.MinimumStock),
            products.Count(x => x.StockQuantity <= 0),
            _db.StockMovements.AsNoTracking().OrderByDescending(x => x.MovementAtUtc).Take(10).ToList());
    }

    private StockMovement Register(StockMoveRequest request, bool save = true)
    {
        if (request.Quantity == 0)
        {
            throw new InvalidOperationException("Quantidade de estoque deve ser diferente de zero.");
        }

        var product = _db.Products.FirstOrDefault(x => x.Id == request.ProductId) ?? throw new InvalidOperationException("Produto nao encontrado.");
        if (!request.AllowNegative && product.StockQuantity + request.Quantity < 0)
        {
            throw new InvalidOperationException("Estoque negativo nao permitido sem autorizacao.");
        }

        var movement = AddMovement(product, request.Quantity, request.Type, request.Reason, request.Reference, request.UserId, request.Warehouse, request.Observation);
        if (save)
        {
            _db.SaveChanges();
            Audit(request.UserId, "Movimento", nameof(StockMovement), movement.Id.ToString(), $"{product.Sku}: {request.Quantity}");
        }

        return movement;
    }

    private StockMovement AddMovement(Product product, decimal quantity, StockMovementType type, string reason, string reference, Guid? userId, string warehouse, string observation)
    {
        var previous = product.StockQuantity;
        product.StockQuantity += quantity;
        product.UpdatedAtUtc = DateTime.UtcNow;
        var movement = new StockMovement
        {
            ProductId = product.Id,
            Type = type,
            Quantity = quantity,
            PreviousStock = previous,
            CurrentStock = product.StockQuantity,
            UserId = userId,
            Reason = reason.Trim(),
            Reference = reference.Trim(),
            Warehouse = string.IsNullOrWhiteSpace(warehouse) ? "Loja" : warehouse.Trim(),
            Observation = observation.Trim(),
            MovementAtUtc = DateTime.UtcNow
        };
        _db.StockMovements.Add(movement);
        return movement;
    }

    private void Audit(Guid? userId, string action, string entityName, string entityId, string details)
    {
        _security?.RecordAudit(new SecurityAuditRequest(userId, SecurityEventType.Audit, "Estoque", action, entityName, entityId, details, Environment.MachineName, string.Empty));
    }

    internal static HashSet<StockMovementType> EntryTypes() => new()
    {
        StockMovementType.PurchaseEntry,
        StockMovementType.ManualEntry,
        StockMovementType.ReturnEntry,
        StockMovementType.AdjustmentEntry,
        StockMovementType.TransferEntry,
        StockMovementType.InventoryAdjustment
    };

    internal static HashSet<StockMovementType> ExitTypes() => new()
    {
        StockMovementType.SaleExit,
        StockMovementType.LossExit,
        StockMovementType.BreakageExit,
        StockMovementType.AdjustmentExit,
        StockMovementType.TransferExit
    };
}

public sealed class BudgetService : IBudgetService
{
    private readonly MaterialProDbContext _db;

    public BudgetService(MaterialProDbContext db) => _db = db;

    public Budget CreateBudget(BudgetCreateRequest request, IEnumerable<BudgetItemRequest> items)
    {
        var budget = new Budget
        {
            Number = request.Number.Trim(),
            CustomerId = request.CustomerId,
            DiscountAmount = request.DiscountAmount,
            ValidUntilUtc = request.ValidUntilUtc,
            Notes = request.Notes.Trim(),
            Status = BudgetStatus.Draft
        };

        var list = items.ToList();
        budget.TotalAmount = list.Sum(x => (x.Quantity * x.UnitPrice) - x.DiscountAmount);

        _db.Budgets.Add(budget);
        _db.BudgetItems.AddRange(list.Select(item => new BudgetItem
        {
            BudgetId = budget.Id,
            ProductId = item.ProductId,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice,
            DiscountAmount = item.DiscountAmount
        }));
        _db.SaveChanges();
        return budget;
    }

    public IReadOnlyList<Budget> List() => _db.Budgets.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).ToList();
}

public sealed class SalesService : ISalesService
{
    private readonly MaterialProDbContext _db;

    public SalesService(MaterialProDbContext db) => _db = db;

    public Sale CreateSale(SaleCreateRequest request, IEnumerable<SaleItemRequest> items)
    {
        var saleItems = items.ToList();
        var sale = new Sale
        {
            CustomerId = request.CustomerId ?? Guid.Empty,
            PaymentMethod = request.PaymentMethod.Trim(),
            DiscountAmount = request.DiscountAmount,
            PaidAmount = request.PaidAmount,
            ReceiptNumber = request.ReceiptNumber.Trim(),
            SoldAtUtc = DateTime.UtcNow,
            Status = SaleStatus.Finalizada
        };

        sale.TotalAmount = saleItems.Sum(x => (x.Quantity * x.UnitPrice) - x.DiscountAmount);
        sale.ChangeAmount = Math.Max(0, sale.PaidAmount - sale.TotalAmount);

        _db.Sales.Add(sale);
        _db.SaleItems.AddRange(saleItems.Select(item => new SaleItem
        {
            SaleId = sale.Id,
            ProductId = item.ProductId,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice,
            DiscountAmount = item.DiscountAmount
        }));

        foreach (var item in saleItems)
        {
            var product = _db.Products.First(x => x.Id == item.ProductId);
            product.StockQuantity -= item.Quantity;
            product.UpdatedAtUtc = DateTime.UtcNow;
        }

        _db.SaveChanges();
        return sale;
    }

    public IReadOnlyList<Sale> List() => _db.Sales.AsNoTracking().OrderByDescending(x => x.SoldAtUtc).ToList();
}

public sealed class CashService : ICashService
{
    private readonly MaterialProDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ISecurityService? _security;

    public CashService(MaterialProDbContext db, IPasswordHasher? hasher = null, ISecurityService? security = null)
    {
        _db = db;
        _hasher = hasher ?? new Sha256PasswordHasher();
        _security = security;
    }

    public CashSession Open(CashOpenRequest request)
    {
        if (_db.CashSessions.Any(x => x.ClosedAtUtc == null && x.OpenedByUserId == request.OpenedByUserId))
        {
            throw new InvalidOperationException("Usuario ja possui caixa aberto.");
        }

        var session = new CashSession
        {
            Code = $"CX-{DateTime.UtcNow:yyyyMMddHHmmss}",
            OpeningAmount = request.OpeningAmount,
            CurrentAmount = request.OpeningAmount,
            CashAmount = request.OpeningAmount,
            OpenedByUserId = request.OpenedByUserId,
            OpenedAtUtc = DateTime.UtcNow,
            Status = CashSessionStatus.Aberto
        };

        _db.CashSessions.Add(session);
        _db.CashMovements.Add(new CashMovement
        {
            CashSessionId = session.Id,
            Type = CashMovementType.Opening,
            Origin = "CAIXA",
            Amount = request.OpeningAmount,
            Description = "Abertura de caixa",
            UserId = request.OpenedByUserId,
            PaymentMethod = "DINHEIRO",
            MovementAtUtc = DateTime.UtcNow
        });
        _db.SaveChanges();
        Audit(request.OpenedByUserId, "Abertura", session.Id.ToString(), session.Code);
        return session;
    }

    public CashMovement RegisterMovement(CashMovementRequest request)
    {
        var movement = AddMovement(request.CashSessionId, request.Type, request.Amount, request.Description, request.SaleId, null, null, PaymentFromType(request.Type), "SISTEMA", string.Empty);
        _db.SaveChanges();
        return movement;
    }

    public CashSession? ActiveSession()
    {
        return _db.CashSessions.AsNoTracking().FirstOrDefault(x => x.ClosedAtUtc == null && x.Status == CashSessionStatus.Aberto);
    }

    public CashMovement Supply(CashSupplyRequest request)
    {
        if (request.Amount <= 0) throw new InvalidOperationException("Valor de suprimento deve ser maior que zero.");
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new InvalidOperationException("Motivo obrigatorio.");
        var movement = AddMovement(request.CashSessionId, CashMovementType.Supply, request.Amount, request.Reason, null, null, request.UserId, "DINHEIRO", "SUPRIMENTO", request.Reason);
        _db.SaveChanges();
        Audit(request.UserId, "Suprimento", request.CashSessionId.ToString(), request.Amount.ToString("N2"));
        return movement;
    }

    public CashMovement Withdraw(CashWithdrawalRequest request)
    {
        if (request.Amount <= 0) throw new InvalidOperationException("Valor de sangria deve ser maior que zero.");
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new InvalidOperationException("Motivo obrigatorio.");
        EnsureManagerPassword(request.ManagerPassword);
        var session = OpenSession(request.CashSessionId);
        if (AvailableCash(session) < request.Amount)
        {
            throw new InvalidOperationException("Sangria maior que saldo em dinheiro.");
        }

        var movement = AddMovement(request.CashSessionId, CashMovementType.Withdrawal, -request.Amount, request.Reason, null, null, request.UserId, "DINHEIRO", "SANGRIA", request.Reason);
        _db.SaveChanges();
        Audit(request.UserId, "Sangria", request.CashSessionId.ToString(), request.Amount.ToString("N2"));
        return movement;
    }

    public CashSession Close(CashCloseRequest request)
    {
        var session = OpenSession(request.CashSessionId);
        if (_db.Sales.Any(x => x.CashSessionId == session.Id && x.Status == SaleStatus.Aberta))
        {
            throw new InvalidOperationException("Nao e permitido fechar caixa com venda aberta.");
        }

        var reported = request.CountedCash + request.CountedPix + request.CountedDebitCard + request.CountedCreditCard + session.CreditSaleAmount;
        var expected = ExpectedTotal(session);
        session.ReportedAmount = reported;
        session.ClosingAmount = request.CountedCash;
        session.DifferenceAmount = reported - expected;
        session.ClosedByUserId = request.UserId;
        session.ClosedAtUtc = DateTime.UtcNow;
        session.Status = CashSessionStatus.Fechado;
        session.Observation = request.Observation.Trim();
        session.UpdatedAtUtc = DateTime.UtcNow;
        _db.CashMovements.Add(new CashMovement
        {
            CashSessionId = session.Id,
            Type = CashMovementType.Close,
            Origin = "CAIXA",
            Amount = reported,
            Description = "Fechamento de caixa",
            UserId = request.UserId,
            MovementAtUtc = DateTime.UtcNow,
            Observation = request.Observation.Trim()
        });
        _db.SaveChanges();
        Audit(request.UserId, "Fechamento", session.Id.ToString(), $"Diferenca {session.DifferenceAmount:N2}");
        return session;
    }

    public IReadOnlyList<CashSession> History(CashHistoryRequest request)
    {
        var query = _db.CashSessions.AsNoTracking().AsQueryable();
        if (request.FromUtc.HasValue) query = query.Where(x => x.OpenedAtUtc >= request.FromUtc.Value);
        if (request.ToUtc.HasValue) query = query.Where(x => x.OpenedAtUtc <= request.ToUtc.Value);
        if (request.OperatorId.HasValue) query = query.Where(x => x.OpenedByUserId == request.OperatorId.Value);
        if (request.Status.HasValue) query = query.Where(x => x.Status == request.Status.Value);
        if (!string.IsNullOrWhiteSpace(request.Code)) query = query.Where(x => x.Code.Contains(request.Code.Trim()));
        return query.OrderByDescending(x => x.OpenedAtUtc).ToList();
    }

    public IReadOnlyList<CashMovement> Movements(Guid cashSessionId)
        => _db.CashMovements.AsNoTracking().Where(x => x.CashSessionId == cashSessionId && x.IsActive).OrderByDescending(x => x.MovementAtUtc).ToList();

    public CashDashboardSummary Dashboard()
    {
        var today = DateTime.UtcNow.Date;
        var sessions = _db.CashSessions.AsNoTracking().Where(x => x.OpenedAtUtc >= today).ToList();
        var open = sessions.FirstOrDefault(x => x.Status == CashSessionStatus.Aberto);
        return new CashDashboardSummary(
            open is not null,
            open?.Code ?? string.Empty,
            sessions.Sum(x => x.TotalSalesAmount),
            sessions.Sum(x => x.CashAmount),
            sessions.Sum(x => x.PixAmount),
            sessions.Sum(x => x.DebitCardAmount),
            sessions.Sum(x => x.CreditCardAmount),
            sessions.Sum(x => x.WithdrawalAmount),
            sessions.Sum(x => x.SupplyAmount),
            sessions.Sum(x => x.DifferenceAmount));
    }

    public byte[] PrintOpening(Guid cashSessionId, InternalPaperFormat format = InternalPaperFormat.Thermal80)
    {
        var session = _db.CashSessions.AsNoTracking().First(x => x.Id == cashSessionId);
        return DocumentPdf("Abertura de caixa", session.Code, session.OpeningAmount, format, [$"Operador: {session.OpenedByUserId}", $"Data: {session.OpenedAtUtc:dd/MM/yyyy HH:mm}", $"Valor inicial: {session.OpeningAmount:C}"]);
    }

    public byte[] PrintMovement(Guid movementId, InternalPaperFormat format = InternalPaperFormat.Thermal80)
    {
        var movement = _db.CashMovements.AsNoTracking().First(x => x.Id == movementId);
        return DocumentPdf(movement.Type.ToString(), movement.Id.ToString(), movement.Amount, format, [$"Data: {movement.MovementAtUtc:dd/MM/yyyy HH:mm}", $"Forma: {movement.PaymentMethod}", $"Valor: {movement.Amount:C}", $"Obs: {movement.Observation}"]);
    }

    public byte[] PrintClosing(Guid cashSessionId, InternalPaperFormat format = InternalPaperFormat.A4)
    {
        var session = _db.CashSessions.AsNoTracking().First(x => x.Id == cashSessionId);
        return DocumentPdf("Fechamento de caixa", session.Code, session.ReportedAmount, format,
        [
            $"Abertura: {session.OpenedAtUtc:dd/MM/yyyy HH:mm}",
            $"Fechamento: {session.ClosedAtUtc:dd/MM/yyyy HH:mm}",
            $"Dinheiro: {session.CashAmount:C}",
            $"PIX: {session.PixAmount:C}",
            $"Debito: {session.DebitCardAmount:C}",
            $"Credito: {session.CreditCardAmount:C}",
            $"Prazo: {session.CreditSaleAmount:C}",
            $"Suprimentos: {session.SupplyAmount:C}",
            $"Sangrias: {session.WithdrawalAmount:C}",
            $"Informado: {session.ReportedAmount:C}",
            $"Diferenca: {session.DifferenceAmount:C}"
        ]);
    }

    private CashMovement AddMovement(Guid sessionId, CashMovementType type, decimal amount, string description, Guid? saleId, Guid? duplicateId, Guid? userId, string paymentMethod, string origin, string observation)
    {
        var session = OpenSession(sessionId);
        var normalizedPayment = NormalizePayment(paymentMethod);
        ApplyTotals(session, type, amount, normalizedPayment);
        session.UpdatedAtUtc = DateTime.UtcNow;
        var movement = new CashMovement
        {
            CashSessionId = session.Id,
            Type = type,
            Origin = origin,
            Amount = amount,
            Description = description.Trim(),
            SaleId = saleId,
            DuplicateId = duplicateId,
            UserId = userId,
            PaymentMethod = normalizedPayment,
            MovementAtUtc = DateTime.UtcNow,
            Observation = observation.Trim()
        };
        _db.CashMovements.Add(movement);
        return movement;
    }

    private CashSession OpenSession(Guid id)
    {
        var session = _db.CashSessions.FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException("Caixa nao encontrado.");
        if (session.Status != CashSessionStatus.Aberto || session.ClosedAtUtc.HasValue)
        {
            throw new InvalidOperationException("Caixa fechado nao pode receber movimento.");
        }
        return session;
    }

    private static void ApplyTotals(CashSession session, CashMovementType type, decimal amount, string payment)
    {
        if (type == CashMovementType.Sale || type == CashMovementType.DuplicateReceipt)
        {
            session.TotalSalesAmount += type == CashMovementType.Sale ? amount : 0;
            switch (payment)
            {
                case "DINHEIRO": session.CashAmount += amount; break;
                case "PIX": session.PixAmount += amount; break;
                case "CARTAO_DEBITO": session.DebitCardAmount += amount; break;
                case "CARTAO_CREDITO": session.CreditCardAmount += amount; break;
                case "PRAZO": session.CreditSaleAmount += amount; break;
            }
        }
        else if (type == CashMovementType.Supply)
        {
            session.SupplyAmount += amount;
            session.CashAmount += amount;
        }
        else if (type == CashMovementType.Withdrawal)
        {
            var value = Math.Abs(amount);
            session.WithdrawalAmount += value;
            session.CashAmount -= value;
        }
        else if (type == CashMovementType.Cancellation)
        {
            session.TotalSalesAmount += amount;
            if (payment == "DINHEIRO") session.CashAmount += amount;
            if (payment == "PIX") session.PixAmount += amount;
            if (payment == "CARTAO_DEBITO") session.DebitCardAmount += amount;
            if (payment == "CARTAO_CREDITO") session.CreditCardAmount += amount;
            if (payment == "PRAZO") session.CreditSaleAmount += amount;
        }

        session.CurrentAmount = session.CashAmount;
    }

    private void EnsureManagerPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password)) throw new InvalidOperationException("Senha de gerente obrigatoria.");
        var ok = _db.Users.Where(x => x.IsActive && (x.Role == UserRole.Admin || x.Role == UserRole.Manager)).AsEnumerable().Any(x => _hasher.Verify(password, x.PasswordSalt, x.PasswordHash));
        if (!ok) throw new InvalidOperationException("Senha de gerente invalida.");
    }

    private void Audit(Guid? userId, string action, string entityId, string details)
    {
        _security?.RecordAudit(new SecurityAuditRequest(userId, SecurityEventType.Audit, "Caixa", action, nameof(CashSession), entityId, details, Environment.MachineName, string.Empty));
    }

    private static decimal AvailableCash(CashSession session) => session.CashAmount;
    private static decimal ExpectedTotal(CashSession session) => session.CashAmount + session.PixAmount + session.DebitCardAmount + session.CreditCardAmount + session.CreditSaleAmount;
    private static string PaymentFromType(CashMovementType type) => type == CashMovementType.Opening || type == CashMovementType.Supply || type == CashMovementType.Withdrawal ? "DINHEIRO" : string.Empty;
    private static string NormalizePayment(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();
    private static byte[] DocumentPdf(string kind, string number, decimal total, InternalPaperFormat format, IEnumerable<string> lines)
        => new InternalDocumentService().GeneratePdf(new InternalDocumentRequest(InternalDocumentKind.PaymentProof, format, number, "Caixa", kind, total, "CAIXA", "Documento interno", lines));
}

public sealed class FinancialService : IFinancialService
{
    private readonly MaterialProDbContext _db;

    public FinancialService(MaterialProDbContext db) => _db = db;

    public AccountPayable CreatePayable(AccountPayableRequest request)
    {
        if (request.OriginalAmount <= 0) throw new InvalidOperationException("Valor da conta a pagar deve ser maior que zero.");
        if (_db.AccountsPayable.Any(x => x.Number == request.Number.Trim())) throw new InvalidOperationException("Conta a pagar duplicada.");
        var entity = new AccountPayable
        {
            Number = request.Number.Trim(),
            SupplierName = request.SupplierName.Trim(),
            Description = request.Description.Trim(),
            Category = "Fornecedor",
            IssueDateUtc = DateTime.UtcNow,
            OriginalAmount = request.OriginalAmount,
            PaidAmount = 0,
            BalanceAmount = request.OriginalAmount,
            DueDateUtc = request.DueDateUtc,
            PaymentMethod = request.PaymentMethod.Trim()
        };
        _db.AccountsPayable.Add(entity);
        Log("Cadastro conta pagar", entity.Number, entity.OriginalAmount, entity.Description, null);
        _db.SaveChanges();
        return entity;
    }

    public AccountReceivable CreateReceivable(AccountReceivableRequest request)
    {
        if (request.OriginalAmount <= 0) throw new InvalidOperationException("Valor da conta a receber deve ser maior que zero.");
        if (_db.AccountsReceivable.Any(x => x.Number == request.Number.Trim())) throw new InvalidOperationException("Conta a receber duplicada.");
        var customerName = request.CustomerName.Trim();
        if (request.CustomerId.HasValue && string.IsNullOrWhiteSpace(customerName))
        {
            customerName = _db.Customers.Where(x => x.Id == request.CustomerId.Value).Select(x => x.FullName).FirstOrDefault() ?? string.Empty;
        }

        var entity = new AccountReceivable
        {
            Number = request.Number.Trim(),
            CustomerId = request.CustomerId,
            SaleId = request.SaleId,
            CustomerName = customerName,
            Description = request.Description.Trim(),
            IssueDateUtc = DateTime.UtcNow,
            OriginalAmount = request.OriginalAmount,
            BalanceAmount = request.OriginalAmount,
            DueDateUtc = request.DueDateUtc,
            PaymentMethod = request.ReceiveMethod.Trim()
        };
        _db.AccountsReceivable.Add(entity);
        Log("Cadastro conta receber", entity.Number, entity.OriginalAmount, entity.Description, null);
        _db.SaveChanges();
        return entity;
    }

    public AccountPayable PayableBaixa(Guid id, decimal amount, string paymentMethod)
        => SettlePayable(new FinancialSettlementRequest(id, FinancialType.Payable, amount, PaymentMethod: paymentMethod));

    public AccountPayable SettlePayable(FinancialSettlementRequest request)
    {
        var entity = _db.AccountsPayable.First(x => x.Id == request.DocumentId);
        EnsureCanSettle(entity.Status, entity.BalanceAmount, request.Amount);
        ApplySettlement(entity, request);
        entity.PaymentMethod = request.PaymentMethod.Trim();
        entity.PaidAtUtc = entity.Status == FinancialStatus.Paid ? DateTime.UtcNow : entity.PaidAtUtc;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        _db.FinancialSettlements.Add(BuildSettlement(request, FinancialType.Payable, accountPayableId: entity.Id));
        _db.FinancialMovements.Add(new FinancialMovement { Number = entity.Number, Type = FinancialType.Payable, Amount = request.Amount, Description = $"Pagamento {entity.Description}", Reference = request.PaymentMethod });
        Log("Baixa conta pagar", entity.Number, request.Amount, request.Observation, request.UserId);
        _db.SaveChanges();
        return entity;
    }

    public AccountReceivable SettleReceivable(FinancialSettlementRequest request)
    {
        var entity = _db.AccountsReceivable.First(x => x.Id == request.DocumentId);
        EnsureCanSettle(entity.Status, entity.BalanceAmount, request.Amount);
        ApplySettlement(entity, request);
        entity.PaymentMethod = request.PaymentMethod.Trim();
        entity.PaidAtUtc = entity.Status == FinancialStatus.Paid ? DateTime.UtcNow : entity.PaidAtUtc;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        _db.FinancialSettlements.Add(BuildSettlement(request, FinancialType.Receivable, accountReceivableId: entity.Id));
        _db.FinancialMovements.Add(new FinancialMovement { Number = entity.Number, Type = FinancialType.Receivable, Amount = request.Amount, Description = $"Recebimento {entity.Description}", Reference = request.PaymentMethod });
        RegisterCashReceipt(entity, request);
        Log("Baixa conta receber", entity.Number, request.Amount, request.Observation, request.UserId);
        _db.SaveChanges();
        return entity;
    }

    public AccountPayable CancelPayable(Guid id, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("Motivo obrigatorio para cancelamento.");
        var entity = _db.AccountsPayable.First(x => x.Id == id);
        if (entity.Status == FinancialStatus.Paid) throw new InvalidOperationException("Nao e permitido cancelar conta paga sem estorno gerencial.");
        entity.Status = FinancialStatus.Cancelled;
        entity.Description = $"{entity.Description} | CANCELADO: {reason.Trim()}";
        entity.BalanceAmount = 0;
        entity.IsActive = false;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        Log("Cancelamento conta pagar", entity.Number, entity.OriginalAmount, reason, null);
        _db.SaveChanges();
        return entity;
    }

    public AccountReceivable CancelReceivable(Guid id, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("Motivo obrigatorio para cancelamento.");
        var entity = _db.AccountsReceivable.First(x => x.Id == id);
        if (entity.Status == FinancialStatus.Paid) throw new InvalidOperationException("Nao e permitido cancelar conta recebida sem estorno gerencial.");
        entity.Status = FinancialStatus.Cancelled;
        entity.Description = $"{entity.Description} | CANCELADO: {reason.Trim()}";
        entity.BalanceAmount = 0;
        entity.IsActive = false;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        Log("Cancelamento conta receber", entity.Number, entity.OriginalAmount, reason, null);
        _db.SaveChanges();
        return entity;
    }

    public Duplicate CreateDuplicate(DuplicateRequest request)
    {
        if (request.Amount <= 0) throw new InvalidOperationException("Valor da duplicata deve ser maior que zero.");
        var entity = new Duplicate
        {
            Number = request.Number.Trim(),
            Type = request.Type,
            SaleId = request.SaleId,
            BudgetId = request.BudgetId,
            Amount = request.Amount,
            BalanceAmount = request.Amount,
            IssueDateUtc = DateTime.UtcNow,
            DueDateUtc = request.DueDateUtc,
            Status = FinancialStatus.Open
        };
        _db.Duplicates.Add(entity);
        Log("Cadastro duplicata", entity.Number, entity.Amount, entity.Type.ToString(), null);
        _db.SaveChanges();
        return entity;
    }

    public Duplicate DuplicateBaixa(Guid id, decimal amount)
        => SettleDuplicate(new FinancialSettlementRequest(id, FinancialType.Receivable, amount));

    public Duplicate SettleDuplicate(FinancialSettlementRequest request)
    {
        var entity = _db.Duplicates.First(x => x.Id == request.DocumentId);
        EnsureCanSettle(entity.Status, entity.BalanceAmount, request.Amount);
        entity.PaidAmount += request.Amount;
        entity.InterestAmount += request.Interest;
        entity.FineAmount += request.Fine;
        entity.DiscountAmount += request.Discount;
        entity.BalanceAmount = Math.Max(0, entity.Amount + entity.InterestAmount + entity.FineAmount - entity.DiscountAmount - entity.PaidAmount);
        entity.Status = entity.BalanceAmount <= 0 ? FinancialStatus.Paid : FinancialStatus.Partial;
        entity.PaidAtUtc = entity.Status == FinancialStatus.Paid ? DateTime.UtcNow : entity.PaidAtUtc;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        _db.FinancialSettlements.Add(BuildSettlement(request, entity.Type, duplicateId: entity.Id));
        if (entity.AccountReceivableId.HasValue)
        {
            SettleReceivable(request with { DocumentId = entity.AccountReceivableId.Value, Type = FinancialType.Receivable });
        }
        if (entity.AccountPayableId.HasValue)
        {
            SettlePayable(request with { DocumentId = entity.AccountPayableId.Value, Type = FinancialType.Payable });
        }
        Log("Baixa duplicata", entity.Number, request.Amount, request.Observation, request.UserId);
        _db.SaveChanges();
        return entity;
    }

    public Duplicate CancelDuplicate(Guid id, string reason)
    {
        var entity = _db.Duplicates.First(x => x.Id == id);
        entity.Status = FinancialStatus.Cancelled;
        entity.Description = reason.Trim();
        entity.BalanceAmount = 0;
        entity.IsActive = false;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        Log("Cancelamento duplicata", entity.Number, entity.Amount, reason, null);
        _db.SaveChanges();
        return entity;
    }

    public Duplicate IssueSecondCopy(Guid id)
    {
        var source = _db.Duplicates.AsNoTracking().First(x => x.Id == id);
        var entity = new Duplicate
        {
            Number = $"{source.Number}-2VIA-{DateTime.UtcNow:yyyyMMddHHmmss}",
            Type = source.Type,
            SaleId = source.SaleId,
            BudgetId = source.BudgetId,
            Description = $"Segunda via de {source.Number}",
            Amount = source.BalanceAmount > 0 ? source.BalanceAmount : source.Amount,
            PaidAmount = 0,
            BalanceAmount = source.BalanceAmount > 0 ? source.BalanceAmount : source.Amount,
            DueDateUtc = source.DueDateUtc,
            Status = FinancialStatus.Open
        };
        _db.Duplicates.Add(entity);
        _db.SaveChanges();
        return entity;
    }

    public FinancialMovement RegisterMovement(FinancialMovementRequest request)
    {
        var entity = new FinancialMovement
        {
            Number = request.Number.Trim(),
            Type = request.Type,
            Amount = request.Amount,
            Description = request.Description.Trim(),
            Reference = request.Reference.Trim()
        };
        _db.FinancialMovements.Add(entity);
        Log("Movimento financeiro", entity.Number, entity.Amount, entity.Description, null);
        _db.SaveChanges();
        return entity;
    }

    public IReadOnlyList<AccountPayable> SearchPayables(FinancialSearchRequest request)
    {
        UpdateOverdueStatuses();
        var query = _db.AccountsPayable.AsNoTracking().AsQueryable();
        if (request.FromUtc.HasValue) query = query.Where(x => x.DueDateUtc >= request.FromUtc.Value);
        if (request.ToUtc.HasValue) query = query.Where(x => x.DueDateUtc <= request.ToUtc.Value);
        if (request.Status.HasValue) query = query.Where(x => x.Status == request.Status.Value);
        if (request.OnlyOverdue) query = query.Where(x => x.Status == FinancialStatus.Overdue || (x.BalanceAmount > 0 && x.DueDateUtc.Date < DateTime.UtcNow.Date));
        if (!string.IsNullOrWhiteSpace(request.Term))
        {
            var term = request.Term.Trim().ToLower();
            query = query.Where(x => x.Number.ToLower().Contains(term) || x.SupplierName.ToLower().Contains(term) || x.Description.ToLower().Contains(term));
        }
        return query.OrderBy(x => x.DueDateUtc).ToList();
    }

    public IReadOnlyList<AccountReceivable> SearchReceivables(FinancialSearchRequest request)
    {
        UpdateOverdueStatuses();
        var query = _db.AccountsReceivable.AsNoTracking().AsQueryable();
        if (request.FromUtc.HasValue) query = query.Where(x => x.DueDateUtc >= request.FromUtc.Value);
        if (request.ToUtc.HasValue) query = query.Where(x => x.DueDateUtc <= request.ToUtc.Value);
        if (request.Status.HasValue) query = query.Where(x => x.Status == request.Status.Value);
        if (request.OnlyOverdue) query = query.Where(x => x.Status == FinancialStatus.Overdue || (x.BalanceAmount > 0 && x.DueDateUtc.Date < DateTime.UtcNow.Date));
        if (!string.IsNullOrWhiteSpace(request.Term))
        {
            var term = request.Term.Trim().ToLower();
            query = query.Where(x => x.Number.ToLower().Contains(term) || x.CustomerName.ToLower().Contains(term) || x.Description.ToLower().Contains(term));
        }
        return query.OrderBy(x => x.DueDateUtc).ToList();
    }

    public IReadOnlyList<Duplicate> SearchDuplicates(FinancialSearchRequest request)
    {
        UpdateOverdueStatuses();
        var query = _db.Duplicates.AsNoTracking().AsQueryable();
        if (request.FromUtc.HasValue) query = query.Where(x => x.DueDateUtc >= request.FromUtc.Value);
        if (request.ToUtc.HasValue) query = query.Where(x => x.DueDateUtc <= request.ToUtc.Value);
        if (request.Status.HasValue) query = query.Where(x => x.Status == request.Status.Value);
        if (request.OnlyOverdue) query = query.Where(x => x.Status == FinancialStatus.Overdue || (x.BalanceAmount > 0 && x.DueDateUtc.Date < DateTime.UtcNow.Date));
        if (!string.IsNullOrWhiteSpace(request.Term))
        {
            var term = request.Term.Trim().ToLower();
            query = query.Where(x => x.Number.ToLower().Contains(term) || x.Description.ToLower().Contains(term));
        }
        return query.OrderBy(x => x.DueDateUtc).ToList();
    }

    public IReadOnlyList<FinancialSettlement> Settlements(Guid? documentId = null)
    {
        var query = _db.FinancialSettlements.AsNoTracking().AsQueryable();
        if (documentId.HasValue)
        {
            query = query.Where(x => x.DuplicateId == documentId || x.AccountPayableId == documentId || x.AccountReceivableId == documentId);
        }
        return query.OrderByDescending(x => x.SettledAtUtc).ToList();
    }

    public IReadOnlyList<FinancialCategory> Categories(FinancialType? type = null)
    {
        EnsureDefaultCategories();
        var query = _db.FinancialCategories.AsNoTracking().Where(x => x.IsActive);
        if (type.HasValue) query = query.Where(x => x.Type == type.Value);
        return query.OrderBy(x => x.Type).ThenBy(x => x.Name).ToList();
    }

    public FinancialCategory CreateCategory(string name, FinancialType type)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Nome da categoria obrigatorio.");
        var entity = new FinancialCategory { Name = name.Trim(), Type = type };
        _db.FinancialCategories.Add(entity);
        Log("Cadastro categoria", name, 0, type.ToString(), null);
        _db.SaveChanges();
        return entity;
    }

    public FinancialDashboardSummary Dashboard(DateTime? todayUtc = null)
    {
        UpdateOverdueStatuses();
        var today = (todayUtc ?? DateTime.UtcNow).Date;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddTicks(-1);
        var payToday = _db.AccountsPayable.Where(x => x.BalanceAmount > 0 && x.DueDateUtc.Date == today).Sum(x => x.BalanceAmount);
        var payOverdue = _db.AccountsPayable.Where(x => x.BalanceAmount > 0 && x.DueDateUtc.Date < today && x.Status != FinancialStatus.Cancelled).Sum(x => x.BalanceAmount);
        var recToday = _db.AccountsReceivable.Where(x => x.BalanceAmount > 0 && x.DueDateUtc.Date == today).Sum(x => x.BalanceAmount);
        var recOverdue = _db.AccountsReceivable.Where(x => x.BalanceAmount > 0 && x.DueDateUtc.Date < today && x.Status != FinancialStatus.Cancelled).Sum(x => x.BalanceAmount);
        var received = _db.FinancialSettlements.Where(x => x.Type == FinancialType.Receivable && x.SettledAtUtc >= monthStart && x.SettledAtUtc <= monthEnd).Sum(x => x.Amount);
        var paid = _db.FinancialSettlements.Where(x => x.Type == FinancialType.Payable && x.SettledAtUtc >= monthStart && x.SettledAtUtc <= monthEnd).Sum(x => x.Amount);
        var flow = CashFlow(today, today.AddDays(6));
        var forecast = _db.AccountsReceivable.Where(x => x.BalanceAmount > 0 && x.Status != FinancialStatus.Cancelled).Sum(x => x.BalanceAmount)
            - _db.AccountsPayable.Where(x => x.BalanceAmount > 0 && x.Status != FinancialStatus.Cancelled).Sum(x => x.BalanceAmount);
        var alerts = new List<string>();
        if (payOverdue > 0) alerts.Add($"Contas a pagar vencidas: {payOverdue:C}");
        if (recOverdue > 0) alerts.Add($"Clientes inadimplentes/contas vencidas: {recOverdue:C}");
        if (forecast < 0) alerts.Add($"Saldo previsto negativo: {forecast:C}");
        return new FinancialDashboardSummary(payToday, payOverdue, recToday, recOverdue, received, paid, forecast, flow, alerts);
    }

    public IReadOnlyList<FinancialCashFlowItem> CashFlow(DateTime fromUtc, DateTime toUtc)
    {
        var items = new List<FinancialCashFlowItem>();
        decimal running = 0;
        for (var day = fromUtc.Date; day <= toUtc.Date; day = day.AddDays(1))
        {
            var expectedIn = _db.AccountsReceivable.Where(x => x.BalanceAmount > 0 && x.DueDateUtc.Date == day && x.Status != FinancialStatus.Cancelled).Sum(x => x.BalanceAmount);
            var expectedOut = _db.AccountsPayable.Where(x => x.BalanceAmount > 0 && x.DueDateUtc.Date == day && x.Status != FinancialStatus.Cancelled).Sum(x => x.BalanceAmount);
            var realizedIn = _db.FinancialSettlements.Where(x => x.Type == FinancialType.Receivable && x.SettledAtUtc.Date == day).Sum(x => x.Amount);
            var realizedOut = _db.FinancialSettlements.Where(x => x.Type == FinancialType.Payable && x.SettledAtUtc.Date == day).Sum(x => x.Amount);
            var daily = realizedIn - realizedOut;
            running += expectedIn - expectedOut + daily;
            items.Add(new FinancialCashFlowItem(day, expectedIn, expectedOut, realizedIn, realizedOut, daily, running));
        }
        return items;
    }

    public byte[] ExportPdf(FinancialSearchRequest request)
    {
        var lines = FinancialLines(request);
        return BuildPdf("Relatorio financeiro", lines);
    }

    public byte[] ExportExcel(FinancialSearchRequest request)
    {
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var sheet = workbook.AddWorksheet("Financeiro");
        var row = 1;
        foreach (var line in FinancialLines(request))
        {
            sheet.Cell(row++, 1).Value = line;
        }
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] PrintReceipt(Guid settlementId, InternalPaperFormat format = InternalPaperFormat.Thermal80)
    {
        var s = _db.FinancialSettlements.AsNoTracking().First(x => x.Id == settlementId);
        return new InternalDocumentService().GeneratePdf(new InternalDocumentRequest(
            InternalDocumentKind.PaymentProof,
            format,
            settlementId.ToString("N")[..10],
            string.Empty,
            s.Type == FinancialType.Receivable ? "Recibo de recebimento" : "Comprovante de pagamento",
            s.TotalAmount,
            s.PaymentMethod,
            s.Observation,
            new[]
            {
                $"Data: {s.SettledAtUtc:dd/MM/yyyy HH:mm}",
                $"Baixa: {s.Amount:C}",
                $"Juros: {s.InterestAmount:C}",
                $"Multa: {s.FineAmount:C}",
                $"Desconto: {s.DiscountAmount:C}",
                $"Total: {s.TotalAmount:C}"
            }));
    }

    private static void EnsureCanSettle(FinancialStatus status, decimal balance, decimal amount)
    {
        if (status == FinancialStatus.Cancelled) throw new InvalidOperationException("Nao e permitido baixar documento cancelado.");
        if (status == FinancialStatus.Paid) throw new InvalidOperationException("Documento ja baixado.");
        if (amount <= 0) throw new InvalidOperationException("Valor da baixa deve ser maior que zero.");
        if (amount > balance) throw new InvalidOperationException("Baixa maior que saldo.");
    }

    private static void ApplySettlement(AccountPayable entity, FinancialSettlementRequest request)
    {
        entity.PaidAmount += request.Amount;
        entity.InterestAmount += request.Interest;
        entity.FineAmount += request.Fine;
        entity.DiscountAmount += request.Discount;
        entity.BalanceAmount = Math.Max(0, entity.OriginalAmount + entity.InterestAmount + entity.FineAmount - entity.DiscountAmount - entity.PaidAmount);
        entity.Status = entity.BalanceAmount <= 0 ? FinancialStatus.Paid : FinancialStatus.Partial;
        entity.UserId = request.UserId;
    }

    private static void ApplySettlement(AccountReceivable entity, FinancialSettlementRequest request)
    {
        entity.PaidAmount += request.Amount;
        entity.InterestAmount += request.Interest;
        entity.FineAmount += request.Fine;
        entity.DiscountAmount += request.Discount;
        entity.BalanceAmount = Math.Max(0, entity.OriginalAmount + entity.InterestAmount + entity.FineAmount - entity.DiscountAmount - entity.PaidAmount);
        entity.Status = entity.BalanceAmount <= 0 ? FinancialStatus.Paid : FinancialStatus.Partial;
        entity.UserId = request.UserId;
    }

    private static FinancialSettlement BuildSettlement(
        FinancialSettlementRequest request,
        FinancialType type,
        Guid? duplicateId = null,
        Guid? accountPayableId = null,
        Guid? accountReceivableId = null)
    {
        return new FinancialSettlement
        {
            DuplicateId = duplicateId,
            AccountPayableId = accountPayableId,
            AccountReceivableId = accountReceivableId,
            Type = type,
            Amount = request.Amount,
            InterestAmount = request.Interest,
            FineAmount = request.Fine,
            DiscountAmount = request.Discount,
            TotalAmount = request.Amount + request.Interest + request.Fine - request.Discount,
            PaymentMethod = request.PaymentMethod.Trim(),
            SettledAtUtc = DateTime.UtcNow,
            UserId = request.UserId,
            CashSessionId = request.CashSessionId,
            Observation = request.Observation.Trim()
        };
    }

    private void RegisterCashReceipt(AccountReceivable entity, FinancialSettlementRequest request)
    {
        if (!request.CashSessionId.HasValue || request.PaymentMethod.Equals("BOLETO", StringComparison.OrdinalIgnoreCase) || request.PaymentMethod.Equals("CHEQUE", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var session = _db.CashSessions.FirstOrDefault(x => x.Id == request.CashSessionId.Value && x.Status == CashSessionStatus.Aberto);
        if (session is null)
        {
            return;
        }

        var method = string.IsNullOrWhiteSpace(request.PaymentMethod) ? "DINHEIRO" : request.PaymentMethod.Trim().ToUpperInvariant();
        var total = request.Amount + request.Interest + request.Fine - request.Discount;
        _db.CashMovements.Add(new CashMovement
        {
            CashSessionId = session.Id,
            Type = CashMovementType.DuplicateReceipt,
            Origin = "Financeiro",
            Amount = total,
            Description = $"Recebimento {entity.Number}",
            UserId = request.UserId,
            PaymentMethod = method,
            MovementAtUtc = DateTime.UtcNow,
            Observation = request.Observation
        });
        session.TotalSalesAmount += total;
        if (method == "DINHEIRO") { session.CashAmount += total; session.CurrentAmount = session.CashAmount; }
        else if (method == "PIX") session.PixAmount += total;
        else if (method == "CARTAO_DEBITO") session.DebitCardAmount += total;
        else if (method == "CARTAO_CREDITO") session.CreditCardAmount += total;
        session.UpdatedAtUtc = DateTime.UtcNow;
    }

    private void UpdateOverdueStatuses()
    {
        var today = DateTime.UtcNow.Date;
        foreach (var payable in _db.AccountsPayable.Where(x => x.BalanceAmount > 0 && x.DueDateUtc.Date < today && x.Status == FinancialStatus.Open))
        {
            payable.Status = FinancialStatus.Overdue;
            payable.UpdatedAtUtc = DateTime.UtcNow;
        }
        foreach (var receivable in _db.AccountsReceivable.Where(x => x.BalanceAmount > 0 && x.DueDateUtc.Date < today && x.Status == FinancialStatus.Open))
        {
            receivable.Status = FinancialStatus.Overdue;
            receivable.UpdatedAtUtc = DateTime.UtcNow;
        }
        foreach (var duplicate in _db.Duplicates.Where(x => x.BalanceAmount > 0 && x.DueDateUtc.Date < today && x.Status == FinancialStatus.Open))
        {
            duplicate.Status = FinancialStatus.Overdue;
            duplicate.UpdatedAtUtc = DateTime.UtcNow;
        }
        _db.SaveChanges();
    }

    private void EnsureDefaultCategories()
    {
        if (_db.FinancialCategories.Any())
        {
            return;
        }

        foreach (var name in new[] { "Venda a vista", "Venda a prazo", "Recebimento duplicata", "Outros recebimentos" })
        {
            _db.FinancialCategories.Add(new FinancialCategory { Name = name, Type = FinancialType.Receivable });
        }
        foreach (var name in new[] { "Fornecedor", "Funcionarios", "Aluguel", "Agua", "Luz", "Internet", "Impostos", "Transporte", "Outros pagamentos" })
        {
            _db.FinancialCategories.Add(new FinancialCategory { Name = name, Type = FinancialType.Payable });
        }
        _db.SaveChanges();
    }

    private List<string> FinancialLines(FinancialSearchRequest request)
    {
        var lines = new List<string> { "TIPO | NUMERO | VENCIMENTO | VALOR | PAGO | SALDO | STATUS" };
        lines.AddRange(SearchPayables(request).Select(x => $"PAGAR | {x.Number} | {x.DueDateUtc:dd/MM/yyyy} | {x.OriginalAmount:C} | {x.PaidAmount:C} | {x.BalanceAmount:C} | {x.Status}"));
        lines.AddRange(SearchReceivables(request).Select(x => $"RECEBER | {x.Number} | {x.DueDateUtc:dd/MM/yyyy} | {x.OriginalAmount:C} | {x.PaidAmount:C} | {x.BalanceAmount:C} | {x.Status}"));
        lines.AddRange(SearchDuplicates(request).Select(x => $"DUP {x.Type} | {x.Number} | {x.DueDateUtc:dd/MM/yyyy} | {x.Amount:C} | {x.PaidAmount:C} | {x.BalanceAmount:C} | {x.Status}"));
        return lines;
    }

    private static byte[] BuildPdf(string title, IEnumerable<string> lines)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.Header().Text(title).SemiBold().FontSize(18);
                page.Content().Column(column =>
                {
                    column.Spacing(4);
                    foreach (var line in lines)
                    {
                        column.Item().Text(line);
                    }
                });
            });
        });
        return document.GeneratePdf();
    }

    private void Log(string action, string document, decimal amount, string reason, Guid? userId)
    {
        _db.FinancialLogs.Add(new FinancialLog
        {
            UserId = userId,
            LoggedAtUtc = DateTime.UtcNow,
            Action = action,
            Document = document,
            Amount = amount,
            Reason = reason.Trim()
        });
    }

    public SaleCancellation CancelSale(SaleCancellationRequest request)
    {
        var entity = new SaleCancellation
        {
            SaleId = request.SaleId,
            Reason = request.Reason.Trim(),
            UserId = request.UserId,
            Observation = request.Observation.Trim()
        };
        _db.SaleCancellations.Add(entity);

        var sale = _db.Sales.First(x => x.Id == request.SaleId);
        entity.TotalAmount = sale.TotalAmount;
        sale.IsActive = false;
        sale.Status = SaleStatus.Cancelada;
        sale.UpdatedAtUtc = DateTime.UtcNow;
        _db.SaveChanges();
        return entity;
    }

    public SaleReturn ReturnSale(SaleReturnRequest request)
    {
        var entity = new SaleReturn
        {
            SaleId = request.SaleId,
            Reason = request.Reason.Trim(),
            TotalReturnedAmount = request.TotalReturnedAmount,
            ProcessedBy = request.ProcessedBy.Trim()
        };
        _db.SaleReturns.Add(entity);

        var sale = _db.Sales.First(x => x.Id == request.SaleId);
        sale.UpdatedAtUtc = DateTime.UtcNow;
        _db.SaveChanges();
        return entity;
    }

    public AccountPayable ReversePayable(Guid id, string reason)
    {
        var entity = _db.AccountsPayable.First(x => x.Id == id);
        entity.Status = FinancialStatus.Returned;
        entity.Description = $"{entity.Description} | DEVOLUÇÃO: {reason.Trim()}";
        entity.UpdatedAtUtc = DateTime.UtcNow;
        _db.SaveChanges();
        return entity;
    }
}

public sealed class SaleCancellationService : ISaleCancellationService
{
    private readonly MaterialProDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ISecurityService? _security;

    public SaleCancellationService(MaterialProDbContext db, IPasswordHasher hasher, ISecurityService? security = null)
    {
        _db = db;
        _hasher = hasher;
        _security = security;
    }

    public SaleCancellation CancelSale(SaleCancellationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new InvalidOperationException("Motivo do cancelamento é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(request.ManagerPassword))
        {
            throw new InvalidOperationException("Senha de gerente é obrigatória.");
        }

        var authorized = _db.Users
            .Where(x => x.IsActive && (x.Role == UserRole.Admin || x.Role == UserRole.Manager))
            .AsEnumerable()
            .Any(x => _hasher.Verify(request.ManagerPassword, x.PasswordSalt, x.PasswordHash));

        if (!authorized)
        {
            throw new InvalidOperationException("Senha de gerente inválida.");
        }

        var sale = _db.Sales.First(x => x.Id == request.SaleId);
        if (sale.Status == SaleStatus.Cancelada || !sale.IsActive)
        {
            throw new InvalidOperationException("Venda já cancelada.");
        }

        var cancellation = new SaleCancellation
        {
            SaleId = sale.Id,
            Reason = request.Reason.Trim(),
            UserId = request.UserId,
            CancelledAtUtc = DateTime.UtcNow,
            TotalAmount = sale.TotalAmount,
            Observation = request.Observation.Trim()
        };

        sale.Status = SaleStatus.Cancelada;
        sale.IsActive = false;
        sale.UpdatedAtUtc = DateTime.UtcNow;

        var items = _db.SaleItems.Where(x => x.SaleId == sale.Id).ToList();
        foreach (var item in items)
        {
            var product = _db.Products.FirstOrDefault(x => x.Id == item.ProductId);
            if (product is null)
            {
                continue;
            }

            product.StockQuantity += item.Quantity;
            product.UpdatedAtUtc = DateTime.UtcNow;
            _db.StockMovements.Add(new StockMovement
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                Reason = "Estorno por cancelamento de venda",
                Reference = sale.ReceiptNumber,
                MovementAtUtc = DateTime.UtcNow
            });
        }

        foreach (var duplicate in _db.Duplicates.Where(x => x.SaleId == sale.Id))
        {
            duplicate.Status = FinancialStatus.Cancelled;
            duplicate.BalanceAmount = 0;
            duplicate.Description = AppendText(duplicate.Description, $"CANCELADA: {request.Reason}");
            duplicate.UpdatedAtUtc = DateTime.UtcNow;
        }

        foreach (var receivable in _db.AccountsReceivable.Where(x => x.SaleId == sale.Id))
        {
            receivable.Status = FinancialStatus.Cancelled;
            receivable.BalanceAmount = 0;
            receivable.Description = AppendText(receivable.Description, $"CANCELADA: {request.Reason}");
            receivable.UpdatedAtUtc = DateTime.UtcNow;
        }

        _db.SaleCancellations.Add(cancellation);
        _db.SaveChanges();
        _security?.RecordAudit(new SecurityAuditRequest(request.UserId, SecurityEventType.Audit, "Vendas", "CancelSale", nameof(Sale), sale.Id.ToString(), $"Venda cancelada: {request.Reason}", Environment.MachineName, string.Empty));
        return cancellation;
    }

    public IReadOnlyList<SaleCancellationSummary> ListCancelledSales()
    {
        return QuerySummaries().OrderByDescending(x => x.CancelledAtUtc).ToList();
    }

    public IReadOnlyList<SaleCancellationSummary> Report(DateTime fromUtc, DateTime toUtc)
    {
        return QuerySummaries()
            .Where(x => x.CancelledAtUtc >= fromUtc && x.CancelledAtUtc <= toUtc)
            .OrderByDescending(x => x.CancelledAtUtc)
            .ToList();
    }

    public byte[] GenerateProofPdf(Guid cancellationId)
    {
        var item = QuerySummaries().First(x => x.Id == cancellationId);
        return BuildPdf("Comprovante de cancelamento", new[]
        {
            $"Venda: {item.ReceiptNumber}",
            $"Data: {item.CancelledAtUtc:dd/MM/yyyy HH:mm}",
            $"Usuario: {item.UserName}",
            $"Valor: {item.TotalAmount:C}",
            $"Motivo: {item.Reason}",
            $"Observacao: {item.Observation}",
            "Documento interno do sistema MaterialPro. Sem transmissao fiscal."
        });
    }

    public byte[] GenerateReportPdf(DateTime fromUtc, DateTime toUtc)
    {
        var lines = Report(fromUtc, toUtc)
            .Select(x => $"{x.CancelledAtUtc:dd/MM/yyyy HH:mm} | {x.ReceiptNumber} | {x.UserName} | {x.TotalAmount:C} | {x.Reason}")
            .ToList();
        return BuildPdf("Relatorio de cancelamentos", lines);
    }

    private IEnumerable<SaleCancellationSummary> QuerySummaries()
    {
        return from cancellation in _db.SaleCancellations.AsNoTracking()
               join sale in _db.Sales.AsNoTracking() on cancellation.SaleId equals sale.Id
               join user in _db.Users.AsNoTracking() on cancellation.UserId equals user.Id into users
               from user in users.DefaultIfEmpty()
               select new SaleCancellationSummary(
                   cancellation.Id,
                   cancellation.SaleId,
                   string.IsNullOrWhiteSpace(sale.ReceiptNumber) ? sale.Id.ToString() : sale.ReceiptNumber,
                   cancellation.Reason,
                   user == null ? "Usuario removido" : user.FullName,
                   cancellation.CancelledAtUtc,
                   cancellation.TotalAmount,
                   cancellation.Observation);
    }

    private static string AppendText(string value, string suffix)
    {
        return string.IsNullOrWhiteSpace(value) ? suffix : $"{value} | {suffix}";
    }

    private static byte[] BuildPdf(string title, IEnumerable<string> lines)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.Header().Text(title).SemiBold().FontSize(18);
                page.Content().Column(column =>
                {
                    column.Spacing(6);
                    foreach (var line in lines)
                    {
                        column.Item().Text(line);
                    }
                });
            });
        });

        return document.GeneratePdf();
    }
}

public sealed class ReportService : IReportService
{
    private readonly MaterialProDbContext _db;

    public ReportService(MaterialProDbContext db) => _db = db;

    public byte[] ExportSalesPdf(ReportRangeRequest request) => BuildPdf("Relatorio de Vendas", LoadSalesLines(request));
    public byte[] ExportFinancialPdf(ReportRangeRequest request) => BuildPdf("Relatorio Financeiro", LoadFinancialLines(request));

    public byte[] ExportSalesExcel(ReportRangeRequest request) => BuildExcel("Vendas", LoadSalesLines(request));
    public byte[] ExportFinancialExcel(ReportRangeRequest request) => BuildExcel("Financeiro", LoadFinancialLines(request));
    public byte[] ExportDuplicateSecondCopyPdf(Guid duplicateId)
    {
        var duplicate = _db.Duplicates.AsNoTracking().First(x => x.Id == duplicateId);
        var lines = new[]
        {
            $"Numero: {duplicate.Number}",
            $"Valor: {duplicate.Amount:C}",
            $"Saldo: {duplicate.BalanceAmount:C}",
            $"Vencimento: {duplicate.DueDateUtc:yyyy-MM-dd}",
            $"Descricao: {duplicate.Description}"
        };
        return BuildPdf("Segunda Via de Duplicata", lines);
    }

    private List<string> LoadSalesLines(ReportRangeRequest request)
        => _db.Sales.AsNoTracking()
            .Where(x => x.SoldAtUtc >= request.FromUtc && x.SoldAtUtc <= request.ToUtc)
            .OrderByDescending(x => x.SoldAtUtc)
            .Select(x => $"{x.SoldAtUtc:yyyy-MM-dd} | {x.ReceiptNumber} | {x.TotalAmount:C}")
            .ToList();

    private List<string> LoadFinancialLines(ReportRangeRequest request)
        => _db.AccountsPayable.AsNoTracking()
            .Where(x => x.CreatedAtUtc >= request.FromUtc && x.CreatedAtUtc <= request.ToUtc)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => $"{x.CreatedAtUtc:yyyy-MM-dd} | {x.Number} | {x.BalanceAmount:C} | {x.Status}")
            .ToList();

    private static byte[] BuildPdf(string title, IEnumerable<string> lines)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(24);
                page.Header().Text(title).SemiBold().FontSize(18);
                page.Content().Column(column =>
                {
                    foreach (var line in lines)
                    {
                        column.Item().Text(line);
                    }
                });
            });
        });

        return document.GeneratePdf();
    }

    private static byte[] BuildExcel(string sheetName, IEnumerable<string> lines)
    {
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var sheet = workbook.AddWorksheet(sheetName);
        var row = 1;
        foreach (var line in lines)
        {
            sheet.Cell(row++, 1).Value = line;
        }
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }
}

public sealed class PrintService : IPrintService
{
    [SupportedOSPlatform("windows")]
    public void PrintText(string title, IEnumerable<string> lines)
    {
        var content = new List<string> { title };
        content.AddRange(lines);

        var document = new PrintDocument();
        document.PrintPage += (_, e) =>
        {
            using var font = new Font("Consolas", 9);
            var y = e.MarginBounds.Top;
            foreach (var line in content)
            {
                e.Graphics!.DrawString(line, font, Brushes.Black, e.MarginBounds.Left, y);
                y += (int)font.GetHeight(e.Graphics) + 2;
            }
        };

        try
        {
            document.Print();
        }
        catch
        {
            File.WriteAllText(Path.Combine(Path.GetTempPath(), $"materialpro-print-{Guid.NewGuid():N}.txt"), string.Join(Environment.NewLine, content));
        }
    }
}

public sealed class InternalDocumentService : IInternalDocumentService
{
    public byte[] GeneratePdf(InternalDocumentRequest request)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var pageSize = request.PaperFormat switch
        {
            InternalPaperFormat.Thermal58 => PageSizes.A7,
            InternalPaperFormat.Thermal80 => PageSizes.A6,
            _ => PageSizes.A4
        };

        var title = request.Kind switch
        {
            InternalDocumentKind.SaleCoupon => "Cupom de venda simples",
            InternalDocumentKind.SaleReceipt => "Recibo de venda",
            InternalDocumentKind.Budget => "Orcamento",
            InternalDocumentKind.PaymentProof => "Comprovante de pagamento",
            InternalDocumentKind.SaleSecondCopy => "Segunda via de venda",
            _ => "Documento interno"
        };

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(pageSize);
                page.Margin(request.PaperFormat == InternalPaperFormat.A4 ? 24 : 10);
                page.Header().Column(column =>
                {
                    column.Item().Text(title).SemiBold().FontSize(request.PaperFormat == InternalPaperFormat.A4 ? 18 : 14);
                    column.Item().Text(request.Number).FontSize(10);
                });
                page.Content().Column(column =>
                {
                    column.Spacing(4);
                    if (!string.IsNullOrWhiteSpace(request.CustomerName))
                    {
                        column.Item().Text($"Cliente: {request.CustomerName}");
                    }
                    if (!string.IsNullOrWhiteSpace(request.Reference))
                    {
                        column.Item().Text($"Referencia: {request.Reference}");
                    }

                    foreach (var line in request.Lines.Where(x => !string.IsNullOrWhiteSpace(x)))
                    {
                        column.Item().Text(line);
                    }

                    column.Item().Text($"Total: {request.TotalAmount:C}").SemiBold();
                    if (!string.IsNullOrWhiteSpace(request.PaymentMethod))
                    {
                        column.Item().Text($"Pagamento: {request.PaymentMethod}");
                    }
                    if (!string.IsNullOrWhiteSpace(request.Notes))
                    {
                        column.Item().Text(request.Notes);
                    }
                });
            });
        });

        return document.GeneratePdf();
    }
}

public sealed class NonFiscalNoteService : INonFiscalNoteService
{
    private readonly MaterialProDbContext _db;

    public NonFiscalNoteService(MaterialProDbContext db) => _db = db;

    public NonFiscalNote Create(NonFiscalNoteRequest request)
    {
        var items = request.Items
            .Where(x => !string.IsNullOrWhiteSpace(x.Description))
            .Select(x => new NonFiscalNoteItem
            {
                Description = x.Description.Trim(),
                Quantity = x.Quantity,
                UnitPrice = x.UnitPrice,
                TotalPrice = Math.Round(x.Quantity * x.UnitPrice, 2)
            })
            .ToList();

        var note = new NonFiscalNote
        {
            Number = request.Number.Trim(),
            StoreName = request.StoreName.Trim(),
            StoreDocument = request.StoreDocument.Trim(),
            CustomerName = request.CustomerName.Trim(),
            CustomerDocument = request.CustomerDocument.Trim(),
            CustomerAddress = request.CustomerAddress.Trim(),
            Notes = request.Notes.Trim(),
            TotalAmount = items.Sum(x => x.TotalPrice),
            IssuedAtUtc = DateTime.UtcNow
        };

        _db.NonFiscalNotes.Add(note);
        _db.SaveChanges();

        foreach (var item in items)
        {
            item.NonFiscalNoteId = note.Id;
        }

        _db.NonFiscalNoteItems.AddRange(items);
        _db.SaveChanges();
        return note;
    }

    public IReadOnlyList<NonFiscalNote> List()
    {
        return _db.NonFiscalNotes.AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToList();
    }

    public byte[] GeneratePdf(Guid noteId)
    {
        var note = _db.NonFiscalNotes.AsNoTracking().First(x => x.Id == noteId);
        var items = _db.NonFiscalNoteItems.AsNoTracking()
            .Where(x => x.NonFiscalNoteId == noteId)
            .OrderBy(x => x.CreatedAtUtc)
            .ToList();

        QuestPDF.Settings.License = LicenseType.Community;
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(24);
                page.Size(PageSizes.A4);
                page.Header().Column(column =>
                {
                    column.Item().Text("Documento nao fiscal").SemiBold().FontSize(18);
                    column.Item().Text($"{note.StoreName} | {note.StoreDocument}");
                });

                page.Content().Column(column =>
                {
                    column.Spacing(8);
                    column.Item().Text($"Nota: {note.Number}");
                    column.Item().Text($"Cliente: {note.CustomerName}");
                    column.Item().Text($"Documento: {note.CustomerDocument}");
                    column.Item().Text($"Endereco: {note.CustomerAddress}");

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(4);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("Descricao").SemiBold();
                            header.Cell().Text("Qtd").SemiBold();
                            header.Cell().Text("Valor").SemiBold();
                            header.Cell().Text("Total").SemiBold();
                        });

                        foreach (var item in items)
                        {
                            table.Cell().Text(item.Description);
                            table.Cell().Text(item.Quantity.ToString("0.###"));
                            table.Cell().Text(item.UnitPrice.ToString("C"));
                            table.Cell().Text(item.TotalPrice.ToString("C"));
                        }
                    });

                    column.Item().Text($"Total geral: {note.TotalAmount:C}").SemiBold();
                    if (!string.IsNullOrWhiteSpace(note.Notes))
                    {
                        column.Item().Text($"Obs: {note.Notes}");
                    }
                });
            });
        });

        return document.GeneratePdf();
    }
}
