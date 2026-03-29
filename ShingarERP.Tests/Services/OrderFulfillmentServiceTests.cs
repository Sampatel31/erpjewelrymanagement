using System;
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
    public class OrderFulfillmentServiceTests
    {
        private ShingarContext _context = null!;
        private UnitOfWork _uow = null!;
        private OrderFulfillmentRepository _fulfillmentRepo = null!;
        private SalesOrderRepository _orderRepo = null!;
        private AuditTrailService _auditService = null!;
        private SalesOrderService _salesService = null!;
        private OrderFulfillmentService _service = null!;

        private int _customerId;
        private int _itemId;

        [SetUp]
        public void SetUp()
        {
            var options = new DbContextOptionsBuilder<ShingarContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _context         = new ShingarContext(options);
            _uow             = new UnitOfWork(_context);
            _fulfillmentRepo = new OrderFulfillmentRepository(_context);
            _orderRepo       = new SalesOrderRepository(_context);
            _auditService    = new AuditTrailService(_uow, new AuditLogRepository(_context), NullLogger<AuditTrailService>.Instance);
            _salesService    = new SalesOrderService(_uow, _orderRepo, _auditService, NullLogger<SalesOrderService>.Instance);
            _service         = new OrderFulfillmentService(_uow, _fulfillmentRepo, _auditService, NullLogger<OrderFulfillmentService>.Instance);

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

        private async Task<SalesOrder> CreateApprovedOrderAsync()
        {
            var order = await _salesService.CreateOrderAsync(_customerId, new[] { (_itemId, 1, 25000m, 0m) }, null, 1);
            await _salesService.SubmitOrderAsync(order.Id, 1);
            await _salesService.ApproveOrderAsync(order.Id, 2, 1, null);
            return order;
        }

        // ── Create Tests ──────────────────────────────────────────────

        [Test]
        public async Task CreateFulfillment_Approved()
        {
            var order       = await CreateApprovedOrderAsync();
            var fulfillment = await _service.CreateFulfillmentAsync(order.Id);
            Assert.That(fulfillment.Status, Is.EqualTo("Pending"));
            Assert.That(fulfillment.SalesOrderId, Is.EqualTo(order.Id));
        }

        [Test]
        public async Task CreateFulfillment_NotApproved_ShouldThrow()
        {
            var order = await _salesService.CreateOrderAsync(_customerId, new[] { (_itemId, 1, 25000m, 0m) }, null, 1);
            Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateFulfillmentAsync(order.Id));
        }

        [Test]
        public async Task CreateFulfillment_Duplicate_ShouldThrow()
        {
            var order = await CreateApprovedOrderAsync();
            await _service.CreateFulfillmentAsync(order.Id);
            Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateFulfillmentAsync(order.Id));
        }

        // ── Packing Tests ─────────────────────────────────────────────

        [Test]
        public async Task StartPacking_Valid()
        {
            var order       = await CreateApprovedOrderAsync();
            var fulfillment = await _service.CreateFulfillmentAsync(order.Id);
            var packing     = await _service.StartPackingAsync(fulfillment.Id, 1);
            Assert.That(packing.Status, Is.EqualTo("Packing"));
        }

        [Test]
        public async Task StartPacking_NotPending_ShouldThrow()
        {
            var order       = await CreateApprovedOrderAsync();
            var fulfillment = await _service.CreateFulfillmentAsync(order.Id);
            await _service.StartPackingAsync(fulfillment.Id, 1);
            Assert.ThrowsAsync<InvalidOperationException>(() => _service.StartPackingAsync(fulfillment.Id, 1));
        }

        [Test]
        public async Task CompletePacking_Valid()
        {
            var order       = await CreateApprovedOrderAsync();
            var fulfillment = await _service.CreateFulfillmentAsync(order.Id);
            await _service.StartPackingAsync(fulfillment.Id, 1);
            var slipNo  = await _service.GeneratePackingSlipNumberAsync();
            var packed  = await _service.CompletePackingAsync(fulfillment.Id, slipNo, 1);
            Assert.That(packed.Status, Is.EqualTo("Packed"));
            Assert.That(packed.PackingSlipNo, Is.EqualTo(slipNo));

            var refreshedOrder = await _context.SalesOrders.FindAsync(order.Id);
            Assert.That(refreshedOrder!.Status, Is.EqualTo("ReadyToShip"));
        }

        // ── Ship/Delivery Tests ───────────────────────────────────────

        [Test]
        public async Task ShipOrder_Valid()
        {
            var order       = await CreateApprovedOrderAsync();
            var fulfillment = await _service.CreateFulfillmentAsync(order.Id);
            await _service.StartPackingAsync(fulfillment.Id, 1);
            await _service.CompletePackingAsync(fulfillment.Id, "PS-001", 1);
            var shipped = await _service.ShipOrderAsync(fulfillment.Id, "TRK123456", "BlueDart", 1);
            Assert.That(shipped.Status, Is.EqualTo("Shipped"));
            Assert.That(shipped.ShipmentTrackingNo, Is.EqualTo("TRK123456"));
        }

        [Test]
        public async Task ConfirmDelivery_Valid()
        {
            var order       = await CreateApprovedOrderAsync();
            var fulfillment = await _service.CreateFulfillmentAsync(order.Id);
            await _service.StartPackingAsync(fulfillment.Id, 1);
            await _service.CompletePackingAsync(fulfillment.Id, "PS-001", 1);
            await _service.ShipOrderAsync(fulfillment.Id, "TRK123456", "BlueDart", 1);
            var delivered = await _service.ConfirmDeliveryAsync(fulfillment.Id, "Ramesh Patel", 1);
            Assert.That(delivered.Status, Is.EqualTo("Delivered"));
        }

        [Test]
        public async Task ConfirmDelivery_NotShipped_ShouldThrow()
        {
            var order       = await CreateApprovedOrderAsync();
            var fulfillment = await _service.CreateFulfillmentAsync(order.Id);
            Assert.ThrowsAsync<InvalidOperationException>(() => _service.ConfirmDeliveryAsync(fulfillment.Id, null, 1));
        }

        [Test]
        public async Task MarkDeliveryFailed_Valid()
        {
            var order       = await CreateApprovedOrderAsync();
            var fulfillment = await _service.CreateFulfillmentAsync(order.Id);
            await _service.StartPackingAsync(fulfillment.Id, 1);
            await _service.CompletePackingAsync(fulfillment.Id, "PS-001", 1);
            await _service.ShipOrderAsync(fulfillment.Id, "TRK001", "FedEx", 1);
            var failed = await _service.MarkDeliveryFailedAsync(fulfillment.Id, "Address not found", 1);
            Assert.That(failed.Status, Is.EqualTo("Failed"));
            Assert.That(failed.FailureReason, Is.EqualTo("Address not found"));
        }

        // ── Query Tests ───────────────────────────────────────────────

        [Test]
        public async Task GetFulfillmentByOrder()
        {
            var order = await CreateApprovedOrderAsync();
            await _service.CreateFulfillmentAsync(order.Id);
            var f = await _service.GetFulfillmentByOrderAsync(order.Id);
            Assert.That(f, Is.Not.Null);
        }

        [Test]
        public async Task GetPendingFulfillments()
        {
            var o1 = await CreateApprovedOrderAsync();
            var o2 = await CreateApprovedOrderAsync();
            await _service.CreateFulfillmentAsync(o1.Id);
            await _service.CreateFulfillmentAsync(o2.Id);
            var pending = (await _service.GetPendingFulfillmentsAsync()).ToList();
            Assert.That(pending.Count, Is.EqualTo(2));
        }

        // ── Utility Tests ─────────────────────────────────────────────

        [Test]
        public async Task GeneratePackingSlipNumber_Format()
        {
            var slipNo = await _service.GeneratePackingSlipNumberAsync();
            Assert.That(slipNo, Does.StartWith("PS-"));
            Assert.That(slipNo.Length, Is.GreaterThan(8));
        }

        // ── Metrics Tests ─────────────────────────────────────────────

        [Test]
        public async Task GetFulfillmentMetrics_Valid()
        {
            var order       = await CreateApprovedOrderAsync();
            var fulfillment = await _service.CreateFulfillmentAsync(order.Id);
            await _service.StartPackingAsync(fulfillment.Id, 1);
            await _service.CompletePackingAsync(fulfillment.Id, "PS-001", 1);
            await _service.ShipOrderAsync(fulfillment.Id, "TRK001", "DHL", 1);
            await _service.ConfirmDeliveryAsync(fulfillment.Id, "Customer", 1);

            var (total, onTime, pct, failed) = await _service.GetFulfillmentMetricsAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
            Assert.That(total, Is.EqualTo(1));
            Assert.That(onTime, Is.EqualTo(1));
        }

        [Test]
        public async Task GetFulfillmentMetrics_Empty()
        {
            var (total, onTime, pct, failed) = await _service.GetFulfillmentMetricsAsync(DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(-9));
            Assert.That(total, Is.EqualTo(0));
            Assert.That(pct, Is.EqualTo(0));
        }
    }
}
