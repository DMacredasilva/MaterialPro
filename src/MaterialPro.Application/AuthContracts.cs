using MaterialPro.Domain;

namespace MaterialPro.Application;

public sealed record LoginRequest(string Username, string Password);
public sealed record AuthenticationResult(bool Success, string Message, AppUser? User = null, string? SessionKey = null);
public sealed record ChangePasswordRequest(Guid UserId, string CurrentPassword, string NewPassword);
public sealed record SecurityAuditRequest(Guid? UserId, MaterialPro.Domain.SecurityEventType EventType, string Area, string Action, string EntityName, string EntityId, string Details, string MachineName, string IpAddress);
public sealed record SecurityLoginAttemptRequest(string Username, Guid? UserId, bool Success, string FailureReason, string MachineName, string IpAddress);
public sealed record SecuritySessionRequest(Guid UserId, string SessionKey, string MachineName, string IpAddress);

public interface IPasswordHasher
{
    string Hash(string password, string salt);
    bool Verify(string password, string salt, string hash);
    string CreateSalt();
}

public interface IUserRepository
{
    AppUser? FindByUsername(string username);
    AppUser? FindByEmail(string email);
    void Add(AppUser user);
    void Update(AppUser user);
    IReadOnlyList<AppUser> GetAll();
}

public interface IAuthService
{
    AuthenticationResult Login(LoginRequest request);
    AppUser CreateAdmin(string fullName, string username, string email, string password);
}

public interface IAuthorizationService
{
    bool HasPermission(AppUser user, MaterialPro.Domain.Permission permission);
}

public interface ISecurityService
{
    void RecordAudit(SecurityAuditRequest request);
    void RecordLoginAttempt(SecurityLoginAttemptRequest request);
    SecuritySession OpenSession(SecuritySessionRequest request);
    SecuritySession CloseSession(string sessionKey);
    void LockUser(Guid userId, string reason, DateTime? untilUtc = null);
    void UnlockUser(Guid userId);
    IReadOnlyList<MaterialPro.Domain.SecurityAuditEntry> GetAudits();
    IReadOnlyList<MaterialPro.Domain.SecurityLoginAttempt> GetLoginAttempts();
    IReadOnlyList<MaterialPro.Domain.SecuritySession> GetSessions();
}

public sealed record AuthSession(AppUser User, IReadOnlyCollection<MaterialPro.Domain.Permission> Permissions);
public sealed record StoreProfileRequest(string ProgramName, string StoreName, string Cnpj, string Address, string Phone, string LogoPath);

public sealed record CustomerUpsertRequest(
    string FullName,
    string DocumentNumber,
    string Phone,
    string Email,
    string Address,
    string City,
    string Code = "",
    string StateRegistration = "",
    string WhatsApp = "",
    string ZipCode = "",
    string AddressNumber = "",
    string Complement = "",
    string District = "",
    string State = "",
    decimal CreditLimit = 0,
    string Notes = "",
    bool IsActive = true,
    bool IsBlocked = false);
public sealed record CustomerSearchRequest(string Term = "", bool OnlyActive = true, bool IncludeBlocked = true);
public sealed record CustomerPurchaseHistoryItem(Guid SaleId, string ReceiptNumber, DateTime SoldAtUtc, decimal TotalAmount, string PaymentMethod, MaterialPro.Domain.SaleStatus Status);
public sealed record CustomerFinancialHistoryItem(string Number, string Description, DateTime DueDateUtc, decimal OriginalAmount, decimal PaidAmount, decimal BalanceAmount, MaterialPro.Domain.FinancialStatus Status);
public sealed record CustomerCreditSummary(decimal CreditLimit, decimal OpenBalance, decimal AvailableCredit, bool IsBlocked);
public sealed record CustomerReportRequest(string Term = "", bool OnlyActive = true, bool IncludeBlocked = true);
public sealed record SupplierUpsertRequest(
    string Name,
    string Cnpj,
    string Phone,
    string Email,
    string Address,
    string Code = "",
    MaterialPro.Domain.PersonType PersonType = MaterialPro.Domain.PersonType.Juridica,
    string FantasyName = "",
    string LegalName = "",
    string StateRegistration = "",
    string MunicipalRegistration = "",
    string MobilePhone = "",
    string WhatsApp = "",
    string Website = "",
    string ZipCode = "",
    string AddressNumber = "",
    string Complement = "",
    string District = "",
    string City = "",
    string State = "",
    string ContactName = "",
    string ContactRole = "",
    int DefaultPaymentTermDays = 0,
    decimal PurchaseLimit = 0,
    string Notes = "",
    bool IsActive = true);
