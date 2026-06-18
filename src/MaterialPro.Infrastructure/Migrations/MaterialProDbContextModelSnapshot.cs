using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace MaterialPro.Infrastructure.Migrations;

[DbContext(typeof(MaterialProDbContext))]
public partial class MaterialProDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "8.0.8");

        modelBuilder.Entity("MaterialPro.Domain.AppUser", b => { });
        modelBuilder.Entity("MaterialPro.Domain.StoreProfile", b => { });
        modelBuilder.Entity("MaterialPro.Domain.Budget", b => { });
        modelBuilder.Entity("MaterialPro.Domain.BudgetItem", b => { });
        modelBuilder.Entity("MaterialPro.Domain.AccountPayable", b => { });
        modelBuilder.Entity("MaterialPro.Domain.CashMovement", b => { });
        modelBuilder.Entity("MaterialPro.Domain.CashSession", b => { });
        modelBuilder.Entity("MaterialPro.Domain.Customer", b => { });
        modelBuilder.Entity("MaterialPro.Domain.Duplicate", b => { });
        modelBuilder.Entity("MaterialPro.Domain.FinancialMovement", b => { });
        modelBuilder.Entity("MaterialPro.Domain.NonFiscalNote", b => { });
        modelBuilder.Entity("MaterialPro.Domain.NonFiscalNoteItem", b => { });
        modelBuilder.Entity("MaterialPro.Domain.SecurityAuditEntry", b => { });
        modelBuilder.Entity("MaterialPro.Domain.SecurityLoginAttempt", b => { });
        modelBuilder.Entity("MaterialPro.Domain.SecuritySession", b => { });
        modelBuilder.Entity("MaterialPro.Domain.Product", b => { });
        modelBuilder.Entity("MaterialPro.Domain.Sale", b => { });
        modelBuilder.Entity("MaterialPro.Domain.SaleCancellation", b => { });
        modelBuilder.Entity("MaterialPro.Domain.SaleItem", b => { });
        modelBuilder.Entity("MaterialPro.Domain.SaleReturn", b => { });
        modelBuilder.Entity("MaterialPro.Domain.StockMovement", b => { });
        modelBuilder.Entity("MaterialPro.Domain.Supplier", b => { });
    }
}
