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
    public class PurchaseOrderServiceTests
    {
        private ShingarContext _context = null!;
        private UnitOfWork _uow = null!;
        private PurchaseOrderRepository _poRepo = null!;
        private AuditTrailService _auditService = null!;
        private PurchaseOrderService _service = null!;

        private int _supplierId;
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
            _poRepo       = new PurchaseOrderRepository(_context);
            _auditService = new AuditTrailService(_uow, new AuditLogRepository(_context), NullLogger<AuditTrailService>.Instance);
            _service      = new PurchaseOrderService(_uow, _poRepo, _auditService, NullLogger<PurchaseOrderService>.Instance);

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
            var supplier = new Supplier { SupplierName = "Gold Supplier Ltd", IsActive = true };
            _context.Suppliers.Add(supplier);

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

            _supplierId = supplier.SupplierId;
            _itemId     = item.ItemId;
        }

        private IEnumerable<(string Desc, int? ItemId, decimal Qty, decimal UnitPrice, decimal TaxPct)> Lines(decimal qty = 10, decimal price = 5000, decimal tax = 3)
            => new[] { ("Gold Wire 22K", (int?)_itemId, qty, price, tax) };

        // ── Create Tests ──────────────────────────────────────────────

        [Test]
        public async Task CreatePO_Valid()
        {
            var po = await _service.CreatePOAsync(_supplierId, Lines(), DateTime.UtcNow.AddDays(7), "Net 30", 1);
            Assert.That(po.Status, Is.EqualTo("Draft"));
            Assert.That(po.PONumber, Does.StartWith("PO-"));
        }

        [Test]
        public async Task CreatePO_NoLines()
        {
            var po = await _service.CreatePOAsync(_supplierId, Enumerable.Empty<(string, int?, decimal, decimal, decimal)>(), null, null, 1);
            Assert.That(po.TotalAmount, Is.EqualTo(0));
        }

        [Test]
        public async Task CreatePO_CalculatesTotals()
        {
            var po = await _service.CreatePOAsync(_supplierId, new[] { ("Item", (int?)null, 10m, 1000m, 3m) }, null, null, 1);
            Assert.That(po.TotalAmount, Is.EqualTo(10000));
            Assert.That(po.TaxAmount, Is.EqualTo(300));
            Assert.That(po.NetAmount, Is.EqualTo(10300));
        }

        // ── Lifecycle Tests ───────────────────────────────────────────

        [Test]
        public async Task SendPO_Draft()
        {
            var po   = await _service.CreatePOAsync(_supplierId, Lines(), null, null, 1);
            var sent = await _service.SendPOAsync(po.Id, 1);
            Assert.That(sent.Status, Is.EqualTo("Sent"));
        }

        [Test]
        public async Task SendPO_AlreadySent_ShouldThrow()
        {
            var po = await _service.CreatePOAsync(_supplierId, Lines(), null, null, 1);
            await _service.SendPOAsync(po.Id, 1);
            Assert.ThrowsAsync<InvalidOperationException>(() => _service.SendPOAsync(po.Id, 1));
        }

        [Test]
        public async Task ConfirmPO_Sent()
        {
            var po        = await _service.CreatePOAsync(_supplierId, Lines(), null, null, 1);
            await _service.SendPOAsync(po.Id, 1);
            var confirmed = await _service.ConfirmPOAsync(po.Id, 1);
            Assert.That(confirmed.Status, Is.EqualTo("Confirmed"));
        }

        [Test]
        public async Task ReceivePO_Full()
        {
            var po = await _service.CreatePOAsync(_supplierId, Lines(5), null, null, 1);
            await _service.SendPOAsync(po.Id, 1);
            await _service.ConfirmPOAsync(po.Id, 1);

            var detailed = (await _poRepo.GetWithLinesAsync(po.Id))!;
            var lineId   = detailed.Lines.First().Id;

            var received = await _service.ReceivePOAsync(po.Id, 1, new[] { (lineId, 5m) });
            Assert.That(received.Status, Is.EqualTo("Received"));
        }

        [Test]
        public async Task ReceivePO_Partial()
        {
            var po = await _service.CreatePOAsync(_supplierId, Lines(10), null, null, 1);
            await _service.SendPOAsync(po.Id, 1);
            await _service.ConfirmPOAsync(po.Id, 1);

            var detailed = (await _poRepo.GetWithLinesAsync(po.Id))!;
            var lineId   = detailed.Lines.First().Id;

            var received = await _service.ReceivePOAsync(po.Id, 1, new[] { (lineId, 5m) });
            Assert.That(received.Status, Is.EqualTo("PartiallyReceived"));
        }

        [Test]
        public async Task ReceivePO_OverReceive()
        {
            var po = await _service.CreatePOAsync(_supplierId, Lines(5), null, null, 1);
            await _service.SendPOAsync(po.Id, 1);
            await _service.ConfirmPOAsync(po.Id, 1);

            var detailed = (await _poRepo.GetWithLinesAsync(po.Id))!;
            var lineId   = detailed.Lines.First().Id;

            // Over-receiving still marks as received from the service perspective
            var received = await _service.ReceivePOAsync(po.Id, 1, new[] { (lineId, 10m) });
            Assert.That(received.Status, Is.EqualTo("Received"));
        }

        [Test]
        public async Task InvoicePO_Valid()
        {
            var po = await _service.CreatePOAsync(_supplierId, Lines(), null, null, 1);
            await _service.SendPOAsync(po.Id, 1);
            await _service.ConfirmPOAsync(po.Id, 1);

            var invoiced = await _service.InvoicePOAsync(po.Id, 1);
            Assert.That(invoiced.Status, Is.EqualTo("Invoiced"));
        }

        [Test]
        public async Task CancelPO_Draft()
        {
            var po        = await _service.CreatePOAsync(_supplierId, Lines(), null, null, 1);
            var cancelled = await _service.CancelPOAsync(po.Id, 1);
            Assert.That(cancelled.Status, Is.EqualTo("Cancelled"));
        }

        [Test]
        public async Task CancelPO_Received_ShouldThrow()
        {
            var po = await _service.CreatePOAsync(_supplierId, Lines(5), null, null, 1);
            await _service.SendPOAsync(po.Id, 1);
            await _service.ConfirmPOAsync(po.Id, 1);

            var detailed = (await _poRepo.GetWithLinesAsync(po.Id))!;
            var lineId   = detailed.Lines.First().Id;
            await _service.ReceivePOAsync(po.Id, 1, new[] { (lineId, 5m) });

            Assert.ThrowsAsync<InvalidOperationException>(() => _service.CancelPOAsync(po.Id, 1));
        }

        [Test]
        public async Task CancelPO_Confirmed_ShouldThrow()
        {
            var po = await _service.CreatePOAsync(_supplierId, Lines(), null, null, 1);
            await _service.SendPOAsync(po.Id, 1);
            await _service.ConfirmPOAsync(po.Id, 1);
            Assert.ThrowsAsync<InvalidOperationException>(() => _service.CancelPOAsync(po.Id, 1));
        }

        // ── Query Tests ───────────────────────────────────────────────

        [Test]
        public async Task GetPOsBySupplier()
        {
            await _service.CreatePOAsync(_supplierId, Lines(), null, null, 1);
            await _service.CreatePOAsync(_supplierId, Lines(), null, null, 1);
            var pos = (await _service.GetPOsBySupplierAsync(_supplierId)).ToList();
            Assert.That(pos.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task GetPOsByStatus()
        {
            await _service.CreatePOAsync(_supplierId, Lines(), null, null, 1);
            var pos = (await _service.GetPOsByStatusAsync("Draft")).ToList();
            Assert.That(pos.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task GetPOWithDetails_IncludesLines()
        {
            var po = await _service.CreatePOAsync(_supplierId, Lines(), null, null, 1);
            var detailed = await _service.GetPOWithDetailsAsync(po.Id);
            Assert.That(detailed, Is.Not.Null);
            Assert.That(detailed!.Lines.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task GetPOWithDetails_NotFound()
        {
            var po = await _service.GetPOWithDetailsAsync(99999);
            Assert.That(po, Is.Null);
        }

        [Test]
        public async Task GetOverduePOs()
        {
            var po = await _service.CreatePOAsync(_supplierId, Lines(), DateTime.UtcNow.AddDays(-5), null, 1);
            await _service.SendPOAsync(po.Id, 1);
            var overdue = (await _service.GetOverduePOsAsync()).ToList();
            Assert.That(overdue.Count, Is.GreaterThan(0));
        }

        // ── Performance Tests ─────────────────────────────────────────

        [Test]
        public async Task GetSupplierPerformance_Valid()
        {
            await _service.CreatePOAsync(_supplierId, Lines(), null, null, 1);
            var (total, onTime, pct, value) = await _service.GetSupplierPerformanceAsync(
                _supplierId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
            Assert.That(total, Is.EqualTo(1));
        }

        [Test]
        public async Task GetSupplierPerformance_Empty()
        {
            var (total, onTime, pct, value) = await _service.GetSupplierPerformanceAsync(
                _supplierId, DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(-9));
            Assert.That(total, Is.EqualTo(0));
            Assert.That(pct, Is.EqualTo(0));
        }
    }
}