public sealed record SupplierSearchRequest(string Term = "", bool OnlyActive = true, string City = "", string State = "");
public sealed record SupplierProductHistoryItem(Guid ProductId, string Sku, string Name, decimal LastCostPrice, DateTime? LastPurchaseAtUtc, decimal PurchasedQuantity);
public sealed record SupplierPurchaseHistoryItem(Guid PurchaseId, string Number, DateTime PurchasedAtUtc, decimal TotalAmount, string Notes);
public sealed record SupplierFinancialSummary(decimal OpenAmount, decimal PaidAmount, decimal OverdueAmount, int OpenCount, int PaidCount, int OverdueCount);
public sealed record SupplierPayableRequest(Guid SupplierId, string Number, string Description, decimal OriginalAmount, DateTime DueDateUtc, string PaymentMethod);
public sealed record SupplierImportOptions(bool UpdateExisting, bool IgnoreDuplicates);
public sealed record SupplierImportIssue(int RowNumber, string Severity, string Message);
public sealed record SupplierImportResult(int TotalRows, int ImportedRows, int UpdatedRows, int IgnoredRows, int ErrorRows, IReadOnlyList<SupplierImportIssue> Issues);
public sealed record SupplierReportRequest(string Term = "", bool OnlyActive = true, string City = "", string State = "", bool OnlyWithOpenPayables = false, bool OnlyWithOverduePayables = false);
public sealed record ProductUpsertRequest(
    string Sku,
    string Name,
    string Unit,
    decimal SalePrice,
    decimal CostPrice,
    decimal MinimumStock,
    string Barcode,
    string Description = "",
    string Category = "",
    string Brand = "",
    string Ncm = "",
    string Location = "",
    Guid? SupplierId = null);
