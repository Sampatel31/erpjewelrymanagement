using System;
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
    public class AssayServiceTests
    {
        private ShingarContext _context = null!;
        private UnitOfWork _uow = null!;
        private AssayTestRepository _assayRepo = null!;
        private AssayService _service = null!;

        [SetUp]
        public void SetUp()
        {
            var options = new DbContextOptionsBuilder<ShingarContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            _context   = new ShingarContext(options);
            _uow       = new UnitOfWork(_context);
            _assayRepo = new AssayTestRepository(_context);
            _service   = new AssayService(_uow, _assayRepo, NullLogger<AssayService>.Instance);
        }

        [TearDown]
        public void TearDown() { _uow.Dispose(); _context.Dispose(); }

        // ── RecordTestResultAsync ─────────────────────────────────────────

        [Test]
        public async Task RecordTestResultAsync_ValidInput_CreatesTest()
        {
            var test = await _service.RecordTestResultAsync(1, null, "BIS Lab", "CERT001", 916, 916, 10.5m, 10.5m, DateTime.UtcNow);
            Assert.That(test.Id, Is.GreaterThan(0));
            Assert.That(test.TestNo, Does.StartWith("AT-"));
            Assert.That(test.CertificateNo, Is.EqualTo("CERT001"));
        }

        [Test]
        public async Task RecordTestResultAsync_CalculatesPurityVariance()
        {
            var test = await _service.RecordTestResultAsync(1, null, "BIS Lab", null, 910, 916, 10m, 10m, DateTime.UtcNow);
            Assert.That(test.PurityVariance, Is.EqualTo(6));
        }

        [Test]
        public async Task RecordTestResultAsync_CalculatesWeightVariancePercent()
        {
            var test = await _service.RecordTestResultAsync(1, null, "BIS Lab", null, 916, 916, 9.8m, 10m, DateTime.UtcNow);
            Assert.That(test.WeightVariance, Is.EqualTo(2)); // 0.2/10*100 = 2%
        }

        [Test]
        public async Task RecordTestResultAsync_InvalidPurity_ThrowsException()
        {
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.RecordTestResultAsync(1, null, "Lab", null, 0, 916, 10m, 10m, DateTime.UtcNow));
        }

        [Test]
        public async Task RecordTestResultAsync_PurityAbove1000_ThrowsException()
        {
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.RecordTestResultAsync(1, null, "Lab", null, 1001, 916, 10m, 10m, DateTime.UtcNow));
        }

        [Test]
        public async Task RecordTestResultAsync_ZeroDeclaredWeight_WeightVarianceIsZero()
        {
            var test = await _service.RecordTestResultAsync(1, null, "Lab", null, 916, 916, 10m, 0m, DateTime.UtcNow);
            Assert.That(test.WeightVariance, Is.EqualTo(0));
        }

        // ── GetByCertificateAsync ────────────────────────────────────────

        [Test]
        public async Task GetByCertificateAsync_ExistingCert_ReturnsTest()
        {
            await _service.RecordTestResultAsync(1, null, "Lab", "CERT123", 916, 916, 10m, 10m, DateTime.UtcNow);
            var found = await _service.GetByCertificateAsync("CERT123");
            Assert.That(found, Is.Not.Null);
            Assert.That(found!.CertificateNo, Is.EqualTo("CERT123"));
        }

        [Test]
        public async Task GetByCertificateAsync_NonExistent_ReturnsNull()
        {
            var found = await _service.GetByCertificateAsync("NONEXISTENT");
            Assert.That(found, Is.Null);
        }

        // ── GetByMetalLotAsync ───────────────────────────────────────────

        [Test]
        public async Task GetByMetalLotAsync_ReturnsCorrectTests()
        {
            await _service.RecordTestResultAsync(5, null, "Lab", null, 916, 916, 10m, 10m, DateTime.UtcNow);
            await _service.RecordTestResultAsync(5, null, "Lab", null, 918, 916, 10m, 10m, DateTime.UtcNow);
            await _service.RecordTestResultAsync(6, null, "Lab", null, 916, 916, 10m, 10m, DateTime.UtcNow);
            var tests = await _service.GetByMetalLotAsync(5);
            Assert.That(tests.Count(), Is.EqualTo(2));
        }

        // ── GetHighVarianceTestsAsync ─────────────────────────────────────

        [Test]
        public async Task GetHighVarianceTestsAsync_ReturnsOnlyHighVariance()
        {
            await _service.RecordTestResultAsync(1, null, "Lab", null, 916, 916, 10m, 10m, DateTime.UtcNow); // 0 variance
            await _service.RecordTestResultAsync(2, null, "Lab", null, 915, 916, 10m, 10m, DateTime.UtcNow); // 1 variance
            await _service.RecordTestResultAsync(3, null, "Lab", null, 910, 916, 10m, 10m, DateTime.UtcNow); // 6 variance (> threshold 0.5)
            var from = DateTime.UtcNow.AddDays(-1);
            var highVariance = await _service.GetHighVarianceTestsAsync(from, DateTime.UtcNow.AddDays(1));
            // Only variance > 0.5 (threshold)
            Assert.That(highVariance.All(t => t.PurityVariance > AssayService.PurityVarianceAlertThreshold), Is.True);
        }

        // ── GetAveragePurityVarianceAsync ──────────────────────────────────

        [Test]
        public async Task GetAveragePurityVarianceAsync_MultipleTests_ReturnsAverage()
        {
            await _service.RecordTestResultAsync(1, null, "Lab", null, 914, 916, 10m, 10m, DateTime.UtcNow); // 2 variance
            await _service.RecordTestResultAsync(2, null, "Lab", null, 912, 916, 10m, 10m, DateTime.UtcNow); // 4 variance
            var from = DateTime.UtcNow.AddDays(-1);
            var avg = await _service.GetAveragePurityVarianceAsync(from, DateTime.UtcNow.AddDays(1));
            Assert.That(avg, Is.EqualTo(3m));
        }

        [Test]
        public async Task GetAveragePurityVarianceAsync_NoTests_ReturnsZero()
        {
            var avg = await _service.GetAveragePurityVarianceAsync(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
            Assert.That(avg, Is.EqualTo(0));
        }
    }
}
