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
    public class SalesOrderServiceTests
    {
        private ShingarContext _context = null!;
        private UnitOfWork _uow = null!;
        private SalesOrderRepository _orderRepo = null!;
        private AuditTrailService _auditService = null!;
        private SalesOrderService _service = null!;

        private int _customerId;
        private int _itemId;

        [SetUp]
        public void SetUp()
        {
            var options = new DbContextOptionsBuilder<ShingarContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _context      = new ShingarContext(options);
            _uow          = new UnitOfWork(_context);
            _orderRepo    = new SalesOrderRepository(_context);
            _auditService = new AuditTrailService(_uow, new AuditLogRepository(_context), NullLogger<AuditTrailService>.Instance);
            _service      = new SalesOrderService(_uow, _orderRepo, _auditService, NullLogger<SalesOrderService>.Instance);

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
            var customer = new Customer { FirstName = "Test", Mobile = "9000000001" };
            _context.Customers.Add(customer);

            var metal = new Metal { MetalType = "Gold", PurityCode = "22K", Fineness = 916 };
            _context.Metals.Add(metal);

            var cat = new ItemCategory { CategoryName = "Ring", CategoryCode = "RNG" };
            _context.ItemCategories.Add(cat);

            _context.SaveChanges();

            var item = new FinishedGood
            {
                SKU = "RNG001", ItemName = "Gold Ring", CategoryId = cat.CategoryId,
                MetalId = metal.MetalId, GrossWeight = 5, NetWeight = 4.8m, SalePrice = 25000
            };
            _context.FinishedGoods.Add(item);
            _context.SaveChanges();

            _customerId = customer.CustomerId;
            _itemId     = item.ItemId;
        }

        private IEnumerable<(int ItemId, int Qty, decimal UnitPrice, decimal DiscPct)> Lines(int qty = 1, decimal price = 25000, decimal disc = 0)
            => new[] { (_itemId, qty, price, disc) };

        // ── Create Tests ──────────────────────────────────────────────

        [Test]
        public async Task CreateOrder_Valid()
        {
            var order = await _service.CreateOrderAsync(_customerId, Lines(), null, 1);
            Assert.That(order.Status, Is.EqualTo("Draft"));
            Assert.That(order.CustomerId, Is.EqualTo(_customerId));
            Assert.That(order.OrderNo, Does.StartWith("SO-"));
        }

        [Test]
        public async Task CreateOrder_NoLines_ShouldThrow()
        {
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CreateOrderAsync(_customerId, Enumerable.Empty<(int, int, decimal, decimal)>(), null, 1));
        }

        [Test]
        public async Task CreateOrder_CalculatesTotals()
        {
            var order = await _service.CreateOrderAsync(_customerId, new[] { (_itemId, 2, 10000m, 10m) }, null, 1);
            // 2 * 10000 * (1 - 10/100) = 18000
            Assert.That(order.TotalAmount, Is.EqualTo(18000));
        }

        // ── Submit Tests ──────────────────────────────────────────────

        [Test]
        public async Task SubmitOrder_Draft()
        {
            var order = await _service.CreateOrderAsync(_customerId, Lines(), null, 1);
            var submitted = await _service.SubmitOrderAsync(order.Id, 1);
            Assert.That(submitted.Status, Is.EqualTo("Submitted"));
        }

        [Test]
        public async Task SubmitOrder_NotDraft_ShouldThrow()
        {
            var order = await _service.CreateOrderAsync(_customerId, Lines(), null, 1);
            await _service.SubmitOrderAsync(order.Id, 1);
            Assert.ThrowsAsync<InvalidOperationException>(() => _service.SubmitOrderAsync(order.Id, 1));
        }

        [Test]
        public async Task SubmitOrder_UpdatesAudit()
        {
            var order = await _service.CreateOrderAsync(_customerId, Lines(), null, 1);
            await _service.SubmitOrderAsync(order.Id, 1);
            var logs = (await _context.AuditLogs.ToListAsync()).Where(a => a.EntityName == "SalesOrder").ToList();
            Assert.That(logs.Count, Is.GreaterThan(0));
        }

        // ── Approve Tests ─────────────────────────────────────────────

        [Test]
        public async Task ApproveOrder_Level1()
        {
            var order = await _service.CreateOrderAsync(_customerId, Lines(), null, 1);
            await _service.SubmitOrderAsync(order.Id, 1);
            var approval = await _service.ApproveOrderAsync(order.Id, 2, 1, "Looks good");
            Assert.That(approval.Status, Is.EqualTo("Approved"));
        }

        [Test]
        public async Task ApproveOrder_Level2_FullApproval()
        {
            var order = await _service.CreateOrderAsync(_customerId, Lines(), null, 1);
            await _service.SubmitOrderAsync(order.Id, 1);
            await _service.ApproveOrderAsync(order.Id, 2, 1, null);
            var refreshed = await _context.SalesOrders.FindAsync(order.Id);
            Assert.That(refreshed!.Status, Is.EqualTo("Approved"));
        }

        [Test]
        public async Task ApproveOrder_AlreadyApproved()
        {
            var order = await _service.CreateOrderAsync(_customerId, Lines(), null, 1);
            await _service.SubmitOrderAsync(order.Id, 1);
            await _service.ApproveOrderAsync(order.Id, 2, 1, null);
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.ApproveOrderAsync(order.Id, 2, 1, null));
        }

        // ── Reject Tests ──────────────────────────────────────────────

        [Test]
        public async Task RejectOrder_Valid()
        {
            var order = await _service.CreateOrderAsync(_customerId, Lines(), null, 1);
            await _service.SubmitOrderAsync(order.Id, 1);
            var rejection = await _service.RejectOrderAsync(order.Id, 2, 1, "Out of stock");
            Assert.That(rejection.Status, Is.EqualTo("Rejected"));
            var refreshed = await _context.SalesOrders.FindAsync(order.Id);
            Assert.That(refreshed!.Status, Is.EqualTo("Cancelled"));
        }

        // ── Status Update Tests ───────────────────────────────────────

        [Test]
        public async Task UpdateStatus_ValidTransition()
        {
            var order = await _service.CreateOrderAsync(_customerId, Lines(), null, 1);
            await _service.SubmitOrderAsync(order.Id, 1);
            await _service.ApproveOrderAsync(order.Id, 2, 1, null);
            var updated = await _service.UpdateOrderStatusAsync(order.Id, "InProduction", 1);
            Assert.That(updated.Status, Is.EqualTo("InProduction"));
        }

        [Test]
        public async Task CancelOrder_Draft()
        {
            var order = await _service.CreateOrderAsync(_customerId, Lines(), null, 1);
            var cancelled = await _service.CancelOrderAsync(order.Id, 1, "Customer request");
            Assert.That(cancelled.Status, Is.EqualTo("Cancelled"));
        }

        [Test]
        public async Task CancelOrder_Submitted()
        {
            var order = await _service.CreateOrderAsync(_customerId, Lines(), null, 1);
            await _service.SubmitOrderAsync(order.Id, 1);
            var cancelled = await _service.CancelOrderAsync(order.Id, 1, "Changed mind");
            Assert.That(cancelled.Status, Is.EqualTo("Cancelled"));
        }

        [Test]
        public async Task CancelOrder_Delivered_ShouldThrow()
        {
            var order = await _service.CreateOrderAsync(_customerId, Lines(), null, 1);
            await _service.SubmitOrderAsync(order.Id, 1);
            await _service.ApproveOrderAsync(order.Id, 2, 1, null);
            await _service.UpdateOrderStatusAsync(order.Id, "InProduction", 1);
            await _service.UpdateOrderStatusAsync(order.Id, "ReadyToShip", 1);
            await _service.UpdateOrderStatusAsync(order.Id, "Shipped", 1);
            await _service.UpdateOrderStatusAsync(order.Id, "Delivered", 1);
            Assert.ThrowsAsync<InvalidOperationException>(() => _service.CancelOrderAsync(order.Id, 1, "Too late"));
        }

        // ── Payment Schedule Tests ────────────────────────────────────

        [Test]
        public async Task AddPaymentSchedule_Valid()
        {
            var order    = await _service.CreateOrderAsync(_customerId, Lines(), null, 1);
            var schedule = await _service.AddPaymentScheduleAsync(order.Id, DateTime.UtcNow.AddDays(30), 12500);
            Assert.That(schedule.DueAmount, Is.EqualTo(12500));
            Assert.That(schedule.IsPaid, Is.False);
        }

        [Test]
        public async Task AddMultiplePayments()
        {
            var order = await _service.CreateOrderAsync(_customerId, Lines(), null, 1);
            await _service.AddPaymentScheduleAsync(order.Id, DateTime.UtcNow.AddDays(30), 12500);
            await _service.AddPaymentScheduleAsync(order.Id, DateTime.UtcNow.AddDays(60), 12500);
            var schedules = await _context.OrderPaymentSchedules.Where(s => s.SalesOrderId == order.Id).ToListAsync();
            Assert.That(schedules.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task MarkPaymentPaid_Valid()
        {
            var order    = await _service.CreateOrderAsync(_customerId, Lines(), null, 1);
            var schedule = await _service.AddPaymentScheduleAsync(order.Id, DateTime.UtcNow.AddDays(30), 25000);
            var paid     = await _service.MarkPaymentPaidAsync(schedule.Id, 25000, "Cash", "REF001");
            Assert.That(paid.IsPaid, Is.True);
            Assert.That(paid.PaymentMethod, Is.EqualTo("Cash"));
        }

        // ── Query Tests ───────────────────────────────────────────────

        [Test]
        public async Task GetOrdersByCustomer()
        {
            await _service.CreateOrderAsync(_customerId, Lines(), null, 1);
            await _service.CreateOrderAsync(_customerId, Lines(), null, 1);
            var orders = (await _service.GetOrdersByCustomerAsync(_customerId)).ToList();
            Assert.That(orders.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task GetOrdersByStatus()
        {
            var o1 = await _service.CreateOrderAsync(_customerId, Lines(), null, 1);
            await _service.SubmitOrderAsync(o1.Id, 1);
            await _service.CreateOrderAsync(_customerId, Lines(), null, 1);
            var submitted = (await _service.GetOrdersByStatusAsync("Submitted")).ToList();
            Assert.That(submitted.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task GetOrderWithDetails_IncludesLines()
        {
            var order = await _service.CreateOrderAsync(_customerId, Lines(2), null, 1);
            var detailed = await _service.GetOrderWithDetailsAsync(order.Id);
            Assert.That(detailed, Is.Not.Null);
            Assert.That(detailed!.Lines.Count, Is.EqualTo(1));
        }

        // ── Return Tests ──────────────────────────────────────────────

        [Test]
        public async Task ProcessReturn_Valid()
        {
            var order = await _service.CreateOrderAsync(_customerId, Lines(), null, 1);
            await _service.SubmitOrderAsync(order.Id, 1);
            await _service.ApproveOrderAsync(order.Id, 2, 1, null);
            await _service.UpdateOrderStatusAsync(order.Id, "InProduction", 1);
            await _service.UpdateOrderStatusAsync(order.Id, "ReadyToShip", 1);
            await _service.UpdateOrderStatusAsync(order.Id, "Shipped", 1);
            await _service.UpdateOrderStatusAsync(order.Id, "Delivered", 1);

            var ret = await _service.ProcessReturnAsync(order.Id, "Damaged", "Item arrived broken", 25000, "BankTransfer");
            Assert.That(ret.ReasonCode, Is.EqualTo("Damaged"));
            Assert.That(ret.ReturnStatus, Is.EqualTo("Pending"));
        }

        [Test]
        public async Task ProcessReturn_Invalid_ShouldThrow()
        {
            var order = await _service.CreateOrderAsync(_customerId, Lines(), null, 1);
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.ProcessReturnAsync(order.Id, "WrongItem", null, 0, null));
        }

        // ── Metrics Tests ─────────────────────────────────────────────

        [Test]
        public async Task GetOrderMetrics_Valid()
        {
            var o1 = await _service.CreateOrderAsync(_customerId, Lines(1, 10000), null, 1);
            var o2 = await _service.CreateOrderAsync(_customerId, Lines(1, 20000), null, 1);
            await _service.CancelOrderAsync(o2.Id, 1, "Test");

            var (total, value, cancelled, rate) = await _service.GetOrderMetricsAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
            Assert.That(total, Is.EqualTo(2));
            Assert.That(cancelled, Is.EqualTo(1));
            Assert.That(rate, Is.EqualTo(50));
        }

        [Test]
        public async Task GetOrderMetrics_EmptyPeriod()
        {
            var (total, value, cancelled, rate) = await _service.GetOrderMetricsAsync(DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(-9));
            Assert.That(total, Is.EqualTo(0));
            Assert.That(rate, Is.EqualTo(0));
        }

        [Test]
        public async Task CreateOrder_MultipleLines()
        {
            var lines = new[]
            {
                (_itemId, 1, 10000m, 0m),
                (_itemId, 2, 5000m, 5m)
            };
            var order = await _service.CreateOrderAsync(_customerId, lines, "Multi-line test", 1);
            Assert.That(order.Lines.Count, Is.EqualTo(2));
        }
    }
}