public sealed record ProductSearchRequest(string Term = "", bool OnlyActive = true, bool OnlyLowStock = false);
public sealed record ProductImportOptions(bool UpdateExisting, bool IgnoreDuplicates);
public sealed record ProductImportIssue(int RowNumber, string Severity, string Message);
public sealed record ProductImportResult(int TotalRows, int ImportedRows, int UpdatedRows, int IgnoredRows, int ErrorRows, IReadOnlyList<ProductImportIssue> Issues);
public sealed record ProductReportRequest(string Term = "", bool OnlyActive = true, bool OnlyLowStock = false);
public sealed record StockMoveRequest(Guid ProductId, decimal Quantity, MaterialPro.Domain.StockMovementType Type, string Reason, string Reference = "", Guid? UserId = null, string Warehouse = "Loja", string Observation = "", bool AllowNegative = false);
public sealed record StockAdjustRequest(Guid ProductId, decimal NewStock, string Reason, Guid? UserId = null, string Warehouse = "Loja", bool UserCanAdjust = true);
public sealed record StockInventoryRequest(Guid? UserId, string Observation);
public sealed record StockInventoryItemRequest(Guid ProductId, decimal CountedStock);
public sealed record StockTransferRequest(Guid ProductId, decimal Quantity, string SourceWarehouse, string DestinationWarehouse, Guid? UserId, string Observation = "", bool AllowNegative = false);
public sealed record StockReservationRequest(Guid ProductId, decimal Quantity, string Source, string Reference, Guid? UserId = null, string Observation = "");
public sealed record StockQueryRequest(string Term = "", string Category = "", string Brand = "", Guid? SupplierId = null, bool OnlyLowStock = false, bool OnlyZeroStock = false);
public sealed record StockPositionItem(Guid ProductId, string Sku, string Name, string Category, string Brand, string SupplierName, decimal PhysicalStock, decimal ReservedStock, decimal AvailableStock, decimal MinimumStock, DateTime? LastEntryAtUtc, DateTime? LastExitAtUtc);
public sealed record StockDashboardSummary(int TotalProducts, decimal TotalStock, int LowStockProducts, int ZeroStockProducts, IReadOnlyList<StockMovement> LastMovements);
public sealed record StockReportRequest(string Term = "", bool OnlyLowStock = false, bool OnlyZeroStock = false, bool OnlyEntries = false, bool OnlyExits = false, Guid? SupplierId = null);
public sealed record StockImportOptions(bool UpdateProducts, bool AllowNegative);
public sealed record StockImportIssue(int RowNumber, string Severity, string Message);
public sealed record StockImportResult(int TotalRows, int ImportedRows, int UpdatedRows, int IgnoredRows, int ErrorRows, IReadOnlyList<StockImportIssue> Issues);
public sealed record BudgetCreateRequest(string Number, Guid? CustomerId, decimal DiscountAmount, DateTime ValidUntilUtc, string Notes);
public sealed record BudgetItemRequest(Guid ProductId, decimal Quantity, decimal UnitPrice, decimal DiscountAmount);
public sealed record SaleCreateRequest(Guid? CustomerId, string PaymentMethod, decimal DiscountAmount, decimal PaidAmount, string ReceiptNumber);
public sealed record SaleItemRequest(Guid ProductId, decimal Quantity, decimal UnitPrice, decimal DiscountAmount);
public sealed record PdvCreateSaleRequest(Guid? CustomerId, Guid? UserId, Guid? CashSessionId, string Observation = "");
public sealed record PdvSaleItemRequest(Guid ProductId, decimal Quantity, decimal UnitPrice, decimal DiscountAmount = 0, decimal SurchargeAmount = 0);
public sealed record PdvPaymentRequest(string PaymentMethod, decimal Amount, int Installments = 1, DateTime? FirstDueDateUtc = null, string Observation = "");
public sealed record PdvFinalizeRequest(Guid SaleId, IReadOnlyList<PdvPaymentRequest> Payments, decimal DiscountAmount = 0, decimal SurchargeAmount = 0, bool AllowNegativeStock = false, bool ManagerAuthorized = false);
public sealed record PdvReceiptRequest(Guid SaleId, InternalPaperFormat Format = InternalPaperFormat.Thermal80);
public sealed record PdvSaleSearchRequest(string Number = "", string Customer = "", DateTime? FromUtc = null, DateTime? ToUtc = null, decimal? Amount = null, Guid? UserId = null);
public sealed record SalesReportRequest(DateTime? FromUtc = null, DateTime? ToUtc = null, Guid? CustomerId = null, Guid? UserId = null, Guid? ProductId = null, string PaymentMethod = "", bool OnlyCancelled = false, bool OnlyCredit = false);
public sealed record ProductSalesSummary(Guid ProductId, string Sku, string Name, decimal Quantity, decimal TotalAmount);
public sealed record CashOpenRequest(decimal OpeningAmount, Guid OpenedByUserId);
public sealed record CashMovementRequest(Guid CashSessionId, MaterialPro.Domain.CashMovementType Type, decimal Amount, string Description, Guid? SaleId = null);
public sealed record CashSupplyRequest(Guid CashSessionId, decimal Amount, string Reason, Guid UserId);
public sealed record CashWithdrawalRequest(Guid CashSessionId, decimal Amount, string Reason, Guid UserId, string ManagerPassword);
public sealed record CashCloseRequest(Guid CashSessionId, Guid UserId, decimal CountedCash, decimal CountedPix, decimal CountedDebitCard, decimal CountedCreditCard, string Observation);
public sealed record CashHistoryRequest(DateTime? FromUtc = null, DateTime? ToUtc = null, Guid? OperatorId = null, MaterialPro.Domain.CashSessionStatus? Status = null, string Code = "");
public sealed record CashDashboardSummary(bool HasOpenCash, string OpenCashCode, decimal TodaySales, decimal TodayCash, decimal TodayPix, decimal TodayDebitCard, decimal TodayCreditCard, decimal TodayWithdrawals, decimal TodaySupplies, decimal TodayDifference);
public sealed record CashReportRequest(DateTime? FromUtc = null, DateTime? ToUtc = null, Guid? OperatorId = null, bool OnlyWithdrawals = false, bool OnlySupplies = false, bool OnlyDifferences = false);
public sealed record AccountPayableRequest(string Number, string SupplierName, string Description, decimal OriginalAmount, DateTime DueDateUtc, string PaymentMethod);
public sealed record AccountReceivableRequest(string Number, Guid? CustomerId, Guid? SaleId, string CustomerName, string Description, decimal OriginalAmount, DateTime DueDateUtc, string ReceiveMethod);
public sealed record FinancialSettlementRequest(Guid DocumentId, MaterialPro.Domain.FinancialType Type, decimal Amount, decimal Interest = 0, decimal Fine = 0, decimal Discount = 0, string PaymentMethod = "DINHEIRO", Guid? UserId = null, Guid? CashSessionId = null, string Observation = "");
public sealed record FinancialCancelRequest(Guid DocumentId, MaterialPro.Domain.FinancialType Type, string Reason, Guid? UserId = null);
public sealed record FinancialSearchRequest(DateTime? FromUtc = null, DateTime? ToUtc = null, string Term = "", MaterialPro.Domain.FinancialStatus? Status = null, bool OnlyOverdue = false);
public sealed record FinancialDashboardSummary(decimal PayableToday, decimal PayableOverdue, decimal ReceivableToday, decimal ReceivableOverdue, decimal ReceivedThisMonth, decimal PaidThisMonth, decimal ForecastBalance, IReadOnlyList<FinancialCashFlowItem> CashFlow, IReadOnlyList<string> Alerts);
public sealed record FinancialCashFlowItem(DateTime DateUtc, decimal ExpectedIn, decimal ExpectedOut, decimal RealizedIn, decimal RealizedOut, decimal DailyBalance, decimal ForecastBalance);
public sealed record DuplicateRequest(string Number, MaterialPro.Domain.FinancialType Type, Guid? SaleId, Guid? BudgetId, decimal Amount, DateTime DueDateUtc);
public sealed record FinancialMovementRequest(string Number, MaterialPro.Domain.FinancialType Type, decimal Amount, string Description, string Reference);
public sealed record SaleCancellationRequest(Guid SaleId, string Reason, Guid UserId, string ManagerPassword, string Observation);
public sealed record SaleReturnRequest(Guid SaleId, string Reason, decimal TotalReturnedAmount, string ProcessedBy);
public sealed record ReportRangeRequest(DateTime FromUtc, DateTime ToUtc);
public sealed record ReportFilterRequest(
    string ReportKey,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    Guid? CustomerId = null,
    Guid? SupplierId = null,
    Guid? ProductId = null,
    Guid? UserId = null,
    string Status = "",
    string Term = "");
