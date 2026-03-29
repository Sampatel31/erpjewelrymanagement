using System;
using System.Collections.Generic;
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
    public class MeltingServiceTests
    {
        private ShingarContext _context = null!;
        private UnitOfWork _uow = null!;
        private MeltingBatchRepository _batchRepo = null!;
        private MeltingService _service = null!;

        private static readonly List<(int? MetalLotId, string? LotNumber, decimal WeightUsed)> DefaultInputs
            = new() { (null, "LOT001", 100m) };

        private static readonly List<(string MetalType, string? Purity, decimal Proportion)> DefaultCompositions
            = new() { ("Gold", "22K", 75m), ("Silver", null, 25m) };

        [SetUp]
        public void SetUp()
        {
            var options = new DbContextOptionsBuilder<ShingarContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            _context   = new ShingarContext(options);
            _uow       = new UnitOfWork(_context);
            _batchRepo = new MeltingBatchRepository(_context);
            _service   = new MeltingService(_uow, _batchRepo, NullLogger<MeltingService>.Instance);
        }

        [TearDown]
        public void TearDown() { _uow.Dispose(); _context.Dispose(); }

        // ── CreateBatchAsync ─────────────────────────────────────────────

        [Test]
        public async Task CreateBatchAsync_ValidInput_CreatesBatch()
        {
            var batch = await _service.CreateBatchAsync("Gold", 100m, DefaultInputs, DefaultCompositions);
            Assert.That(batch.Id, Is.GreaterThan(0));
            Assert.That(batch.Status, Is.EqualTo("Pending"));
            Assert.That(batch.BatchNo, Does.StartWith("MB-"));
        }

        [Test]
        public async Task CreateBatchAsync_CompositionNot100_ThrowsException()
        {
            var badComps = new List<(string, string?, decimal)> { ("Gold", null, 60m), ("Silver", null, 30m) }; // 90%
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CreateBatchAsync("Gold", 100m, DefaultInputs, badComps));
        }

        [Test]
        public async Task CreateBatchAsync_WithInputLots_SavesInputs()
        {
            var batch = await _service.CreateBatchAsync("Gold", 100m, DefaultInputs, DefaultCompositions);
            var saved = await _batchRepo.GetWithDetailsAsync(batch.Id);
            Assert.That(saved!.Inputs.Count, Is.EqualTo(1));
            Assert.That(saved.Inputs.First().LotNumber, Is.EqualTo("LOT001"));
        }

        [Test]
        public async Task CreateBatchAsync_CompositionExactly100_Succeeds()
        {
            var comps = new List<(string, string?, decimal)> { ("Gold", null, 100m) };
            var batch = await _service.CreateBatchAsync("Gold", 100m, DefaultInputs, comps);
            Assert.That(batch.Id, Is.GreaterThan(0));
        }

        // ── AdvanceBatchStatusAsync ──────────────────────────────────────

        [Test]
        public async Task AdvanceBatchStatusAsync_PendingToMelting_Succeeds()
        {
            var batch = await _service.CreateBatchAsync("Gold", 100m, DefaultInputs, DefaultCompositions);
            var updated = await _service.AdvanceBatchStatusAsync(batch.Id, "Melting");
            Assert.That(updated.Status, Is.EqualTo("Melting"));
        }

        [Test]
        public async Task AdvanceBatchStatusAsync_InvalidTransition_ThrowsException()
        {
            var batch = await _service.CreateBatchAsync("Gold", 100m, DefaultInputs, DefaultCompositions);
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.AdvanceBatchStatusAsync(batch.Id, "Distributed"));
        }

        [Test]
        public async Task AdvanceBatchStatusAsync_PendingToComplete_InvalidTransition()
        {
            var batch = await _service.CreateBatchAsync("Gold", 100m, DefaultInputs, DefaultCompositions);
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.AdvanceBatchStatusAsync(batch.Id, "Complete"));
        }

        [Test]
        public async Task AdvanceBatchStatusAsync_SetsCompletedAtOnComplete()
        {
            var batch = await _service.CreateBatchAsync("Gold", 100m, DefaultInputs, DefaultCompositions);
            await _service.AdvanceBatchStatusAsync(batch.Id, "Melting");
            var completed = await _service.AdvanceBatchStatusAsync(batch.Id, "Complete");
            Assert.That(completed.CompletedAt, Is.Not.Null);
        }

        [Test]
        public async Task AdvanceBatchStatusAsync_NonExistentBatch_ThrowsException()
        {
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.AdvanceBatchStatusAsync(99999, "Melting"));
        }

        // ── RecordNetWeightAsync ──────────────────────────────────────────

        [Test]
        public async Task RecordNetWeightAsync_ValidWeight_CalculatesLoss()
        {
            var batch = await _service.CreateBatchAsync("Gold", 100m, DefaultInputs, DefaultCompositions);
            var updated = await _service.RecordNetWeightAsync(batch.Id, 97.5m);
            Assert.That(updated.NetWeight, Is.EqualTo(97.5m));
            Assert.That(updated.MeltingLossPercent, Is.EqualTo(2.5m));
        }

        [Test]
        public async Task RecordNetWeightAsync_ZeroWeight_ThrowsException()
        {
            var batch = await _service.CreateBatchAsync("Gold", 100m, DefaultInputs, DefaultCompositions);
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.RecordNetWeightAsync(batch.Id, 0));
        }

        [Test]
        public async Task RecordNetWeightAsync_WeightExceedsGross_ThrowsException()
        {
            var batch = await _service.CreateBatchAsync("Gold", 100m, DefaultInputs, DefaultCompositions);
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.RecordNetWeightAsync(batch.Id, 110m));
        }

        // ── ValidateCompositionAsync ────────────────────────────────────────

        [Test]
        public async Task ValidateCompositionAsync_SumsTo100_ReturnsTrue()
        {
            var batch = await _service.CreateBatchAsync("Gold", 100m, DefaultInputs, DefaultCompositions);
            var valid = await _service.ValidateCompositionAsync(batch.Id);
            Assert.That(valid, Is.True);
        }
    }
}
