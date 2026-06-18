namespace MaterialPro.Domain;

public abstract class EntityBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
}

public enum UserRole
{
    Admin = 1,
    Manager = 2,
    Cashier = 3,
    Stock = 4
}

public enum Permission
{
    ManageUsers,
    ManageProducts,
    ManageSuppliers,
    ManageCustomers,
    ManageSales,
    OpenPdv,
    FinalizeSale,
    DiscountSale,
    CancelSale,
    ReprintReceipt,
    ViewSalesReports,
    OpenCash,
    SupplyCash,
    WithdrawCash,
    CloseCash,
    ViewCashHistory,
    ExportCashReports,
    ManageStock,
    AdjustStock,
    ManageInventory,
    ExportSuppliers,
    ExportStock,
    ViewFinancialHistory,
    ManageSettings,
    ViewReports
}

public static class RolePermissions
{
    public static IReadOnlyCollection<Permission> For(UserRole role)
    {
        return role switch
        {
            UserRole.Admin => Enum.GetValues<Permission>(),
            UserRole.Manager => new[] { Permission.ManageProducts, Permission.ManageCustomers, Permission.ManageSales, Permission.ManageStock, Permission.ViewReports },
            UserRole.Cashier => new[] { Permission.ManageCustomers, Permission.ManageSales },
            UserRole.Stock => new[] { Permission.ManageProducts, Permission.ManageStock, Permission.ViewReports },
            _ => Array.Empty<Permission>()
        };
    }
}