public sealed record ReportDefinition(string Key, string Title, MaterialPro.Domain.ReportGroup Group, bool FinancialRestricted, bool SupportsThermalSummary);
public sealed record ReportRow(IReadOnlyDictionary<string, object?> Values);
public sealed record ReportResult(ReportDefinition Definition, ReportFilterRequest Filters, IReadOnlyList<string> Columns, IReadOnlyList<ReportRow> Rows, IReadOnlyDictionary<string, decimal> Totals);
public sealed record ReportsDashboardSummary(decimal TotalSales, decimal TotalReceived, decimal TotalReceivable, decimal TotalPayable, decimal GrossProfit, decimal StockValue, IReadOnlyList<ProductSalesSummary> BestProducts, IReadOnlyList<CustomerPurchaseSummary> BestCustomers);
public sealed record CustomerPurchaseSummary(Guid CustomerId, string Name, decimal TotalAmount, int SaleCount);
public sealed record SaleCancellationSummary(Guid Id, Guid SaleId, string ReceiptNumber, string Reason, string UserName, DateTime CancelledAtUtc, decimal TotalAmount, string Observation);
public enum InternalDocumentKind
{
    SaleCoupon = 1,
    SaleReceipt = 2,
    Budget = 3,
    PaymentProof = 4,
    SaleSecondCopy = 5
}

public enum InternalPaperFormat
{
    Thermal58 = 1,
    Thermal80 = 2,
    A4 = 3
}

public sealed record InternalDocumentRequest(
    InternalDocumentKind Kind,
    InternalPaperFormat PaperFormat,
    string Number,
    string CustomerName,
    string Reference,
    decimal TotalAmount,
    string PaymentMethod,
    string Notes,
    IEnumerable<string> Lines);
