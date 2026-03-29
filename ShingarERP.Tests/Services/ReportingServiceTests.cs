using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using ShingarERP.Core.Models;
using ShingarERP.Data;
using ShingarERP.Data.Repositories;
using ShingarERP.Services;

namespace ShingarERP.Tests.Services
{
    [TestFixture]
    public class ReportingServiceTests
    {
        private ShingarContext _context = null!;
        private UnitOfWork _uow = null!;
        private SalesOrderRepository _salesRepo = null!;
        private PurchaseOrderRepository _poRepo = null!;
        private ReportRepository _reportRepo = null!;
        private AuditTrailService _auditService = null!;
        private SalesOrderService _salesService = null!;
        private PurchaseOrderService _poService = null!;
        private ReportingService _service = null!;

        private int _customerId;
        private int _itemId;
        private int _supplierId;

        [SetUp]
        public void SetUp()
        {
            var options = new DbContextOptionsBuilder<ShingarContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _context      = new ShingarContext(options);
            _uow          = new UnitOfWork(_context);
            _salesRepo    = new SalesOrderRepository(_context);
            _poRepo       = new PurchaseOrderRepository(_context);
            _reportRepo   = new ReportRepository(_context);
            _auditService = new AuditTrailService(_uow, new AuditLogRepository(_context), NullLogger<AuditTrailService>.Instance);
            _salesService = new SalesOrderService(_uow, _salesRepo, _auditService, NullLogger<SalesOrderService>.Instance);
            _poService    = new PurchaseOrderService(_uow, _poRepo, _auditService, NullLogger<PurchaseOrderService>.Instance);
            _service      = new ReportingService(_uow, _salesRepo, _poRepo, _reportRepo, NullLogger<ReportingService>.Instance);

            SeedTestData();
        }

        [TearDown]
        public void TearDown()
        {
            _uow.Dispose();
            _context.Dispose();
        }

        private void SeedTestData()
        {
            var customer = new Customer { FirstName = "Report", Mobile = "9111111111" };
            var supplier = new Supplier { SupplierName = "Gold Supplier" };
            var metal    = new Metal { MetalType = "Gold", PurityCode = "22K", Fineness = 916 };
            var cat      = new ItemCategory { CategoryName = "Ring", CategoryCode = "RNG" };

            _context.Customers.Add(customer);
            _context.Suppliers.Add(supplier);
            _context.Metals.Add(metal);
            _context.ItemCategories.Add(cat);
            _context.SaveChanges();

            var item = new FinishedGood
            {
                SKU = "RNG001", ItemName = "Ring", CategoryId = cat.CategoryId,
                MetalId = metal.MetalId, GrossWeight = 5, NetWeight = 4.8m, SalePrice = 20000
            };
            _context.FinishedGoods.Add(item);
            _context.SaveChanges();

            _customerId = customer.CustomerId;
            _supplierId = supplier.SupplierId;
            _itemId     = item.ItemId;
        }

        // ── Sales Report Tests ────────────────────────────────────────

        [Test]
        public async Task GetSalesReport_DateRange()
        {
            await _salesService.CreateOrderAsync(_customerId, new[] { (_itemId, 1, 20000m, 0m) }, null, 1);
            var (orders, total, count, avg) = await _service.GetSalesReportAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
            Assert.That(count, Is.EqualTo(1));
            Assert.That(total, Is.EqualTo(20000));
            Assert.That(avg, Is.EqualTo(20000));
        }

        [Test]
        public async Task GetSalesReport_EmptyRange()
        {
            var (orders, total, count, avg) = await _service.GetSalesReportAsync(DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(-9));
            Assert.That(count, Is.EqualTo(0));
            Assert.That(total, Is.EqualTo(0));
            Assert.That(avg, Is.EqualTo(0));
        }