public sealed class AppUser : EntityBase
{
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Admin;
    public DateTime? LastLoginAtUtc { get; set; }
    public bool MustChangePassword { get; set; } = true;
    public string? Notes { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
    public DateTime? PasswordChangedAtUtc { get; set; }
}

public sealed class StoreProfile : EntityBase
{
    public string ProgramName { get; set; } = "MaterialPro";
    public string StoreName { get; set; } = "Minha Loja";
    public string Cnpj { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string LogoPath { get; set; } = string.Empty;
}

public sealed class Customer : EntityBase
{
    public string Code { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string DocumentNumber { get; set; } = string.Empty;
    public string StateRegistration { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string WhatsApp { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string AddressNumber { get; set; } = string.Empty;
    public string Complement { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public decimal CreditLimit { get; set; }
    public string Notes { get; set; } = string.Empty;
    public bool IsBlocked { get; set; }
}

public sealed class Supplier : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string Cnpj { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public PersonType PersonType { get; set; } = PersonType.Juridica;
    public string FantasyName { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public string StateRegistration { get; set; } = string.Empty;
    public string MunicipalRegistration { get; set; } = string.Empty;
    public string MobilePhone { get; set; } = string.Empty;
    public string WhatsApp { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string AddressNumber { get; set; } = string.Empty;
    public string Complement { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string ContactRole { get; set; } = string.Empty;
    public int DefaultPaymentTermDays { get; set; }
    public decimal PurchaseLimit { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public enum PersonType
{
    Fisica = 1,
    Juridica = 2
}

public sealed class Product : EntityBase
{
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Unit { get; set; } = "UN";
    public decimal SalePrice { get; set; }
    public decimal CostPrice { get; set; }
    public decimal StockQuantity { get; set; }
    public decimal MinimumStock { get; set; }
    public decimal MaximumStock { get; set; }
    public decimal ReservedStock { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string Ncm { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public Guid? SupplierId { get; set; }
}

public enum StockMovementType
{
    PurchaseEntry = 1,
    ManualEntry = 2,
    ReturnEntry = 3,
    AdjustmentEntry = 4,
    SaleExit = 5,
    LossExit = 6,
    BreakageExit = 7,
    AdjustmentExit = 8,
    TransferExit = 9,
    TransferEntry = 10,
    Reservation = 11,
    ReservationRelease = 12,
    InventoryAdjustment = 13
}

public sealed class StockMovement : EntityBase
{
    public Guid ProductId { get; set; }
    public StockMovementType Type { get; set; } = StockMovementType.ManualEntry;
    public decimal Quantity { get; set; }
    public decimal PreviousStock { get; set; }
    public decimal CurrentStock { get; set; }
    public Guid? UserId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string Warehouse { get; set; } = "Loja";
    public string Observation { get; set; } = string.Empty;
    public DateTime MovementAtUtc { get; set; } = DateTime.UtcNow;
}

public enum StockInventoryStatus
{
    Open = 1,
    Counted = 2,
    Closed = 3,
    Cancelled = 4
}

public sealed class StockInventory : EntityBase
{
    public DateTime InventoryDateUtc { get; set; } = DateTime.UtcNow;
    public Guid? UserId { get; set; }
    public StockInventoryStatus Status { get; set; } = StockInventoryStatus.Open;
    public string Observation { get; set; } = string.Empty;
}

public sealed class StockInventoryItem : EntityBase
{
    public Guid InventoryId { get; set; }
    public Guid ProductId { get; set; }
    public decimal SystemStock { get; set; }
    public decimal CountedStock { get; set; }
    public decimal Difference { get; set; }
}

public enum StockTransferStatus
{
    Pending = 1,
    Completed = 2,
    Cancelled = 3
}

public sealed class StockTransfer : EntityBase
{
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public string SourceWarehouse { get; set; } = "Loja";
    public string DestinationWarehouse { get; set; } = "Deposito Principal";
    public Guid? UserId { get; set; }
    public DateTime TransferDateUtc { get; set; } = DateTime.UtcNow;
    public StockTransferStatus Status { get; set; } = StockTransferStatus.Pending;
    public string Observation { get; set; } = string.Empty;
}

public enum StockReservationStatus
{
    Active = 1,
    Released = 2,
    Consumed = 3,
    Cancelled = 4
}

public sealed class StockReservation : EntityBase
{
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public DateTime ReservedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReleasedAtUtc { get; set; }
    public StockReservationStatus Status { get; set; } = StockReservationStatus.Active;
    public string Observation { get; set; } = string.Empty;
}

public sealed class Sale : EntityBase
{
    public Guid CustomerId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? CashSessionId { get; set; }
    public decimal SubtotalAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal SurchargeAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal ChangeAmount { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public string ReceiptNumber { get; set; } = string.Empty;
    public DateTime SoldAtUtc { get; set; } = DateTime.UtcNow;
    public SaleStatus Status { get; set; } = SaleStatus.Finalizada;
    public string Observation { get; set; } = string.Empty;
}

public enum SaleStatus
{
    Aberta = 1,
    Finalizada = 2,
    Cancelada = 3,
    DevolvidaParcial = 4,
    DevolvidaTotal = 5
}

public sealed class SaleItem : EntityBase
{
    public Guid SaleId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductDescription { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal SurchargeAmount { get; set; }
    public decimal TotalItem { get; set; }
    public bool StockDeducted { get; set; }
}

public sealed class SalePayment : EntityBase
{
    public Guid SaleId { get; set; }
    public string PaymentMethod { get; set; } = "DINHEIRO";
    public decimal Amount { get; set; }
    public int Installments { get; set; } = 1;
    public DateTime? FirstDueDateUtc { get; set; }
    public string Observation { get; set; } = string.Empty;
}

public sealed class SaleLog : EntityBase
{
    public Guid SaleId { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime LogAtUtc { get; set; } = DateTime.UtcNow;
    public string MachineIp { get; set; } = string.Empty;
}

public enum BudgetStatus
{
    Draft = 1,
    Sent = 2,
    Approved = 3,
    Rejected = 4,
    ConvertedToSale = 5
}

public sealed class Budget : EntityBase
{
    public string Number { get; set; } = string.Empty;
    public Guid? CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public BudgetStatus Status { get; set; } = BudgetStatus.Draft;
    public DateTime ValidUntilUtc { get; set; } = DateTime.UtcNow.AddDays(7);
    public string Notes { get; set; } = string.Empty;
}

public sealed class BudgetItem : EntityBase
{
    public Guid BudgetId { get; set; }
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
}

public enum CashMovementType
{
    Opening = 1,
    In = 2,
    Out = 3,
    Sale = 4,
    Close = 5,
    Supply = 6,
    Withdrawal = 7,
    DuplicateReceipt = 8,
    Cancellation = 9
}

public enum CashSessionStatus
{
    Aberto = 1,
    Fechado = 2,
    Cancelado = 3
}

public sealed class CashSession : EntityBase
{
    public string Code { get; set; } = string.Empty;
    public decimal OpeningAmount { get; set; }
    public decimal ClosingAmount { get; set; }
    public decimal CurrentAmount { get; set; }
    public decimal CashAmount { get; set; }
    public decimal PixAmount { get; set; }
    public decimal DebitCardAmount { get; set; }
    public decimal CreditCardAmount { get; set; }
    public decimal CreditSaleAmount { get; set; }
    public decimal SupplyAmount { get; set; }
    public decimal WithdrawalAmount { get; set; }
    public decimal TotalSalesAmount { get; set; }
    public decimal ReportedAmount { get; set; }
    public decimal DifferenceAmount { get; set; }
    public DateTime OpenedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAtUtc { get; set; }
    public Guid OpenedByUserId { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public CashSessionStatus Status { get; set; } = CashSessionStatus.Aberto;
    public string Observation { get; set; } = string.Empty;
}

public sealed class CashMovement : EntityBase
{
    public Guid CashSessionId { get; set; }
    public CashMovementType Type { get; set; }
    public string Origin { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public Guid? SaleId { get; set; }
    public Guid? DuplicateId { get; set; }
    public Guid? UserId { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public DateTime MovementAtUtc { get; set; } = DateTime.UtcNow;
    public string Observation { get; set; } = string.Empty;
}

public enum FinancialStatus
{
    Open = 1,
    Paid = 2,
    Cancelled = 3,
    Overdue = 4,
    Returned = 5
}

public enum FinancialType
{
    Payable = 1,
    Receivable = 2
}

public sealed class AccountPayable : EntityBase
{
    public Guid? SupplierId { get; set; }
    public string Number { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal OriginalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal BalanceAmount { get; set; }
    public DateTime DueDateUtc { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public FinancialStatus Status { get; set; } = FinancialStatus.Open;
    public string PaymentMethod { get; set; } = string.Empty;
}

public sealed class Purchase : EntityBase
{
    public Guid SupplierId { get; set; }
    public string Number { get; set; } = string.Empty;
    public DateTime PurchasedAtUtc { get; set; } = DateTime.UtcNow;
    public decimal TotalAmount { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public sealed class PurchaseItem : EntityBase
{
    public Guid PurchaseId { get; set; }
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
}

public sealed class Duplicate : EntityBase
{
    public string Number { get; set; } = string.Empty;
    public FinancialType Type { get; set; } = FinancialType.Receivable;
    public Guid? SaleId { get; set; }
    public Guid? BudgetId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal BalanceAmount { get; set; }
    public DateTime DueDateUtc { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public FinancialStatus Status { get; set; } = FinancialStatus.Open;
}

public sealed class FinancialMovement : EntityBase
{
    public string Number { get; set; } = string.Empty;
    public FinancialType Type { get; set; } = FinancialType.Payable;
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime MovementAtUtc { get; set; } = DateTime.UtcNow;
    public string Reference { get; set; } = string.Empty;
}

public sealed class SaleCancellation : EntityBase
{
    public Guid SaleId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public DateTime CancelledAtUtc { get; set; } = DateTime.UtcNow;
    public decimal TotalAmount { get; set; }
    public string Observation { get; set; } = string.Empty;
}

public sealed class AccountReceivable : EntityBase
{
    public string Number { get; set; } = string.Empty;
    public Guid? SaleId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal OriginalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal BalanceAmount { get; set; }
    public DateTime DueDateUtc { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public FinancialStatus Status { get; set; } = FinancialStatus.Open;
    public string PaymentMethod { get; set; } = string.Empty;
}

public sealed class SaleReturn : EntityBase
{
    public Guid SaleId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public decimal TotalReturnedAmount { get; set; }
    public string ProcessedBy { get; set; } = string.Empty;
    public DateTime ProcessedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class NonFiscalNote : EntityBase
{
    public string Number { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string StoreDocument { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerDocument { get; set; } = string.Empty;
    public string CustomerAddress { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime IssuedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class NonFiscalNoteItem : EntityBase
{
    public Guid NonFiscalNoteId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}

public enum SecurityEventType
{
    LoginSuccess = 1,
    LoginFailure = 2,
    PasswordChanged = 3,
    UserLocked = 4,
    UserUnlocked = 5,
    Audit = 6
}

public sealed class SecurityAuditEntry : EntityBase
{
    public Guid? UserId { get; set; }
    public SecurityEventType EventType { get; set; } = SecurityEventType.Audit;
    public string Area { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
}

public sealed class SecurityLoginAttempt : EntityBase
{
    public string Username { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public bool Success { get; set; }
    public string FailureReason { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public DateTime AttemptedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class SecuritySession : EntityBase
{
    public Guid UserId { get; set; }
    public string SessionKey { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastSeenAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public bool IsClosed { get; set; }
}