public sealed record NonFiscalNoteItemRequest(string Description, decimal Quantity, decimal UnitPrice);
public sealed record NonFiscalNoteRequest(
    string Number,
    string StoreName,
    string StoreDocument,
    string CustomerName,
    string CustomerDocument,
    string CustomerAddress,
    string Notes,
    IEnumerable<NonFiscalNoteItemRequest> Items);

public interface ICustomerService
{
    Customer Create(CustomerUpsertRequest request);
    Customer Update(Guid id, CustomerUpsertRequest request);
    Customer? FindById(Guid id);
    Customer? FindByCodeOrDocument(string value);
    IReadOnlyList<Customer> Search(CustomerSearchRequest request);
    IReadOnlyList<Customer> List();
    Customer Inactivate(Guid id);
    Customer Block(Guid id, string reason);
    Customer Unblock(Guid id);
    IReadOnlyList<CustomerPurchaseHistoryItem> PurchaseHistory(Guid customerId);
    IReadOnlyList<CustomerFinancialHistoryItem> FinancialHistory(Guid customerId);
    CustomerCreditSummary CreditSummary(Guid customerId);
}

public interface ICustomerRepository
{
    void Add(Customer customer);
    void Update(Customer customer);
    Customer? FindById(Guid id);
    Customer? FindByCode(string code);
    Customer? FindByDocument(string documentNumber);
    IReadOnlyList<Customer> Search(CustomerSearchRequest request);
}

public interface ICustomerReportService
{
    byte[] ExportPdf(CustomerReportRequest request);
    byte[] ExportExcel(CustomerReportRequest request);
    byte[] ExportCsv(CustomerReportRequest request);
    byte[] CustomerFichaPdf(Guid customerId);
}

public interface ISupplierService
{
    Supplier Create(SupplierUpsertRequest request);
    Supplier Update(Guid id, SupplierUpsertRequest request);
    Supplier? FindById(Guid id);
    Supplier? FindByCodeOrCnpj(string value);
    IReadOnlyList<Supplier> Search(SupplierSearchRequest request);
    IReadOnlyList<Supplier> List();
    Supplier Inactivate(Guid id);
    Supplier LinkProduct(Guid supplierId, Guid productId);
    IReadOnlyList<SupplierProductHistoryItem> Products(Guid supplierId);
    IReadOnlyList<SupplierPurchaseHistoryItem> PurchaseHistory(Guid supplierId);
    SupplierFinancialSummary FinancialSummary(Guid supplierId);
    IReadOnlyList<AccountPayable> Payables(Guid supplierId);
    AccountPayable CreatePayable(SupplierPayableRequest request);
}

public interface ISupplierRepository
{
    void Add(Supplier supplier);
    void Update(Supplier supplier);
    Supplier? FindById(Guid id);
    Supplier? FindByCode(string code);
    Supplier? FindByCnpj(string cnpj);
    IReadOnlyList<Supplier> Search(SupplierSearchRequest request);
}

public interface ISupplierImportService
{
    SupplierImportResult ImportCsv(string filePath, SupplierImportOptions options);
    SupplierImportResult ImportExcel(string filePath, SupplierImportOptions options);
    SupplierImportResult ImportDbf(string filePath, SupplierImportOptions options);
}

public interface ISupplierReportService
{
    byte[] ExportPdf(SupplierReportRequest request);
    byte[] ExportExcel(SupplierReportRequest request);
    byte[] SupplierFichaPdf(Guid supplierId);
}

public interface IProductService
{
    Product Create(ProductUpsertRequest request);
    Product Update(Guid id, ProductUpsertRequest request);
    Product? FindById(Guid id);
    Product? FindBySkuOrBarcode(string value);
    IReadOnlyList<Product> Search(ProductSearchRequest request);
    IReadOnlyList<Product> List();
}

