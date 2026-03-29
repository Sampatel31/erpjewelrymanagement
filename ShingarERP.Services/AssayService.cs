using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShingarERP.Core.Interfaces;
using ShingarERP.Core.Models;
using ShingarERP.Data.Repositories;

namespace ShingarERP.Services
{
    /// <summary>
    /// Manages assay (lab) test lifecycle including test request generation,
    /// result recording, purity/weight variance analysis, and certificate retrieval.
    /// </summary>
    public class AssayService
    {
        private readonly IUnitOfWork _uow;
        private readonly AssayTestRepository _assayRepo;
        private readonly ILogger<AssayService> _logger;

        /// <summary>Threshold above which a purity variance alert is raised.</summary>
        public const decimal PurityVarianceAlertThreshold = 0.5m;

        /// <summary>Threshold above which a weight variance alert is raised.</summary>
        public const decimal WeightVarianceAlertThreshold = 0.1m;

        /// <summary>Initialises the service with required repositories and logger.</summary>
        public AssayService(IUnitOfWork uow, AssayTestRepository assayRepo, ILogger<AssayService> logger)
        {
            _uow = uow;
            _assayRepo = assayRepo;
            _logger = logger;
        }

        /// <summary>Records an assay test result from an external lab.</summary>
        public async Task<AssayTest> RecordTestResultAsync(
            int? metalLotId, int? finishedGoodId,
            string labName, string? certificateNo,
            decimal testedPurity, decimal declaredPurity,
            decimal testedWeight, decimal declaredWeight,
            DateTime testDate, string? notes = null,
            CancellationToken ct = default)
        {
            if (testedPurity <= 0 || testedPurity > 1000)
                throw new InvalidOperationException("Tested purity must be a valid fineness value (e.g., 916 for 22K).");

            var testNo = $"AT-{testDate:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

            var test = new AssayTest
            {
                TestNo          = testNo,
                MetalLotId      = metalLotId,
                FinishedGoodId  = finishedGoodId,
                LabName         = labName,
                CertificateNo   = certificateNo,
                TestedPurity    = testedPurity,
                DeclaredPurity  = declaredPurity,
                PurityVariance  = Math.Abs(testedPurity - declaredPurity),
                TestedWeight    = testedWeight,
                DeclaredWeight  = declaredWeight,
                WeightVariance  = declaredWeight > 0
                    ? Math.Abs(testedWeight - declaredWeight) / declaredWeight * 100m
                    : 0,
                TestDate        = testDate,
                Notes           = notes,
                CreatedAt       = DateTime.UtcNow
            };

            await _uow.Repository<AssayTest>().AddAsync(test, ct);
            await _uow.SaveChangesAsync(ct);

            if (test.PurityVariance > PurityVarianceAlertThreshold)
                _logger.LogWarning("Purity variance alert: TestNo={TestNo}, Variance={Variance}", testNo, test.PurityVariance);

            if (test.WeightVariance > WeightVarianceAlertThreshold)
                _logger.LogWarning("Weight variance alert: TestNo={TestNo}, Variance={Variance}%", testNo, test.WeightVariance);

            _logger.LogInformation("Assay test {TestNo} recorded (Id={Id})", testNo, test.Id);
            return test;
        }

        /// <summary>Retrieves an assay test by certificate number.</summary>
        public async Task<AssayTest?> GetByCertificateAsync(string certificateNo, CancellationToken ct = default)
            => await _assayRepo.GetByCertificateAsync(certificateNo, ct);

        /// <summary>Returns all assay tests for a specific metal lot.</summary>
        public async Task<IEnumerable<AssayTest>> GetByMetalLotAsync(int metalLotId, CancellationToken ct = default)
            => await _assayRepo.GetByMetalLotAsync(metalLotId, ct);

        /// <summary>Returns tests with purity variance above the alert threshold in a date range.</summary>
        public async Task<IEnumerable<AssayTest>> GetHighVarianceTestsAsync(DateTime from, DateTime to, CancellationToken ct = default)
        {
            var tests = await _assayRepo.GetByDateRangeAsync(from, to, ct);
            return tests.Where(t => t.PurityVariance > PurityVarianceAlertThreshold);
        }

        /// <summary>Calculates average purity variance across a set of tests.</summary>
        public async Task<decimal> GetAveragePurityVarianceAsync(DateTime from, DateTime to, CancellationToken ct = default)
        {
            var tests = (await _assayRepo.GetByDateRangeAsync(from, to, ct)).ToList();
            if (!tests.Any()) return 0;
            return Math.Round(tests.Average(t => t.PurityVariance), 4);
        }
    }
}
