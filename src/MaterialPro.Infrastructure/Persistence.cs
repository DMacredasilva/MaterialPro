using System.Security.Cryptography;
using MaterialPro.Application;
using MaterialPro.Domain;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace MaterialPro.Infrastructure;

public sealed partial class MaterialProDbContext : DbContext
{
    public MaterialProDbContext(DbContextOptions<MaterialProDbContext> options) : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<AccountReceivable> AccountsReceivable => Set<AccountReceivable>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.Property(x => x.FullName).HasMaxLength(150);
            entity.Property(x => x.Username).HasMaxLength(80);
            entity.Property(x => x.Email).HasMaxLength(180);
            entity.Property(x => x.PasswordHash).HasMaxLength(255);
            entity.Property(x => x.PasswordSalt).HasMaxLength(255);
            entity.Property(x => x.Notes).HasMaxLength(500);
        });

        modelBuilder.Entity<StoreProfile>(entity =>
        {
            entity.Property(x => x.ProgramName).HasMaxLength(120);
            entity.Property(x => x.StoreName).HasMaxLength(180);
            entity.Property(x => x.Cnpj).HasMaxLength(30);
            entity.Property(x => x.Address).HasMaxLength(220);
            entity.Property(x => x.Phone).HasMaxLength(40);
            entity.Property(x => x.LogoPath).HasMaxLength(300);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.Property(x => x.Code).HasMaxLength(40);
            entity.Property(x => x.FullName).HasMaxLength(150);
            entity.Property(x => x.DocumentNumber).HasMaxLength(30);
            entity.Property(x => x.StateRegistration).HasMaxLength(30);
            entity.Property(x => x.Phone).HasMaxLength(30);
            entity.Property(x => x.WhatsApp).HasMaxLength(30);
            entity.Property(x => x.Email).HasMaxLength(180);
            entity.Property(x => x.ZipCode).HasMaxLength(20);
            entity.Property(x => x.CreditLimit).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.Property(x => x.Code).HasMaxLength(40);
            entity.Property(x => x.Name).HasMaxLength(150);
            entity.Property(x => x.FantasyName).HasMaxLength(150);
            entity.Property(x => x.LegalName).HasMaxLength(180);
            entity.Property(x => x.Cnpj).HasMaxLength(30);
            entity.Property(x => x.StateRegistration).HasMaxLength(30);
            entity.Property(x => x.MunicipalRegistration).HasMaxLength(30);
            entity.Property(x => x.Phone).HasMaxLength(30);
            entity.Property(x => x.MobilePhone).HasMaxLength(30);
            entity.Property(x => x.WhatsApp).HasMaxLength(30);
            entity.Property(x => x.Email).HasMaxLength(180);
            entity.Property(x => x.Website).HasMaxLength(180);
            entity.Property(x => x.ZipCode).HasMaxLength(20);
            entity.Property(x => x.ContactName).HasMaxLength(150);
            entity.Property(x => x.ContactRole).HasMaxLength(120);
            entity.Property(x => x.PurchaseLimit).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.Property(x => x.Sku).HasMaxLength(60);
            entity.Property(x => x.Name).HasMaxLength(180);
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.Property(x => x.Category).HasMaxLength(120);
            entity.Property(x => x.Brand).HasMaxLength(120);
            entity.Property(x => x.Unit).HasMaxLength(20);
            entity.Property(x => x.SalePrice).HasPrecision(18, 2);
            entity.Property(x => x.CostPrice).HasPrecision(18, 2);
            entity.Property(x => x.StockQuantity).HasPrecision(18, 3);
            entity.Property(x => x.MinimumStock).HasPrecision(18, 3);
            entity.Property(x => x.MaximumStock).HasPrecision(18, 3);
            entity.Property(x => x.ReservedStock).HasPrecision(18, 3);
        });

        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.ToTable("estoque_movimentos");
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.ProductId).HasColumnName("produto_id");
            entity.Property(x => x.Type).HasColumnName("tipo_movimento");
            entity.Property(x => x.Quantity).HasColumnName("quantidade").HasPrecision(18, 3);
            entity.Property(x => x.PreviousStock).HasColumnName("estoque_anterior").HasPrecision(18, 3);
            entity.Property(x => x.CurrentStock).HasColumnName("estoque_atual").HasPrecision(18, 3);
            entity.Property(x => x.UserId).HasColumnName("usuario_id");
            entity.Property(x => x.MovementAtUtc).HasColumnName("data_movimento");
            entity.Property(x => x.Observation).HasColumnName("observacao").HasMaxLength(500);
            entity.Property(x => x.Reason).HasMaxLength(180);
            entity.Property(x => x.Reference).HasMaxLength(120);
            entity.Property(x => x.Warehouse).HasMaxLength(120);
        });

        modelBuilder.Entity<StockInventory>(entity =>
        {
            entity.ToTable("estoque_inventario");
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.InventoryDateUtc).HasColumnName("data_inventario");
            entity.Property(x => x.UserId).HasColumnName("usuario_id");
            entity.Property(x => x.Status).HasColumnName("status");
            entity.Property(x => x.Observation).HasColumnName("observacao").HasMaxLength(500);
        });

        modelBuilder.Entity<StockInventoryItem>(entity =>
        {
            entity.ToTable("estoque_inventario_itens");
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.InventoryId).HasColumnName("inventario_id");
            entity.Property(x => x.ProductId).HasColumnName("produto_id");
            entity.Property(x => x.SystemStock).HasColumnName("estoque_sistema").HasPrecision(18, 3);
            entity.Property(x => x.CountedStock).HasColumnName("estoque_contado").HasPrecision(18, 3);
            entity.Property(x => x.Difference).HasColumnName("diferenca").HasPrecision(18, 3);
        });

        modelBuilder.Entity<StockTransfer>(entity =>
        {
            entity.ToTable("estoque_transferencias");
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.ProductId).HasColumnName("produto_id");
            entity.Property(x => x.Quantity).HasColumnName("quantidade").HasPrecision(18, 3);
            entity.Property(x => x.SourceWarehouse).HasColumnName("deposito_origem").HasMaxLength(120);
            entity.Property(x => x.DestinationWarehouse).HasColumnName("deposito_destino").HasMaxLength(120);
            entity.Property(x => x.UserId).HasColumnName("usuario_id");
            entity.Property(x => x.TransferDateUtc).HasColumnName("data_transferencia");
            entity.Property(x => x.Status).HasColumnName("status");
            entity.Property(x => x.Observation).HasColumnName("observacao").HasMaxLength(500);
        });

        modelBuilder.Entity<StockReservation>(entity =>
        {
            entity.ToTable("estoque_reservas");
            entity.Property(x => x.ProductId).HasColumnName("produto_id");
            entity.Property(x => x.Quantity).HasColumnName("quantidade").HasPrecision(18, 3);
            entity.Property(x => x.Source).HasMaxLength(80);
            entity.Property(x => x.Reference).HasMaxLength(120);
            entity.Property(x => x.Observation).HasMaxLength(500);
        });

        modelBuilder.Entity<Sale>(entity =>
        {
            entity.Property(x => x.PaymentMethod).HasMaxLength(40);
            entity.Property(x => x.ReceiptNumber).HasMaxLength(80);
            entity.Property(x => x.Observation).HasMaxLength(500);
            entity.Property(x => x.SubtotalAmount).HasPrecision(18, 2);
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
            entity.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            entity.Property(x => x.SurchargeAmount).HasPrecision(18, 2);
            entity.Property(x => x.PaidAmount).HasPrecision(18, 2);
            entity.Property(x => x.ChangeAmount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<AccountReceivable>(entity =>
        {
            entity.ToTable("contas_receber");
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.CustomerId).HasColumnName("cliente_id");
            entity.Property(x => x.SaleId).HasColumnName("venda_id");
            entity.Property(x => x.Number).HasMaxLength(60);
            entity.Property(x => x.Number).HasColumnName("numero_documento");
            entity.Property(x => x.CustomerName).HasMaxLength(160);
            entity.Property(x => x.CustomerName).HasColumnName("CustomerName");
            entity.Property(x => x.Description).HasMaxLength(300);
            entity.Property(x => x.Description).HasColumnName("descricao");
            entity.Property(x => x.DocumentNumber).HasColumnName("DocumentNumber").HasMaxLength(80);
            entity.Property(x => x.IssueDateUtc).HasColumnName("data_emissao");
            entity.Property(x => x.PaymentMethod).HasMaxLength(60);
            entity.Property(x => x.PaymentMethod).HasColumnName("forma_recebimento");
            entity.Property(x => x.OriginalAmount).HasPrecision(18, 2);
            entity.Property(x => x.OriginalAmount).HasColumnName("valor_original");
            entity.Property(x => x.PaidAmount).HasPrecision(18, 2);
            entity.Property(x => x.PaidAmount).HasColumnName("valor_recebido");
            entity.Property(x => x.BalanceAmount).HasPrecision(18, 2);
            entity.Property(x => x.BalanceAmount).HasColumnName("saldo");
            entity.Property(x => x.InterestAmount).HasColumnName("juros").HasPrecision(18, 2);
            entity.Property(x => x.FineAmount).HasColumnName("multa").HasPrecision(18, 2);
            entity.Property(x => x.DiscountAmount).HasColumnName("desconto").HasPrecision(18, 2);
            entity.Property(x => x.DueDateUtc).HasColumnName("data_vencimento");
            entity.Property(x => x.PaidAtUtc).HasColumnName("data_recebimento");
            entity.Property(x => x.UserId).HasColumnName("usuario_id");
            entity.Property(x => x.Status).HasColumnName("status");
            entity.Property(x => x.Observation).HasColumnName("observacao").HasMaxLength(500);
        });

        modelBuilder.Entity<SaleItem>(entity =>
        {
            entity.Property(x => x.ProductCode).HasMaxLength(80);
            entity.Property(x => x.ProductDescription).HasMaxLength(220);
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            entity.Property(x => x.SurchargeAmount).HasPrecision(18, 2);
            entity.Property(x => x.TotalItem).HasPrecision(18, 2);
        });

        modelBuilder.Entity<SalePayment>(entity =>
        {
            entity.ToTable("venda_pagamentos");
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.SaleId).HasColumnName("venda_id");
            entity.Property(x => x.PaymentMethod).HasColumnName("forma_pagamento").HasMaxLength(40);
            entity.Property(x => x.Amount).HasColumnName("valor").HasPrecision(18, 2);
            entity.Property(x => x.Installments).HasColumnName("parcelas");
            entity.Property(x => x.FirstDueDateUtc).HasColumnName("vencimento_primeira_parcela");
            entity.Property(x => x.Observation).HasColumnName("observacao").HasMaxLength(300);
        });

        modelBuilder.Entity<SaleLog>(entity =>
        {
            entity.ToTable("vendas_logs");
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.SaleId).HasColumnName("venda_id");
            entity.Property(x => x.UserId).HasColumnName("usuario_id");
            entity.Property(x => x.Action).HasColumnName("acao").HasMaxLength(80);
            entity.Property(x => x.Description).HasColumnName("descricao").HasMaxLength(500);
            entity.Property(x => x.LogAtUtc).HasColumnName("data_log");
            entity.Property(x => x.MachineIp).HasColumnName("ip_maquina").HasMaxLength(80);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.Property(x => x.Address).HasMaxLength(220);
            entity.Property(x => x.AddressNumber).HasMaxLength(20);
            entity.Property(x => x.Complement).HasMaxLength(120);
            entity.Property(x => x.District).HasMaxLength(120);
            entity.Property(x => x.City).HasMaxLength(120);
            entity.Property(x => x.State).HasMaxLength(2);
            entity.Property(x => x.Notes).HasMaxLength(500);
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.Property(x => x.Address).HasMaxLength(220);
            entity.Property(x => x.AddressNumber).HasMaxLength(20);
            entity.Property(x => x.Complement).HasMaxLength(120);
            entity.Property(x => x.District).HasMaxLength(120);
            entity.Property(x => x.City).HasMaxLength(120);
            entity.Property(x => x.State).HasMaxLength(2);
            entity.Property(x => x.Notes).HasMaxLength(500);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.Property(x => x.Barcode).HasMaxLength(80);
            entity.Property(x => x.Ncm).HasMaxLength(20);
            entity.Property(x => x.Location).HasMaxLength(120);
        });

        modelBuilder.Entity<Budget>(entity =>
        {
            entity.Property(x => x.Number).HasMaxLength(60);
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
            entity.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            entity.Property(x => x.Notes).HasMaxLength(500);
        });

        modelBuilder.Entity<BudgetItem>(entity =>
        {
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<CashSession>(entity =>
        {
            entity.Property(x => x.Code).HasMaxLength(40);
            entity.Property(x => x.OpeningAmount).HasPrecision(18, 2);
            entity.Property(x => x.ClosingAmount).HasPrecision(18, 2);
            entity.Property(x => x.CurrentAmount).HasPrecision(18, 2);
            entity.Property(x => x.CashAmount).HasPrecision(18, 2);
            entity.Property(x => x.PixAmount).HasPrecision(18, 2);
            entity.Property(x => x.DebitCardAmount).HasPrecision(18, 2);
            entity.Property(x => x.CreditCardAmount).HasPrecision(18, 2);
            entity.Property(x => x.CreditSaleAmount).HasPrecision(18, 2);
            entity.Property(x => x.SupplyAmount).HasPrecision(18, 2);
            entity.Property(x => x.WithdrawalAmount).HasPrecision(18, 2);
            entity.Property(x => x.TotalSalesAmount).HasPrecision(18, 2);
            entity.Property(x => x.ReportedAmount).HasPrecision(18, 2);
            entity.Property(x => x.DifferenceAmount).HasPrecision(18, 2);
            entity.Property(x => x.Observation).HasMaxLength(500);
        });

        modelBuilder.Entity<CashMovement>(entity =>
        {
            entity.Property(x => x.Origin).HasMaxLength(80);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.Description).HasMaxLength(220);
            entity.Property(x => x.PaymentMethod).HasMaxLength(40);
            entity.Property(x => x.Observation).HasMaxLength(500);
        });

        modelBuilder.Entity<AccountPayable>(entity =>
        {
            entity.ToTable("contas_pagar");
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.SupplierId).HasColumnName("fornecedor_id");
            entity.Property(x => x.Number).HasMaxLength(60);
            entity.Property(x => x.Number).HasColumnName("numero_documento");
            entity.Property(x => x.SupplierName).HasMaxLength(160);
            entity.Property(x => x.Description).HasMaxLength(300);
            entity.Property(x => x.Description).HasColumnName("descricao");
            entity.Property(x => x.Category).HasColumnName("categoria").HasMaxLength(120);
            entity.Property(x => x.DocumentNumber).HasColumnName("DocumentNumber").HasMaxLength(80);
            entity.Property(x => x.IssueDateUtc).HasColumnName("data_emissao");
            entity.Property(x => x.PaymentMethod).HasMaxLength(60);
            entity.Property(x => x.PaymentMethod).HasColumnName("forma_pagamento");
            entity.Property(x => x.OriginalAmount).HasPrecision(18, 2);
            entity.Property(x => x.OriginalAmount).HasColumnName("valor_original");
            entity.Property(x => x.PaidAmount).HasPrecision(18, 2);
            entity.Property(x => x.PaidAmount).HasColumnName("valor_pago");
            entity.Property(x => x.BalanceAmount).HasPrecision(18, 2);
            entity.Property(x => x.BalanceAmount).HasColumnName("saldo");
            entity.Property(x => x.InterestAmount).HasColumnName("juros").HasPrecision(18, 2);
            entity.Property(x => x.FineAmount).HasColumnName("multa").HasPrecision(18, 2);
            entity.Property(x => x.DiscountAmount).HasColumnName("desconto").HasPrecision(18, 2);
            entity.Property(x => x.DueDateUtc).HasColumnName("data_vencimento");
            entity.Property(x => x.PaidAtUtc).HasColumnName("data_pagamento");
            entity.Property(x => x.UserId).HasColumnName("usuario_id");
            entity.Property(x => x.Status).HasColumnName("status");
            entity.Property(x => x.Observation).HasColumnName("observacao").HasMaxLength(500);
        });

        modelBuilder.Entity<Purchase>(entity =>
        {
            entity.Property(x => x.Number).HasMaxLength(60);
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
            entity.Property(x => x.Notes).HasMaxLength(500);
        });

        modelBuilder.Entity<PurchaseItem>(entity =>
        {
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.UnitCost).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Duplicate>(entity =>
        {
            entity.ToTable("duplicatas");
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Number).HasMaxLength(60);
            entity.Property(x => x.Number).HasColumnName("numero_duplicata");
            entity.Property(x => x.Type).HasColumnName("tipo");
            entity.Property(x => x.CustomerId).HasColumnName("cliente_id");
            entity.Property(x => x.SupplierId).HasColumnName("fornecedor_id");
            entity.Property(x => x.SaleId).HasColumnName("venda_id");
            entity.Property(x => x.AccountPayableId).HasColumnName("conta_pagar_id");
            entity.Property(x => x.AccountReceivableId).HasColumnName("conta_receber_id");
            entity.Property(x => x.Description).HasMaxLength(300);
            entity.Property(x => x.Description).HasColumnName("observacao");
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.Amount).HasColumnName("valor_original");
            entity.Property(x => x.PaidAmount).HasPrecision(18, 2);
            entity.Property(x => x.PaidAmount).HasColumnName("valor_pago");
            entity.Property(x => x.BalanceAmount).HasPrecision(18, 2);
            entity.Property(x => x.BalanceAmount).HasColumnName("saldo");
            entity.Property(x => x.InterestAmount).HasColumnName("juros").HasPrecision(18, 2);
            entity.Property(x => x.FineAmount).HasColumnName("multa").HasPrecision(18, 2);
            entity.Property(x => x.DiscountAmount).HasColumnName("desconto").HasPrecision(18, 2);
            entity.Property(x => x.IssueDateUtc).HasColumnName("data_emissao");
            entity.Property(x => x.DueDateUtc).HasColumnName("data_vencimento");
            entity.Property(x => x.Status).HasColumnName("status");
            entity.Property(x => x.Observation).HasMaxLength(500);
        });

        modelBuilder.Entity<FinancialSettlement>(entity =>
        {
            entity.ToTable("baixas_financeiras");
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.DuplicateId).HasColumnName("duplicata_id");
            entity.Property(x => x.AccountPayableId).HasColumnName("conta_pagar_id");
            entity.Property(x => x.AccountReceivableId).HasColumnName("conta_receber_id");
            entity.Property(x => x.Type).HasColumnName("tipo");
            entity.Property(x => x.Amount).HasColumnName("valor_baixa").HasPrecision(18, 2);
            entity.Property(x => x.InterestAmount).HasColumnName("juros").HasPrecision(18, 2);
            entity.Property(x => x.FineAmount).HasColumnName("multa").HasPrecision(18, 2);
            entity.Property(x => x.DiscountAmount).HasColumnName("desconto").HasPrecision(18, 2);
            entity.Property(x => x.TotalAmount).HasColumnName("valor_total").HasPrecision(18, 2);
            entity.Property(x => x.PaymentMethod).HasColumnName("forma_pagamento").HasMaxLength(60);
            entity.Property(x => x.SettledAtUtc).HasColumnName("data_baixa");
            entity.Property(x => x.UserId).HasColumnName("usuario_id");
            entity.Property(x => x.CashSessionId).HasColumnName("caixa_id");
            entity.Property(x => x.Observation).HasColumnName("observacao").HasMaxLength(500);
        });

        modelBuilder.Entity<FinancialCategory>(entity =>
        {
            entity.ToTable("categorias_financeiras");
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Name).HasColumnName("nome").HasMaxLength(120);
            entity.Property(x => x.Type).HasColumnName("tipo");
            entity.Property(x => x.IsActive).HasColumnName("ativo");
        });

        modelBuilder.Entity<FinancialLog>(entity =>
        {
            entity.ToTable("logs_financeiros");
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.UserId).HasColumnName("usuario_id");
            entity.Property(x => x.LoggedAtUtc).HasColumnName("data_hora");
            entity.Property(x => x.Action).HasColumnName("acao").HasMaxLength(120);
            entity.Property(x => x.Document).HasColumnName("documento").HasMaxLength(120);
            entity.Property(x => x.Amount).HasColumnName("valor").HasPrecision(18, 2);
            entity.Property(x => x.Reason).HasColumnName("motivo").HasMaxLength(500);
        });

        modelBuilder.Entity<FinancialMovement>(entity =>
        {
            entity.Property(x => x.Number).HasMaxLength(60);
            entity.Property(x => x.Description).HasMaxLength(300);
            entity.Property(x => x.Reference).HasMaxLength(120);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<SaleCancellation>(entity =>
        {
            entity.ToTable("vendas_canceladas");
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.SaleId).HasColumnName("venda_id");
            entity.Property(x => x.Reason).HasColumnName("motivo").HasMaxLength(300);
            entity.Property(x => x.UserId).HasColumnName("usuario_id");
            entity.Property(x => x.CancelledAtUtc).HasColumnName("data_cancelamento");
            entity.Property(x => x.TotalAmount).HasColumnName("valor_total").HasPrecision(18, 2);
            entity.Property(x => x.Observation).HasColumnName("observacao").HasMaxLength(500);
        });

        modelBuilder.Entity<SaleReturn>(entity =>
        {
            entity.Property(x => x.Reason).HasMaxLength(300);
            entity.Property(x => x.ProcessedBy).HasMaxLength(120);
            entity.Property(x => x.TotalReturnedAmount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<NonFiscalNote>(entity =>
        {
            entity.Property(x => x.Number).HasMaxLength(60);
            entity.Property(x => x.StoreName).HasMaxLength(180);
            entity.Property(x => x.StoreDocument).HasMaxLength(30);
            entity.Property(x => x.CustomerName).HasMaxLength(180);
            entity.Property(x => x.CustomerDocument).HasMaxLength(30);
            entity.Property(x => x.CustomerAddress).HasMaxLength(220);
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
            entity.Property(x => x.Notes).HasMaxLength(500);
        });

        modelBuilder.Entity<NonFiscalNoteItem>(entity =>
        {
            entity.Property(x => x.Description).HasMaxLength(220);
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.TotalPrice).HasPrecision(18, 2);
        });

        modelBuilder.Entity<SecurityAuditEntry>(entity =>
        {
            entity.Property(x => x.Area).HasMaxLength(80);
            entity.Property(x => x.Action).HasMaxLength(120);
            entity.Property(x => x.EntityName).HasMaxLength(120);
            entity.Property(x => x.EntityId).HasMaxLength(120);
            entity.Property(x => x.Details).HasMaxLength(500);
            entity.Property(x => x.MachineName).HasMaxLength(120);
            entity.Property(x => x.IpAddress).HasMaxLength(60);
        });

        modelBuilder.Entity<SecurityLoginAttempt>(entity =>
        {
            entity.Property(x => x.Username).HasMaxLength(80);
            entity.Property(x => x.FailureReason).HasMaxLength(200);
            entity.Property(x => x.MachineName).HasMaxLength(120);
            entity.Property(x => x.IpAddress).HasMaxLength(60);
        });

        modelBuilder.Entity<SecuritySession>(entity =>
        {
            entity.Property(x => x.SessionKey).HasMaxLength(120);
            entity.Property(x => x.MachineName).HasMaxLength(120);
            entity.Property(x => x.IpAddress).HasMaxLength(60);
        });

        modelBuilder.Entity<AppUser>().HasIndex(x => x.Username).IsUnique();
        modelBuilder.Entity<AppUser>().HasIndex(x => x.Email).IsUnique();
        modelBuilder.Entity<Product>().HasIndex(x => x.Sku).IsUnique();
        modelBuilder.Entity<Product>().HasIndex(x => x.Barcode);
        modelBuilder.Entity<Product>().HasIndex(x => x.Category);
        modelBuilder.Entity<Customer>().HasIndex(x => x.Code);
        modelBuilder.Entity<Customer>().HasIndex(x => x.FullName);
        modelBuilder.Entity<Customer>().HasIndex(x => x.DocumentNumber);
        modelBuilder.Entity<Customer>().HasIndex(x => x.Phone);
        modelBuilder.Entity<Customer>().HasIndex(x => x.WhatsApp);
        modelBuilder.Entity<Supplier>().HasIndex(x => x.Cnpj);
        modelBuilder.Entity<Supplier>().HasIndex(x => x.Code);
        modelBuilder.Entity<Supplier>().HasIndex(x => x.FantasyName);
        modelBuilder.Entity<Supplier>().HasIndex(x => x.LegalName);
        modelBuilder.Entity<Supplier>().HasIndex(x => x.Phone);
        modelBuilder.Entity<Supplier>().HasIndex(x => x.WhatsApp);
        modelBuilder.Entity<Product>().HasIndex(x => x.SupplierId);
        modelBuilder.Entity<StockMovement>().HasIndex(x => x.ProductId);
        modelBuilder.Entity<StockMovement>().HasIndex(x => x.MovementAtUtc);
        modelBuilder.Entity<StockInventoryItem>().HasIndex(x => x.InventoryId);
        modelBuilder.Entity<StockInventoryItem>().HasIndex(x => x.ProductId);
        modelBuilder.Entity<StockTransfer>().HasIndex(x => x.ProductId);
        modelBuilder.Entity<StockReservation>().HasIndex(x => x.ProductId);
        modelBuilder.Entity<AccountPayable>().HasIndex(x => x.SupplierId);
        modelBuilder.Entity<SalePayment>().HasIndex(x => x.SaleId);
        modelBuilder.Entity<SaleLog>().HasIndex(x => x.SaleId);
        modelBuilder.Entity<Purchase>().HasIndex(x => x.SupplierId);
        modelBuilder.Entity<PurchaseItem>().HasIndex(x => x.ProductId);
        modelBuilder.Entity<Budget>().HasIndex(x => x.Number).IsUnique();
        modelBuilder.Entity<CashSession>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<AccountPayable>().HasIndex(x => x.Number).IsUnique();
        modelBuilder.Entity<Duplicate>().HasIndex(x => x.Number).IsUnique();
        modelBuilder.Entity<FinancialMovement>().HasIndex(x => x.Number);
        modelBuilder.Entity<AccountReceivable>().HasIndex(x => x.Number).IsUnique();
        modelBuilder.Entity<NonFiscalNote>().HasIndex(x => x.Number).IsUnique();
        modelBuilder.Entity<SecurityAuditEntry>().HasIndex(x => x.CreatedAtUtc);
        modelBuilder.Entity<SecurityLoginAttempt>().HasIndex(x => x.AttemptedAtUtc);
        modelBuilder.Entity<SecuritySession>().HasIndex(x => x.SessionKey).IsUnique();
    }
}

public sealed class Sha256PasswordHasher : IPasswordHasher
{
    public string CreateSalt()
    {
        var bytes = RandomNumberGenerator.GetBytes(16);
        return Convert.ToBase64String(bytes);
    }

    public string Hash(string password, string salt)
    {
        using var deriveBytes = new Rfc2898DeriveBytes(password, Convert.FromBase64String(salt), 100_000, HashAlgorithmName.SHA256);
        return Convert.ToBase64String(deriveBytes.GetBytes(32));
    }

    public bool Verify(string password, string salt, string hash)
    {
        return Hash(password, salt) == hash;
    }
}

public sealed class EfUserRepository : IUserRepository
{
    private readonly MaterialProDbContext _db;

    public EfUserRepository(MaterialProDbContext db)
    {
        _db = db;
    }

    public void Add(AppUser user)
    {
        _db.Users.Add(user);
        _db.SaveChanges();
    }

    public void Update(AppUser user)
    {
        _db.Users.Update(user);
        _db.SaveChanges();
    }

    public AppUser? FindByEmail(string email)
    {
        return _db.Users.FirstOrDefault(x => x.Email.ToLower() == email.ToLower());
    }

    public AppUser? FindByUsername(string username)
    {
        return _db.Users.FirstOrDefault(x => x.Username.ToLower() == username.ToLower());
    }

    public IReadOnlyList<AppUser> GetAll()
    {
        return _db.Users.AsNoTracking().OrderBy(x => x.FullName).ToList();
    }
}

public sealed class MaterialProDatabaseInitializer
{
    private readonly MaterialProDbContext _db;
    private readonly IAuthService _authService;

    public MaterialProDatabaseInitializer(MaterialProDbContext db, IAuthService authService)
    {
        _db = db;
        _authService = authService;
    }

    public void EnsureCreated()
    {
        _db.Database.Migrate();
        EnsureCompatibilitySchema();

        if (!_db.Users.Any())
        {
            _authService.CreateAdmin("Administrador", "admin", "admin@materialpro.local", "Admin@123");
            _db.SaveChanges();
        }
    }

    private void EnsureCompatibilitySchema()
    {
        AddColumnIfMissing("Products", "Description", "`Description` varchar(500) NOT NULL DEFAULT ''");
        AddColumnIfMissing("Products", "Category", "`Category` varchar(120) NOT NULL DEFAULT ''");
        AddColumnIfMissing("Products", "Brand", "`Brand` varchar(120) NOT NULL DEFAULT ''");
        AddColumnIfMissing("Products", "Barcode", "`Barcode` varchar(80) NOT NULL DEFAULT ''");
        AddColumnIfMissing("Products", "Ncm", "`Ncm` varchar(20) NOT NULL DEFAULT ''");
        AddColumnIfMissing("Products", "Location", "`Location` varchar(120) NOT NULL DEFAULT ''");
        AddColumnIfMissing("Products", "SupplierId", "`SupplierId` char(36) NULL");
        AddColumnIfMissing("Products", "MaximumStock", "`MaximumStock` decimal(18,3) NOT NULL DEFAULT 0");
        AddColumnIfMissing("Products", "ReservedStock", "`ReservedStock` decimal(18,3) NOT NULL DEFAULT 0");

        AddColumnIfMissing("Customers", "Code", "`Code` varchar(40) NOT NULL DEFAULT ''");
        AddColumnIfMissing("Customers", "StateRegistration", "`StateRegistration` varchar(30) NOT NULL DEFAULT ''");
        AddColumnIfMissing("Customers", "WhatsApp", "`WhatsApp` varchar(30) NOT NULL DEFAULT ''");
        AddColumnIfMissing("Customers", "ZipCode", "`ZipCode` varchar(20) NOT NULL DEFAULT ''");
        AddColumnIfMissing("Customers", "Address", "`Address` varchar(220) NOT NULL DEFAULT ''");
        AddColumnIfMissing("Customers", "AddressNumber", "`AddressNumber` varchar(20) NOT NULL DEFAULT ''");
        AddColumnIfMissing("Customers", "Complement", "`Complement` varchar(120) NOT NULL DEFAULT ''");
        AddColumnIfMissing("Customers", "District", "`District` varchar(120) NOT NULL DEFAULT ''");
        AddColumnIfMissing("Customers", "City", "`City` varchar(120) NOT NULL DEFAULT ''");
        AddColumnIfMissing("Customers", "State", "`State` varchar(2) NOT NULL DEFAULT ''");
        AddColumnIfMissing("Customers", "CreditLimit", "`CreditLimit` decimal(18,2) NOT NULL DEFAULT 0");
        AddColumnIfMissing("Customers", "Notes", "`Notes` varchar(500) NOT NULL DEFAULT ''");
        AddColumnIfMissing("Customers", "IsBlocked", "`IsBlocked` tinyint(1) NOT NULL DEFAULT 0");

        AddColumnIfMissing("Suppliers", "Code", "`Code` varchar(40) NOT NULL DEFAULT ''");
        AddColumnIfMissing("Suppliers", "PersonType", "`PersonType` int NOT NULL DEFAULT 2");
        AddColumnIfMissing("Suppliers", "FantasyName", "`FantasyName` varchar(150) NOT NULL DEFAULT ''");
        AddColumnIfMissing("Suppliers", "LegalName", "`LegalName` varchar(180) NOT NULL DEFAULT ''");
        AddColumnIfMissing("Suppliers", "StateRegistration", "`StateRegistration` varchar(30) NOT NULL DEFAULT ''");
        AddColumnIfMissing("Suppliers", "MunicipalRegistration", "`MunicipalRegistration` varchar(30) NOT NULL DEFAULT ''");
        AddColumnIfMissing("Suppliers", "MobilePhone", "`MobilePhone` varchar(30) NOT NULL DEFAULT ''");
        AddColumnIfMissing("Suppliers", "WhatsApp", "`WhatsApp` varchar(30) NOT NULL DEFAULT ''");
        AddColumnIfMissing("Suppliers", "Website", "`Website` varchar(180) NOT NULL DEFAULT ''");
        AddColumnIfMissing("Suppliers", "ZipCode", "`ZipCode` varchar(20) NOT NULL DEFAULT ''");
        AddColumnIfMissing("Suppliers", "AddressNumber", "`AddressNumber` varchar(20) NOT NULL DEFAULT ''");
        AddColumnIfMissing("Suppliers", "Complement", "`Complement` varchar(120) NOT NULL DEFAULT ''");
        AddColumnIfMissing("Suppliers", "District", "`District` varchar(120) NOT NULL DEFAULT ''");
        AddColumnIfMissing("Suppliers", "City", "`City` varchar(120) NOT NULL DEFAULT ''");
        AddColumnIfMissing("Suppliers", "State", "`State` varchar(2) NOT NULL DEFAULT ''");
        AddColumnIfMissing("Suppliers", "ContactName", "`ContactName` varchar(150) NOT NULL DEFAULT ''");
        AddColumnIfMissing("Suppliers", "ContactRole", "`ContactRole` varchar(120) NOT NULL DEFAULT ''");
        AddColumnIfMissing("Suppliers", "DefaultPaymentTermDays", "`DefaultPaymentTermDays` int NOT NULL DEFAULT 0");
        AddColumnIfMissing("Suppliers", "PurchaseLimit", "`PurchaseLimit` decimal(18,2) NOT NULL DEFAULT 0");
        AddColumnIfMissing("Suppliers", "Notes", "`Notes` varchar(500) NOT NULL DEFAULT ''");
        AddColumnIfMissing("AccountsPayable", "SupplierId", "`SupplierId` char(36) NULL");
        AddColumnIfMissing("CashSessions", "CashAmount", "`CashAmount` decimal(18,2) NOT NULL DEFAULT 0");
        AddColumnIfMissing("CashSessions", "PixAmount", "`PixAmount` decimal(18,2) NOT NULL DEFAULT 0");
        AddColumnIfMissing("CashSessions", "DebitCardAmount", "`DebitCardAmount` decimal(18,2) NOT NULL DEFAULT 0");
        AddColumnIfMissing("CashSessions", "CreditCardAmount", "`CreditCardAmount` decimal(18,2) NOT NULL DEFAULT 0");
        AddColumnIfMissing("CashSessions", "CreditSaleAmount", "`CreditSaleAmount` decimal(18,2) NOT NULL DEFAULT 0");
        AddColumnIfMissing("CashSessions", "SupplyAmount", "`SupplyAmount` decimal(18,2) NOT NULL DEFAULT 0");
        AddColumnIfMissing("CashSessions", "WithdrawalAmount", "`WithdrawalAmount` decimal(18,2) NOT NULL DEFAULT 0");
        AddColumnIfMissing("CashSessions", "TotalSalesAmount", "`TotalSalesAmount` decimal(18,2) NOT NULL DEFAULT 0");
        AddColumnIfMissing("CashSessions", "ReportedAmount", "`ReportedAmount` decimal(18,2) NOT NULL DEFAULT 0");
        AddColumnIfMissing("CashSessions", "DifferenceAmount", "`DifferenceAmount` decimal(18,2) NOT NULL DEFAULT 0");
        AddColumnIfMissing("CashSessions", "Status", "`Status` int NOT NULL DEFAULT 1");
        AddColumnIfMissing("CashSessions", "Observation", "`Observation` varchar(500) NOT NULL DEFAULT ''");
        AddColumnIfMissing("CashMovements", "Origin", "`Origin` varchar(80) NOT NULL DEFAULT ''");
        AddColumnIfMissing("CashMovements", "DuplicateId", "`DuplicateId` char(36) NULL");
        AddColumnIfMissing("CashMovements", "UserId", "`UserId` char(36) NULL");
        AddColumnIfMissing("CashMovements", "PaymentMethod", "`PaymentMethod` varchar(40) NOT NULL DEFAULT ''");
        AddColumnIfMissing("CashMovements", "MovementAtUtc", "`MovementAtUtc` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)");
        AddColumnIfMissing("CashMovements", "Observation", "`Observation` varchar(500) NOT NULL DEFAULT ''");
        AddColumnIfMissing("Sales", "UserId", "`UserId` char(36) NULL");
        AddColumnIfMissing("Sales", "CashSessionId", "`CashSessionId` char(36) NULL");
        AddColumnIfMissing("Sales", "SubtotalAmount", "`SubtotalAmount` decimal(18,2) NOT NULL DEFAULT 0");
        AddColumnIfMissing("Sales", "SurchargeAmount", "`SurchargeAmount` decimal(18,2) NOT NULL DEFAULT 0");
        AddColumnIfMissing("Sales", "Observation", "`Observation` varchar(500) NOT NULL DEFAULT ''");
        AddColumnIfMissing("SaleItems", "ProductCode", "`ProductCode` varchar(80) NOT NULL DEFAULT ''");
        AddColumnIfMissing("SaleItems", "ProductDescription", "`ProductDescription` varchar(220) NOT NULL DEFAULT ''");
        AddColumnIfMissing("SaleItems", "SurchargeAmount", "`SurchargeAmount` decimal(18,2) NOT NULL DEFAULT 0");
        AddColumnIfMissing("SaleItems", "TotalItem", "`TotalItem` decimal(18,2) NOT NULL DEFAULT 0");
        AddColumnIfMissing("SaleItems", "StockDeducted", "`StockDeducted` tinyint(1) NOT NULL DEFAULT 0");
        CreatePurchaseTablesIfMissing();
        CreateStockTablesIfMissing();
        CreatePdvTablesIfMissing();
        CreateFinancialTablesIfMissing();

        AddIndexIfMissing("Products", "IX_Products_Barcode", "`Barcode`");
        AddIndexIfMissing("Products", "IX_Products_Category", "`Category`");
        AddIndexIfMissing("Customers", "IX_Customers_Code", "`Code`");
        AddIndexIfMissing("Customers", "IX_Customers_FullName", "`FullName`");
        AddIndexIfMissing("Customers", "IX_Customers_Phone", "`Phone`");
        AddIndexIfMissing("Customers", "IX_Customers_WhatsApp", "`WhatsApp`");
        AddIndexIfMissing("Suppliers", "IX_Suppliers_Code", "`Code`");
        AddIndexIfMissing("Suppliers", "IX_Suppliers_FantasyName", "`FantasyName`");
        AddIndexIfMissing("Suppliers", "IX_Suppliers_LegalName", "`LegalName`");
        AddIndexIfMissing("Suppliers", "IX_Suppliers_Cnpj", "`Cnpj`");
        AddIndexIfMissing("Suppliers", "IX_Suppliers_Phone", "`Phone`");
        AddIndexIfMissing("Suppliers", "IX_Suppliers_WhatsApp", "`WhatsApp`");
        AddIndexIfMissing("Products", "IX_Products_SupplierId", "`SupplierId`");
        AddIndexIfMissing("AccountsPayable", "IX_AccountsPayable_SupplierId", "`SupplierId`");
        AddIndexIfMissing("contas_pagar", "IX_contas_pagar_fornecedor_id", "`fornecedor_id`");
        AddIndexIfMissing("contas_pagar", "IX_contas_pagar_vencimento", "`data_vencimento`");
        AddIndexIfMissing("contas_receber", "IX_contas_receber_cliente_id", "`cliente_id`");
        AddIndexIfMissing("contas_receber", "IX_contas_receber_vencimento", "`data_vencimento`");
        AddIndexIfMissing("duplicatas", "IX_duplicatas_vencimento", "`data_vencimento`");
    }

    private void CreateFinancialTablesIfMissing()
    {
        _db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS `contas_pagar` (
              `id` char(36) NOT NULL,
              `CreatedAtUtc` datetime(6) NOT NULL,
              `UpdatedAtUtc` datetime(6) NULL,
              `IsActive` tinyint(1) NOT NULL DEFAULT 1,
              `fornecedor_id` char(36) NULL,
              `numero_documento` varchar(60) NOT NULL DEFAULT '',
              `SupplierName` varchar(160) NOT NULL DEFAULT '',
              `descricao` varchar(300) NOT NULL DEFAULT '',
              `categoria` varchar(120) NOT NULL DEFAULT '',
              `DocumentNumber` varchar(80) NOT NULL DEFAULT '',
              `data_emissao` datetime(6) NOT NULL,
              `valor_original` decimal(18,2) NOT NULL DEFAULT 0,
              `valor_pago` decimal(18,2) NOT NULL DEFAULT 0,
              `saldo` decimal(18,2) NOT NULL DEFAULT 0,
              `juros` decimal(18,2) NOT NULL DEFAULT 0,
              `multa` decimal(18,2) NOT NULL DEFAULT 0,
              `desconto` decimal(18,2) NOT NULL DEFAULT 0,
              `data_vencimento` datetime(6) NOT NULL,
              `data_pagamento` datetime(6) NULL,
              `usuario_id` char(36) NULL,
              `status` int NOT NULL DEFAULT 1,
              `forma_pagamento` varchar(60) NOT NULL DEFAULT '',
              `observacao` varchar(500) NOT NULL DEFAULT '',
              PRIMARY KEY (`id`),
              UNIQUE INDEX `IX_AccountsPayable_Number` (`numero_documento`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """);
        _db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS `contas_receber` (
              `id` char(36) NOT NULL,
              `CreatedAtUtc` datetime(6) NOT NULL,
              `UpdatedAtUtc` datetime(6) NULL,
              `IsActive` tinyint(1) NOT NULL DEFAULT 1,
              `cliente_id` char(36) NULL,
              `venda_id` char(36) NULL,
              `numero_documento` varchar(60) NOT NULL DEFAULT '',
              `CustomerName` varchar(160) NOT NULL DEFAULT '',
              `descricao` varchar(300) NOT NULL DEFAULT '',
              `DocumentNumber` varchar(80) NOT NULL DEFAULT '',
              `data_emissao` datetime(6) NOT NULL,
              `valor_original` decimal(18,2) NOT NULL DEFAULT 0,
              `valor_recebido` decimal(18,2) NOT NULL DEFAULT 0,
              `saldo` decimal(18,2) NOT NULL DEFAULT 0,
              `juros` decimal(18,2) NOT NULL DEFAULT 0,
              `multa` decimal(18,2) NOT NULL DEFAULT 0,
              `desconto` decimal(18,2) NOT NULL DEFAULT 0,
              `data_vencimento` datetime(6) NOT NULL,
              `data_recebimento` datetime(6) NULL,
              `usuario_id` char(36) NULL,
              `status` int NOT NULL DEFAULT 1,
              `forma_recebimento` varchar(60) NOT NULL DEFAULT '',
              `observacao` varchar(500) NOT NULL DEFAULT '',
              PRIMARY KEY (`id`),
              UNIQUE INDEX `IX_AccountsReceivable_Number` (`numero_documento`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """);
        _db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS `duplicatas` (
              `id` char(36) NOT NULL,
              `CreatedAtUtc` datetime(6) NOT NULL,
              `UpdatedAtUtc` datetime(6) NULL,
              `IsActive` tinyint(1) NOT NULL DEFAULT 1,
              `numero_duplicata` varchar(60) NOT NULL DEFAULT '',
              `tipo` int NOT NULL,
              `cliente_id` char(36) NULL,
              `fornecedor_id` char(36) NULL,
              `venda_id` char(36) NULL,
              `BudgetId` char(36) NULL,
              `conta_pagar_id` char(36) NULL,
              `conta_receber_id` char(36) NULL,
              `observacao` varchar(500) NOT NULL DEFAULT '',
              `valor_original` decimal(18,2) NOT NULL DEFAULT 0,
              `valor_pago` decimal(18,2) NOT NULL DEFAULT 0,
              `saldo` decimal(18,2) NOT NULL DEFAULT 0,
              `juros` decimal(18,2) NOT NULL DEFAULT 0,
              `multa` decimal(18,2) NOT NULL DEFAULT 0,
              `desconto` decimal(18,2) NOT NULL DEFAULT 0,
              `data_emissao` datetime(6) NOT NULL,
              `data_vencimento` datetime(6) NOT NULL,
              `PaidAtUtc` datetime(6) NULL,
              `status` int NOT NULL DEFAULT 1,
              `Observation` varchar(500) NOT NULL DEFAULT '',
              PRIMARY KEY (`id`),
              UNIQUE INDEX `IX_Duplicates_Number` (`numero_duplicata`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """);
        _db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS `baixas_financeiras` (
              `id` char(36) NOT NULL,
              `CreatedAtUtc` datetime(6) NOT NULL,
              `UpdatedAtUtc` datetime(6) NULL,
              `IsActive` tinyint(1) NOT NULL DEFAULT 1,
              `duplicata_id` char(36) NULL,
              `conta_pagar_id` char(36) NULL,
              `conta_receber_id` char(36) NULL,
              `tipo` int NOT NULL,
              `valor_baixa` decimal(18,2) NOT NULL DEFAULT 0,
              `juros` decimal(18,2) NOT NULL DEFAULT 0,
              `multa` decimal(18,2) NOT NULL DEFAULT 0,
              `desconto` decimal(18,2) NOT NULL DEFAULT 0,
              `valor_total` decimal(18,2) NOT NULL DEFAULT 0,
              `forma_pagamento` varchar(60) NOT NULL DEFAULT '',
              `data_baixa` datetime(6) NOT NULL,
              `usuario_id` char(36) NULL,
              `caixa_id` char(36) NULL,
              `observacao` varchar(500) NOT NULL DEFAULT '',
              PRIMARY KEY (`id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """);
        _db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS `categorias_financeiras` (
              `id` char(36) NOT NULL,
              `CreatedAtUtc` datetime(6) NOT NULL,
              `UpdatedAtUtc` datetime(6) NULL,
              `ativo` tinyint(1) NOT NULL DEFAULT 1,
              `nome` varchar(120) NOT NULL DEFAULT '',
              `tipo` int NOT NULL,
              PRIMARY KEY (`id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """);
        _db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS `logs_financeiros` (
              `id` char(36) NOT NULL,
              `CreatedAtUtc` datetime(6) NOT NULL,
              `UpdatedAtUtc` datetime(6) NULL,
              `IsActive` tinyint(1) NOT NULL DEFAULT 1,
              `usuario_id` char(36) NULL,
              `data_hora` datetime(6) NOT NULL,
              `acao` varchar(120) NOT NULL DEFAULT '',
              `documento` varchar(120) NOT NULL DEFAULT '',
              `valor` decimal(18,2) NOT NULL DEFAULT 0,
              `motivo` varchar(500) NOT NULL DEFAULT '',
              PRIMARY KEY (`id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """);
        SeedFinancialCategories();
    }

    private void SeedFinancialCategories()
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

    private void CreatePdvTablesIfMissing()
    {
        _db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS `venda_pagamentos` (
              `id` char(36) NOT NULL,
              `CreatedAtUtc` datetime(6) NOT NULL,
              `UpdatedAtUtc` datetime(6) NULL,
              `IsActive` tinyint(1) NOT NULL DEFAULT 1,
              `venda_id` char(36) NOT NULL,
              `forma_pagamento` varchar(40) NOT NULL DEFAULT '',
              `valor` decimal(18,2) NOT NULL DEFAULT 0,
              `parcelas` int NOT NULL DEFAULT 1,
              `vencimento_primeira_parcela` datetime(6) NULL,
              `observacao` varchar(300) NOT NULL DEFAULT '',
              PRIMARY KEY (`id`),
              INDEX `IX_venda_pagamentos_venda_id` (`venda_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """);
        _db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS `vendas_logs` (
              `id` char(36) NOT NULL,
              `CreatedAtUtc` datetime(6) NOT NULL,
              `UpdatedAtUtc` datetime(6) NULL,
              `IsActive` tinyint(1) NOT NULL DEFAULT 1,
              `venda_id` char(36) NOT NULL,
              `usuario_id` char(36) NULL,
              `acao` varchar(80) NOT NULL DEFAULT '',
              `descricao` varchar(500) NOT NULL DEFAULT '',
              `data_log` datetime(6) NOT NULL,
              `ip_maquina` varchar(80) NOT NULL DEFAULT '',
              PRIMARY KEY (`id`),
              INDEX `IX_vendas_logs_venda_id` (`venda_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """);
    }

    private void CreateStockTablesIfMissing()
    {
        _db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS `estoque_movimentos` (
              `id` char(36) NOT NULL,
              `CreatedAtUtc` datetime(6) NOT NULL,
              `UpdatedAtUtc` datetime(6) NULL,
              `IsActive` tinyint(1) NOT NULL DEFAULT 1,
              `produto_id` char(36) NOT NULL,
              `tipo_movimento` int NOT NULL,
              `quantidade` decimal(18,3) NOT NULL DEFAULT 0,
              `estoque_anterior` decimal(18,3) NOT NULL DEFAULT 0,
              `estoque_atual` decimal(18,3) NOT NULL DEFAULT 0,
              `usuario_id` char(36) NULL,
              `Reason` varchar(180) NOT NULL DEFAULT '',
              `Reference` varchar(120) NOT NULL DEFAULT '',
              `Warehouse` varchar(120) NOT NULL DEFAULT 'Loja',
              `data_movimento` datetime(6) NOT NULL,
              `observacao` varchar(500) NOT NULL DEFAULT '',
              PRIMARY KEY (`id`),
              INDEX `IX_estoque_movimentos_produto_id` (`produto_id`),
              INDEX `IX_estoque_movimentos_data_movimento` (`data_movimento`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """);
        _db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS `estoque_inventario` (
              `id` char(36) NOT NULL,
              `CreatedAtUtc` datetime(6) NOT NULL,
              `UpdatedAtUtc` datetime(6) NULL,
              `IsActive` tinyint(1) NOT NULL DEFAULT 1,
              `data_inventario` datetime(6) NOT NULL,
              `usuario_id` char(36) NULL,
              `status` int NOT NULL,
              `observacao` varchar(500) NOT NULL DEFAULT '',
              PRIMARY KEY (`id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """);
        _db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS `estoque_inventario_itens` (
              `id` char(36) NOT NULL,
              `CreatedAtUtc` datetime(6) NOT NULL,
              `UpdatedAtUtc` datetime(6) NULL,
              `IsActive` tinyint(1) NOT NULL DEFAULT 1,
              `inventario_id` char(36) NOT NULL,
              `produto_id` char(36) NOT NULL,
              `estoque_sistema` decimal(18,3) NOT NULL DEFAULT 0,
              `estoque_contado` decimal(18,3) NOT NULL DEFAULT 0,
              `diferenca` decimal(18,3) NOT NULL DEFAULT 0,
              PRIMARY KEY (`id`),
              INDEX `IX_estoque_inventario_itens_inventario_id` (`inventario_id`),
              INDEX `IX_estoque_inventario_itens_produto_id` (`produto_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """);
        _db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS `estoque_transferencias` (
              `id` char(36) NOT NULL,
              `CreatedAtUtc` datetime(6) NOT NULL,
              `UpdatedAtUtc` datetime(6) NULL,
              `IsActive` tinyint(1) NOT NULL DEFAULT 1,
              `produto_id` char(36) NOT NULL,
              `quantidade` decimal(18,3) NOT NULL DEFAULT 0,
              `deposito_origem` varchar(120) NOT NULL DEFAULT '',
              `deposito_destino` varchar(120) NOT NULL DEFAULT '',
              `usuario_id` char(36) NULL,
              `data_transferencia` datetime(6) NOT NULL,
              `status` int NOT NULL,
              `observacao` varchar(500) NOT NULL DEFAULT '',
              PRIMARY KEY (`id`),
              INDEX `IX_estoque_transferencias_produto_id` (`produto_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """);
        _db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS `estoque_reservas` (
              `Id` char(36) NOT NULL,
              `CreatedAtUtc` datetime(6) NOT NULL,
              `UpdatedAtUtc` datetime(6) NULL,
              `IsActive` tinyint(1) NOT NULL DEFAULT 1,
              `produto_id` char(36) NOT NULL,
              `quantidade` decimal(18,3) NOT NULL DEFAULT 0,
              `Source` varchar(80) NOT NULL DEFAULT '',
              `Reference` varchar(120) NOT NULL DEFAULT '',
              `UserId` char(36) NULL,
              `ReservedAtUtc` datetime(6) NOT NULL,
              `ReleasedAtUtc` datetime(6) NULL,
              `Status` int NOT NULL,
              `Observation` varchar(500) NOT NULL DEFAULT '',
              PRIMARY KEY (`Id`),
              INDEX `IX_estoque_reservas_produto_id` (`produto_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """);
    }

    private void CreatePurchaseTablesIfMissing()
    {
        _db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS `Purchases` (
              `Id` char(36) NOT NULL,
              `CreatedAtUtc` datetime(6) NOT NULL,
              `UpdatedAtUtc` datetime(6) NULL,
              `IsActive` tinyint(1) NOT NULL DEFAULT 1,
              `SupplierId` char(36) NOT NULL,
              `Number` varchar(60) NOT NULL DEFAULT '',
              `PurchasedAtUtc` datetime(6) NOT NULL,
              `TotalAmount` decimal(18,2) NOT NULL DEFAULT 0,
              `Notes` varchar(500) NOT NULL DEFAULT '',
              PRIMARY KEY (`Id`),
              INDEX `IX_Purchases_SupplierId` (`SupplierId`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """);
        _db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS `PurchaseItems` (
              `Id` char(36) NOT NULL,
              `CreatedAtUtc` datetime(6) NOT NULL,
              `UpdatedAtUtc` datetime(6) NULL,
              `IsActive` tinyint(1) NOT NULL DEFAULT 1,
              `PurchaseId` char(36) NOT NULL,
              `ProductId` char(36) NOT NULL,
              `Quantity` decimal(18,3) NOT NULL DEFAULT 0,
              `UnitCost` decimal(18,2) NOT NULL DEFAULT 0,
              PRIMARY KEY (`Id`),
              INDEX `IX_PurchaseItems_ProductId` (`ProductId`),
              INDEX `IX_PurchaseItems_PurchaseId` (`PurchaseId`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """);
    }

    private void AddColumnIfMissing(string tableName, string columnName, string definition)
    {
        var exists = _db.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS `Value` FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = {tableName} AND COLUMN_NAME = {columnName}")
            .AsEnumerable()
            .First();

        if (exists == 0)
        {
            var sql = $"ALTER TABLE `{tableName}` ADD COLUMN {definition}";
            _db.Database.ExecuteSqlRaw(sql);
        }
    }

    private void AddIndexIfMissing(string tableName, string indexName, string columns)
    {
        var exists = _db.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS `Value` FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = {tableName} AND INDEX_NAME = {indexName}")
            .AsEnumerable()
            .First();

        if (exists == 0)
        {
            var sql = $"CREATE INDEX `{indexName}` ON `{tableName}` ({columns})";
            _db.Database.ExecuteSqlRaw(sql);
        }
    }
}

public static class MaterialProDbContextFactory
{
    public static MaterialProDbContext CreateMySql(string connectionString)
    {
        var serverVersion = new MySqlServerVersion(new Version(8, 0, 36));
        var options = new DbContextOptionsBuilder<MaterialProDbContext>()
            .UseMySql(connectionString, serverVersion, mysql =>
            {
                mysql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), null);
            })
            .Options;
        return new MaterialProDbContext(options);
    }
}