public interface IInventoryService
{
    StockMovement Move(Guid productId, decimal quantity, string reason, string reference);
    decimal GetStock(Guid productId);
    StockMovement EnterStock(StockMoveRequest request);
    StockMovement ExitStock(StockMoveRequest request);
    StockMovement AdjustStock(StockAdjustRequest request);
    StockInventory StartInventory(StockInventoryRequest request);
    StockInventoryItem CountInventoryItem(Guid inventoryId, StockInventoryItemRequest request);
    StockInventory CloseInventory(Guid inventoryId, bool applyAdjustments, Guid? userId = null);
    StockTransfer Transfer(StockTransferRequest request);
    StockReservation Reserve(StockReservationRequest request);
    StockReservation ReleaseReservation(Guid reservationId, Guid? userId = null, string observation = "");
    IReadOnlyList<StockPositionItem> Query(StockQueryRequest request);
    IReadOnlyList<StockMovement> Movements(Guid? productId = null);
    StockDashboardSummary Dashboard();
}

public interface IStockReportService
{
    byte[] ExportPdf(StockReportRequest request);
    byte[] ExportExcel(StockReportRequest request);
}

public interface IStockImportService
{
    StockImportResult ImportCsv(string filePath, StockImportOptions options);
    StockImportResult ImportExcel(string filePath, StockImportOptions options);
    StockImportResult ImportDbf(string filePath, StockImportOptions options);
}

public interface IProductRepository
{
    void Add(Product product);
    void Update(Product product);
    Product? FindById(Guid id);
    Product? FindBySku(string sku);
    Product? FindByBarcode(string barcode);
    IReadOnlyList<Product> Search(ProductSearchRequest request);
}

public interface IProductImportService
{
    ProductImportResult ImportCsv(string filePath, ProductImportOptions options);
    ProductImportResult ImportExcel(string filePath, ProductImportOptions options);
    ProductImportResult ImportDbf(string filePath, ProductImportOptions options);
}

public interface IProductReportService
{
    byte[] ExportPdf(ProductReportRequest request);
    byte[] ExportExcel(ProductReportRequest request);
}

public interface IBudgetService
{
    Budget CreateBudget(BudgetCreateRequest request, IEnumerable<BudgetItemRequest> items);
    IReadOnlyList<Budget> List();
}

public interface ISalesService
{
    Sale CreateSale(SaleCreateRequest request, IEnumerable<SaleItemRequest> items);
    IReadOnlyList<Sale> List();
}

public interface IPdvService
{
    Sale CreateSale(PdvCreateSaleRequest request);
    SaleItem AddItem(Guid saleId, PdvSaleItemRequest request);
    Sale RemoveItem(Guid saleItemId);
    Sale ApplyDiscount(Guid saleId, decimal discountAmount, bool managerAuthorized = false);
    Sale FinalizeSale(PdvFinalizeRequest request);
    SaleCancellation CancelSale(SaleCancellationRequest request);
    IReadOnlyList<Sale> Search(PdvSaleSearchRequest request);
    IReadOnlyList<SaleItem> Items(Guid saleId);
    IReadOnlyList<SalePayment> Payments(Guid saleId);
    byte[] GenerateReceiptPdf(PdvReceiptRequest request);
}

public interface ISalesReportService
{
    byte[] ExportPdf(SalesReportRequest request);
    byte[] ExportExcel(SalesReportRequest request);
    IReadOnlyList<ProductSalesSummary> BestSellingProducts(DateTime? fromUtc = null, DateTime? toUtc = null);
}

public interface ICashService
{
    CashSession Open(CashOpenRequest request);
    CashMovement RegisterMovement(CashMovementRequest request);
    CashSession? ActiveSession();
    CashMovement Supply(CashSupplyRequest request);
    CashMovement Withdraw(CashWithdrawalRequest request);
    CashSession Close(CashCloseRequest request);
    IReadOnlyList<CashSession> History(CashHistoryRequest request);
    IReadOnlyList<CashMovement> Movements(Guid cashSessionId);
    CashDashboardSummary Dashboard();
    byte[] PrintOpening(Guid cashSessionId, InternalPaperFormat format = InternalPaperFormat.Thermal80);
    byte[] PrintMovement(Guid movementId, InternalPaperFormat format = InternalPaperFormat.Thermal80);
    byte[] PrintClosing(Guid cashSessionId, InternalPaperFormat format = InternalPaperFormat.A4);
}

public interface ICashReportService
{
    byte[] ExportPdf(CashReportRequest request);
    byte[] ExportExcel(CashReportRequest request);
}

