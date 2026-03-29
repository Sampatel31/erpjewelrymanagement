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
    public class QualityControlServiceTests
    {
        private ShingarContext _context = null!;
        private UnitOfWork _uow = null!;
        private QCRecordRepository _qcRepo = null!;
        private QualityControlService _service = null!;

        [SetUp]
        public void SetUp()
        {
            var options = new DbContextOptionsBuilder<ShingarContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            _context = new ShingarContext(options);
            _uow     = new UnitOfWork(_context);
            _qcRepo  = new QCRecordRepository(_context);
            _service = new QualityControlService(_uow, _qcRepo, NullLogger<QualityControlService>.Instance);
        }

        [TearDown]
        public void TearDown() { _uow.Dispose(); _context.Dispose(); }

        // ── CreateInspectionAsync ────────────────────────────────────────

        [Test]
        public async Task CreateInspectionAsync_ValidInput_CreatesRecord()
        {
            var rec = await _service.CreateInspectionAsync(1, null, 1, null, 85);
            Assert.That(rec.Id, Is.GreaterThan(0));
            Assert.That(rec.Result, Is.EqualTo("Pending"));
            Assert.That(rec.QCNo, Does.StartWith("QC-"));
        }

        [Test]
        public async Task CreateInspectionAsync_InvalidScore_ThrowsException()
        {
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CreateInspectionAsync(1, null, 1, null, 110));
        }

        [Test]
        public async Task CreateInspectionAsync_ZeroScore_Succeeds()
        {
            var rec = await _service.CreateInspectionAsync(1, null, 1, null, 0);
            Assert.That(rec.QualityScore, Is.EqualTo(0));
        }

        [Test]
        public async Task CreateInspectionAsync_PerfectScore_Succeeds()
        {
            var rec = await _service.CreateInspectionAsync(1, null, 1, null, 100);
            Assert.That(rec.QualityScore, Is.EqualTo(100));
        }

        // ── RecordDefectAsync ───────────────────────────────────────────

        [Test]
        public async Task RecordDefectAsync_ValidInput_CreatesDefect()
        {
            var rec = await _service.CreateInspectionAsync(1, null, 1, null, 70);
            var defect = await _service.RecordDefectAsync(rec.Id, "D001", "Surface scratch", "Minor");
            Assert.That(defect.Id, Is.GreaterThan(0));
            Assert.That(defect.Severity, Is.EqualTo("Minor"));
        }

        [Test]
        public async Task RecordDefectAsync_InvalidSeverity_ThrowsException()
        {
            var rec = await _service.CreateInspectionAsync(1, null, 1, null, 70);
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.RecordDefectAsync(rec.Id, "D001", "Scratch", "Severe"));
        }

        [Test]
        public async Task RecordDefectAsync_CriticalSeverity_Succeeds()
        {
            var rec = await _service.CreateInspectionAsync(1, null, 1, null, 40);
            var defect = await _service.RecordDefectAsync(rec.Id, "D002", "Crack", "Critical", 500);
            Assert.That(defect.Severity, Is.EqualTo("Critical"));
            Assert.That(defect.RemedyCost, Is.EqualTo(500));
        }

        // ── RecordDecisionAsync ─────────────────────────────────────────

        [Test]
        public async Task RecordDecisionAsync_Pass_UpdatesResult()
        {
            var rec = await _service.CreateInspectionAsync(1, null, 1, null, 90);
            var updated = await _service.RecordDecisionAsync(rec.Id, "Pass");
            Assert.That(updated.Result, Is.EqualTo("Pass"));
        }

        [Test]
        public async Task RecordDecisionAsync_Fail_UpdatesResult()
        {
            var rec = await _service.CreateInspectionAsync(1, null, 1, null, 40);
            var updated = await _service.RecordDecisionAsync(rec.Id, "Fail");
            Assert.That(updated.Result, Is.EqualTo("Fail"));
        }

        [Test]
        public async Task RecordDecisionAsync_PassWithUnresolvedCriticalDefect_ThrowsException()
        {
            var rec = await _service.CreateInspectionAsync(1, null, 1, null, 40);
            await _service.RecordDefectAsync(rec.Id, "D001", "Crack", "Critical");
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.RecordDecisionAsync(rec.Id, "Pass"));
        }

        [Test]
        public async Task RecordDecisionAsync_InvalidResult_ThrowsException()
        {
            var rec = await _service.CreateInspectionAsync(1, null, 1, null, 80);
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.RecordDecisionAsync(rec.Id, "Unknown"));
        }

        [Test]
        public async Task RecordDecisionAsync_Conditional_Succeeds()
        {
            var rec = await _service.CreateInspectionAsync(1, null, 1, null, 70);
            var updated = await _service.RecordDecisionAsync(rec.Id, "Conditional", "Minor touch-up required");
            Assert.That(updated.Result, Is.EqualTo("Conditional"));
        }

        [Test]
        public async Task RecordDecisionAsync_PassWithOnlyMajorDefect_Succeeds()
        {
            var rec = await _service.CreateInspectionAsync(1, null, 1, null, 80);
            await _service.RecordDefectAsync(rec.Id, "D001", "Minor polish", "Major");
            // Major defect does not block pass (only Critical does)
            var updated = await _service.RecordDecisionAsync(rec.Id, "Pass");
            Assert.That(updated.Result, Is.EqualTo("Pass"));
        }

        // ── GetPassRateAsync ────────────────────────────────────────────

        [Test]
        public async Task GetPassRateAsync_50PercentPass_Returns50()
        {
            var from = DateTime.UtcNow.AddDays(-1);
            var r1 = await _service.CreateInspectionAsync(1, null, 1, null, 90);
            await _service.RecordDecisionAsync(r1.Id, "Pass");
            var r2 = await _service.CreateInspectionAsync(2, null, 1, null, 40);
            await _service.RecordDecisionAsync(r2.Id, "Fail");
            var rate = await _service.GetPassRateAsync(from, DateTime.UtcNow.AddDays(1));
            Assert.That(rate, Is.EqualTo(50m));
        }

        [Test]
        public async Task GetPassRateAsync_NoRecords_ReturnsZero()
        {
            var rate = await _service.GetPassRateAsync(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
            Assert.That(rate, Is.EqualTo(0));
        }

        // ── GetJobCardInspectionsAsync ─────────────────────────────────

        [Test]
        public async Task GetJobCardInspectionsAsync_ReturnsCorrectRecords()
        {
            await _service.CreateInspectionAsync(1, null, 1, null, 85);
            await _service.CreateInspectionAsync(2, null, 1, null, 75);
            var records = await _service.GetJobCardInspectionsAsync(1);
            Assert.That(records.All(r => r.JobCardId == 1), Is.True);
        }

        // ── GetFailedInspectionsAsync ──────────────────────────────────

        [Test]
        public async Task GetFailedInspectionsAsync_ReturnsOnlyFailed()
        {
            var r1 = await _service.CreateInspectionAsync(1, null, 1, null, 40);
            await _service.RecordDecisionAsync(r1.Id, "Fail");
            var r2 = await _service.CreateInspectionAsync(2, null, 1, null, 90);
            await _service.RecordDecisionAsync(r2.Id, "Pass");
            var failed = await _service.GetFailedInspectionsAsync();
            Assert.That(failed.All(r => r.Result == "Fail"), Is.True);
        }
    }
}