        [Test]
        public async Task GetSalesReport_MultipleOrders()
        {
            await _salesService.CreateOrderAsync(_customerId, new[] { (_itemId, 1, 10000m, 0m) }, null, 1);
            await _salesService.CreateOrderAsync(_customerId, new[] { (_itemId, 2, 10000m, 0m) }, null, 1);
            var (orders, total, count, avg) = await _service.GetSalesReportAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
            Assert.That(count, Is.EqualTo(2));
            Assert.That(total, Is.EqualTo(30000));
        }

        // ── Purchase Report Tests ─────────────────────────────────────

        [Test]
        public async Task GetPurchaseReport_DateRange()
        {
            await _poService.CreatePOAsync(_supplierId, new[] { ("Gold", (int?)null, 10m, 5000m, 3m) }, null, null, 1);
            var (pos, total, count) = await _service.GetPurchaseReportAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public async Task GetPurchaseReport_MultipleSuppliers()
        {
            var sup2 = new Supplier { SupplierName = "Silver Supplier" };
            _context.Suppliers.Add(sup2);
            await _context.SaveChangesAsync();

            await _poService.CreatePOAsync(_supplierId, new[] { ("Gold", (int?)null, 5m, 5000m, 3m) }, null, null, 1);
            await _poService.CreatePOAsync(sup2.SupplierId, new[] { ("Silver", (int?)null, 5m, 1000m, 3m) }, null, null, 1);

            var (pos, total, count) = await _service.GetPurchaseReportAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
            Assert.That(count, Is.EqualTo(2));
        }

        // ── Customer Performance Tests ────────────────────────────────

        [Test]
        public async Task GetCustomerPerformance_Valid()
        {
            await _salesService.CreateOrderAsync(_customerId, new[] { (_itemId, 1, 15000m, 0m) }, null, 1);
            var (orders, spend, count, avg) = await _service.GetCustomerPerformanceAsync(_customerId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
            Assert.That(count, Is.EqualTo(1));
            Assert.That(spend, Is.EqualTo(15000));
        }

        [Test]
        public async Task GetCustomerPerformance_NoOrders()
        {
            var (orders, spend, count, avg) = await _service.GetCustomerPerformanceAsync(999, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
            Assert.That(count, Is.EqualTo(0));
            Assert.That(spend, Is.EqualTo(0));
        }

        // ── Report Template Tests ─────────────────────────────────────

        [Test]
        public async Task CreateReportTemplate_Valid()
        {
            var template = await _service.CreateReportTemplateAsync("Sales Monthly", "Sales", "SELECT * FROM Orders", "[{\"col\":\"OrderNo\"}]");
            Assert.That(template.TemplateName, Is.EqualTo("Sales Monthly"));
            Assert.That(template.IsActive, Is.True);
        }

        [Test]
        public async Task GetReportTemplates_ByType()
        {
            await _service.CreateReportTemplateAsync("Sales Monthly", "Sales", "query", "cols");
            await _service.CreateReportTemplateAsync("Purchase Monthly", "Purchase", "query", "cols");
            var salesTemplates = (await _service.GetReportTemplatesAsync("Sales")).ToList();
            Assert.That(salesTemplates.All(t => t.ReportType == "Sales"), Is.True);
        }

        // ── Schedule Tests ────────────────────────────────────────────

        [Test]
        public async Task ScheduleReport_Valid()
        {
            var template = await _service.CreateReportTemplateAsync("Daily Sales", "Sales", "query", "cols");
            var schedule = await _service.ScheduleReportAsync(template.Id, "Daily Sales Report", "Daily", "manager@test.com");
            Assert.That(schedule.Frequency, Is.EqualTo("Daily"));
            Assert.That(schedule.IsActive, Is.True);
        }

        [Test]
        public async Task ScheduleReport_MultipleFrequencies()
        {
            var template  = await _service.CreateReportTemplateAsync("Multi-Freq", "Sales", "q", "c");
            var daily     = await _service.ScheduleReportAsync(template.Id, "Daily", "Daily", "a@a.com");
            var monthly   = await _service.ScheduleReportAsync(template.Id, "Monthly", "Monthly", "b@b.com");
            Assert.That(daily.NextRunDate, Is.LessThan(monthly.NextRunDate));
        }

        // ── KPI Tests ─────────────────────────────────────────────────

        [Test]
        public async Task CreateKPI_Valid()
        {
            var kpi = await _service.CreateKPIAsync("SALES_MONTHLY", "Monthly Sales", "Revenue", 1000000, "INR");
            Assert.That(kpi.KPICode, Is.EqualTo("SALES_MONTHLY"));
            Assert.That(kpi.CurrentValue, Is.EqualTo(0));
        }

        [Test]
        public async Task CreateKPI_DuplicateCode()
        {
            await _service.CreateKPIAsync("UNIQUE_KPI", "Test KPI", "Revenue", 100, "INR");
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CreateKPIAsync("UNIQUE_KPI", "Another KPI", "Revenue", 200, "INR"));
        }

        [Test]
        public async Task UpdateKPIValue_Valid()
        {
            var kpi     = await _service.CreateKPIAsync("ORDERS_COUNT", "Order Count", "Operations", 100, "Count");
            var updated = await _service.UpdateKPIValueAsync(kpi.Id, 85, 80);
            Assert.That(updated.CurrentValue, Is.EqualTo(85));
            Assert.That(updated.PreviousValue, Is.EqualTo(80));
        }

        [Test]
        public async Task GetKPIsByCategory()
        {
            await _service.CreateKPIAsync("K001", "KPI 1", "Revenue", 100, "INR");
            var list = (await _service.GetKPIsByCategory("Revenue")).ToList();
            Assert.That(list.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task GetKPIsByCategory_Multiple()
        {
            await _service.CreateKPIAsync("K002", "Rev KPI 1", "Revenue2", 100, "INR");
            await _service.CreateKPIAsync("K003", "Rev KPI 2", "Revenue2", 200, "INR");
            await _service.CreateKPIAsync("K004", "Ops KPI",   "Operations", 50, "Count");
            var revenueKPIs = (await _service.GetKPIsByCategory("Revenue2")).ToList();
            Assert.That(revenueKPIs.Count, Is.EqualTo(2));
        }

        // ── Dashboard Widget Tests ────────────────────────────────────

        [Test]
        public async Task CreateDashboardWidget_Valid()
        {
            var widget = await _service.CreateDashboardWidgetAsync("Sales KPI", "KPICard", "sales_endpoint", "{\"color\":\"blue\"}");
            Assert.That(widget.WidgetName, Is.EqualTo("Sales KPI"));
            Assert.That(widget.IsActive, Is.True);
        }

        [Test]
        public async Task GetActiveDashboardWidgets()
        {
            await _service.CreateDashboardWidgetAsync("Widget 1", "Chart", "source1", "{}");
            await _service.CreateDashboardWidgetAsync("Widget 2", "Table", "source2", "{}");
            var widgets = (await _service.GetActiveDashboardWidgetsAsync()).ToList();
            Assert.That(widgets.Count, Is.EqualTo(2));
        }

        // ── Financial Summary Tests ───────────────────────────────────

        [Test]
        public async Task GetFinancialSummary_Valid()
        {
            await _salesService.CreateOrderAsync(_customerId, new[] { (_itemId, 1, 30000m, 0m) }, null, 1);
            await _poService.CreatePOAsync(_supplierId, new[] { ("Material", (int?)null, 1m, 10000m, 0m) }, null, null, 1);
            var (rev, exp, profit, margin) = await _service.GetFinancialSummaryAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
            Assert.That(rev, Is.GreaterThan(0));
        }

        [Test]
        public async Task GetFinancialSummary_EmptyRange()
        {
            var (rev, exp, profit, margin) = await _service.GetFinancialSummaryAsync(DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(-9));
            Assert.That(rev, Is.EqualTo(0));
            Assert.That(exp, Is.EqualTo(0));
            Assert.That(profit, Is.EqualTo(0));
        }
    }
}