public interface IFinancialService
{
    AccountPayable CreatePayable(AccountPayableRequest request);
    AccountReceivable CreateReceivable(AccountReceivableRequest request);
    AccountPayable PayableBaixa(Guid id, decimal amount, string paymentMethod);
    AccountPayable SettlePayable(FinancialSettlementRequest request);
    AccountReceivable SettleReceivable(FinancialSettlementRequest request);
    AccountPayable CancelPayable(Guid id, string reason);
    AccountReceivable CancelReceivable(Guid id, string reason);
    Duplicate CreateDuplicate(DuplicateRequest request);
    Duplicate DuplicateBaixa(Guid id, decimal amount);
    Duplicate SettleDuplicate(FinancialSettlementRequest request);
    Duplicate CancelDuplicate(Guid id, string reason);
    Duplicate IssueSecondCopy(Guid id);
    FinancialMovement RegisterMovement(FinancialMovementRequest request);
    IReadOnlyList<AccountPayable> SearchPayables(FinancialSearchRequest request);
    IReadOnlyList<AccountReceivable> SearchReceivables(FinancialSearchRequest request);
    IReadOnlyList<Duplicate> SearchDuplicates(FinancialSearchRequest request);
    IReadOnlyList<FinancialSettlement> Settlements(Guid? documentId = null);
    IReadOnlyList<FinancialCategory> Categories(MaterialPro.Domain.FinancialType? type = null);
    FinancialCategory CreateCategory(string name, MaterialPro.Domain.FinancialType type);
    FinancialDashboardSummary Dashboard(DateTime? todayUtc = null);
    IReadOnlyList<FinancialCashFlowItem> CashFlow(DateTime fromUtc, DateTime toUtc);
    byte[] ExportPdf(FinancialSearchRequest request);
    byte[] ExportExcel(FinancialSearchRequest request);
    byte[] PrintReceipt(Guid settlementId, InternalPaperFormat format = InternalPaperFormat.Thermal80);
    SaleCancellation CancelSale(SaleCancellationRequest request);
    SaleReturn ReturnSale(SaleReturnRequest request);
    AccountPayable ReversePayable(Guid id, string reason);
}

public interface ISaleCancellationService
{
    SaleCancellation CancelSale(SaleCancellationRequest request);
    IReadOnlyList<SaleCancellationSummary> ListCancelledSales();
    IReadOnlyList<SaleCancellationSummary> Report(DateTime fromUtc, DateTime toUtc);
    byte[] GenerateProofPdf(Guid cancellationId);
    byte[] GenerateReportPdf(DateTime fromUtc, DateTime toUtc);
}

public interface IReportService
{
    byte[] ExportSalesPdf(ReportRangeRequest request);
    byte[] ExportSalesExcel(ReportRangeRequest request);
    byte[] ExportFinancialPdf(ReportRangeRequest request);
    byte[] ExportFinancialExcel(ReportRangeRequest request);
    byte[] ExportDuplicateSecondCopyPdf(Guid duplicateId);
}

public interface IReportsCenterService
{
    IReadOnlyList<ReportDefinition> Catalog();
    ReportResult Generate(ReportFilterRequest request, AppUser user);
    ReportsDashboardSummary Dashboard(ReportFilterRequest request, AppUser user);
    byte[] ExportPdf(ReportFilterRequest request, AppUser user);
    byte[] ExportExcel(ReportFilterRequest request, AppUser user);
    byte[] PrintSummary(ReportFilterRequest request, AppUser user, InternalPaperFormat format = InternalPaperFormat.A4);
    IReadOnlyList<ReportAuditLog> Logs();
    ReportSchedule Schedule(string reportKey, string frequency, string outputFolder);
}

public interface IPrintService
{
    void PrintText(string title, IEnumerable<string> lines);
}

public interface IInternalDocumentService
{
    byte[] GeneratePdf(InternalDocumentRequest request);
}

public interface IStoreProfileService
{
    StoreProfile Get();
    StoreProfile Save(StoreProfileRequest request);
}

public interface IFiscalConfigurationService
{
    bool IsReady();
    IReadOnlyList<string> Validate();
}

public interface INonFiscalNoteService
{
    NonFiscalNote Create(NonFiscalNoteRequest request);
    IReadOnlyList<NonFiscalNote> List();
    byte[] GeneratePdf(Guid noteId);
}

public interface IUserSecurityService
{
    void ChangePassword(ChangePasswordRequest request);
}
