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
    public class KarigarManagementServiceTests
    {
        private ShingarContext _context = null!;
        private UnitOfWork _uow = null!;
        private KarigarRepository _karigarRepo = null!;
        private KarigarManagementService _service = null!;

        [SetUp]
        public void SetUp()
        {
            var options = new DbContextOptionsBuilder<ShingarContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            _context     = new ShingarContext(options);
            _uow         = new UnitOfWork(_context);
            _karigarRepo = new KarigarRepository(_context);
            _service     = new KarigarManagementService(_uow, _karigarRepo, NullLogger<KarigarManagementService>.Instance);
        }

        [TearDown]
        public void TearDown() { _uow.Dispose(); _context.Dispose(); }

        // ── OnboardKarigarAsync ────────────────────────────────────────────

        [Test]
        public async Task OnboardKarigarAsync_ValidInput_CreatesKarigar()
        {
            var k = await _service.OnboardKarigarAsync("Ramesh Kumar", "9000000001", null, "EMP001", 5, 500, DateTime.UtcNow);
            Assert.That(k.Id, Is.GreaterThan(0));
            Assert.That(k.Name, Is.EqualTo("Ramesh Kumar"));
            Assert.That(k.AvailabilityStatus, Is.EqualTo("Available"));
        }

        [Test]
        public async Task OnboardKarigarAsync_WithSkills_CreatesSkillMatrix()
        {
            var skills = new[] { ("Casting", 4), ("Finishing", 3) };
            var k = await _service.OnboardKarigarAsync("Suresh", null, null, null, 3, 400, DateTime.UtcNow,
                skills.Select(s => (s.Item1, s.Item2)));
            var saved = await _karigarRepo.GetWithDetailsAsync(k.Id);
            Assert.That(saved!.Skills.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task OnboardKarigarAsync_EmptyName_ThrowsException()
        {
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.OnboardKarigarAsync("", null, null, null, 0, 0, DateTime.UtcNow));
        }

        [Test]
        public async Task OnboardKarigarAsync_IsActiveTrueByDefault()
        {
            var k = await _service.OnboardKarigarAsync("Test", null, null, null, 1, 300, DateTime.UtcNow);
            Assert.That(k.IsActive, Is.True);
        }

        // ── RecordPerformanceAsync ─────────────────────────────────────────

        [Test]
        public async Task RecordPerformanceAsync_ValidInput_CreatesRecord()
        {
            var k = await _service.OnboardKarigarAsync("Karigar1", null, null, null, 2, 400, DateTime.UtcNow);
            var perf = await _service.RecordPerformanceAsync(k.Id, 2024, 1, 30, 85, 90, 2, 12000);
            Assert.That(perf.Id, Is.GreaterThan(0));
            Assert.That(perf.ItemsProduced, Is.EqualTo(30));
        }

        [Test]
        public async Task RecordPerformanceAsync_UpdatesKarigarRating()
        {
            var k = await _service.OnboardKarigarAsync("Karigar2", null, null, null, 2, 400, DateTime.UtcNow);
            await _service.RecordPerformanceAsync(k.Id, 2024, 1, 30, 100, 90, 2, 12000);
            var updated = await _karigarRepo.GetByIdAsync(k.Id);
            Assert.That(updated!.PerformanceRating, Is.EqualTo(5m));
        }

        [Test]
        public async Task RecordPerformanceAsync_InvalidQualityScore_ThrowsException()
        {
            var k = await _service.OnboardKarigarAsync("Karigar3", null, null, null, 2, 400, DateTime.UtcNow);
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.RecordPerformanceAsync(k.Id, 2024, 1, 10, 110, 90, 2, 5000));
        }

        [Test]
        public async Task RecordPerformanceAsync_InvalidOnTimePercent_ThrowsException()
        {
            var k = await _service.OnboardKarigarAsync("Karigar4", null, null, null, 2, 400, DateTime.UtcNow);
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.RecordPerformanceAsync(k.Id, 2024, 1, 10, 80, 120, 2, 5000));
        }

        // ── UpdateAvailabilityAsync ─────────────────────────────────────────

        [Test]
        public async Task UpdateAvailabilityAsync_Valid_UpdatesStatus()
        {
            var k = await _service.OnboardKarigarAsync("Karigar5", null, null, null, 2, 400, DateTime.UtcNow);
            await _service.UpdateAvailabilityAsync(k.Id, "Busy");
            var updated = await _karigarRepo.GetByIdAsync(k.Id);
            Assert.That(updated!.AvailabilityStatus, Is.EqualTo("Busy"));
        }

        [Test]
        public async Task UpdateAvailabilityAsync_InvalidStatus_ThrowsException()
        {
            var k = await _service.OnboardKarigarAsync("Karigar6", null, null, null, 2, 400, DateTime.UtcNow);
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.UpdateAvailabilityAsync(k.Id, "Unknown"));
        }

        // ── CertifySkillAsync ──────────────────────────────────────────────

        [Test]
        public async Task CertifySkillAsync_ValidInput_AddsSkill()
        {
            var k = await _service.OnboardKarigarAsync("Karigar7", null, null, null, 2, 400, DateTime.UtcNow);
            var skill = await _service.CertifySkillAsync(k.Id, "Polishing", 4);
            Assert.That(skill.Id, Is.GreaterThan(0));
            Assert.That(skill.ProficiencyLevel, Is.EqualTo(4));
        }

        [Test]
        public async Task CertifySkillAsync_ProficiencyOutOfRange_ThrowsException()
        {
            var k = await _service.OnboardKarigarAsync("Karigar8", null, null, null, 2, 400, DateTime.UtcNow);
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CertifySkillAsync(k.Id, "Polishing", 6));
        }

        // ── GetAvailableBySkillAsync ───────────────────────────────────────

        [Test]
        public async Task GetAvailableBySkillAsync_ReturnsOnlyAvailable()
        {
            var k1 = await _service.OnboardKarigarAsync("K1", null, null, null, 2, 400, DateTime.UtcNow);
            var k2 = await _service.OnboardKarigarAsync("K2", null, null, null, 2, 400, DateTime.UtcNow);
            await _service.CertifySkillAsync(k1.Id, "Casting", 3);
            await _service.CertifySkillAsync(k2.Id, "Casting", 3);
            await _service.UpdateAvailabilityAsync(k2.Id, "Busy");

            var available = await _service.GetAvailableBySkillAsync("Casting");
            Assert.That(available.Select(k => k.Id), Does.Contain(k1.Id));
            Assert.That(available.Select(k => k.Id), Does.Not.Contain(k2.Id));
        }

        // ── CalculateIncentive ─────────────────────────────────────────────

        [Test]
        public void CalculateIncentive_QualityAbove90_Returns10Percent()
        {
            var incentive = _service.CalculateIncentive(500, 92, 26);
            Assert.That(incentive, Is.EqualTo(1300)); // 500*26*0.10
        }

        [Test]
        public void CalculateIncentive_QualityBetween75And90_Returns5Percent()
        {
            var incentive = _service.CalculateIncentive(500, 80, 26);
            Assert.That(incentive, Is.EqualTo(650)); // 500*26*0.05
        }

        [Test]
        public void CalculateIncentive_QualityBelow75_ReturnsZero()
        {
            var incentive = _service.CalculateIncentive(500, 70, 26);
            Assert.That(incentive, Is.EqualTo(0));
        }

        // ── GetTopPerformersAsync ─────────────────────────────────────────

        [Test]
        public async Task GetTopPerformersAsync_ReturnsRequestedCount()
        {
            for (int i = 0; i < 5; i++)
                await _service.OnboardKarigarAsync($"K{i}", null, null, null, 1, 300, DateTime.UtcNow);
            var top = await _service.GetTopPerformersAsync(3);
            Assert.That(top.Count(), Is.LessThanOrEqualTo(3));
        }
    }
}
