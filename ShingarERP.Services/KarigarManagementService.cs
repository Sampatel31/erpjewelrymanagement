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
    /// Manages karigar (craftsman) onboarding, skill certification, performance tracking,
    /// and skill-based job assignment recommendations.
    /// </summary>
    public class KarigarManagementService
    {
        private readonly IUnitOfWork _uow;
        private readonly KarigarRepository _karigarRepo;
        private readonly ILogger<KarigarManagementService> _logger;

        /// <summary>Initialises the service with required repositories and logger.</summary>
        public KarigarManagementService(IUnitOfWork uow, KarigarRepository karigarRepo, ILogger<KarigarManagementService> logger)
        {
            _uow = uow;
            _karigarRepo = karigarRepo;
            _logger = logger;
        }

        /// <summary>Onboards a new karigar and optionally certifies initial skills.</summary>
        public async Task<Karigar> OnboardKarigarAsync(
            string name, string? mobile, string? address, string? employeeCode,
            int experienceYears, decimal dailyRate, DateTime joiningDate,
            IEnumerable<(string SkillName, int ProficiencyLevel)>? skills = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Karigar name is required.");

            var karigar = new Karigar
            {
                Name               = name,
                Mobile             = mobile,
                Address            = address,
                EmployeeCode       = employeeCode,
                ExperienceYears    = experienceYears,
                DailyRate          = dailyRate,
                JoiningDate        = joiningDate,
                AvailabilityStatus = "Available",
                IsActive           = true,
                CreatedAt          = DateTime.UtcNow,
                UpdatedAt          = DateTime.UtcNow
            };

            if (skills != null)
            {
                foreach (var (skillName, proficiency) in skills)
                {
                    karigar.Skills.Add(new KarigarSkill
                    {
                        SkillName        = skillName,
                        ProficiencyLevel = proficiency,
                        CertifiedOn      = DateTime.UtcNow
                    });
                }
            }

            await _uow.Repository<Karigar>().AddAsync(karigar, ct);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("Karigar '{Name}' onboarded (Id={Id})", name, karigar.Id);
            return karigar;
        }

        /// <summary>Records monthly performance data for a karigar.</summary>
        public async Task<KarigarPerformance> RecordPerformanceAsync(
            int karigarId, int year, int month,
            int itemsProduced, decimal qualityScore, decimal onTimePercent,
            decimal wastePercent, decimal totalLaborCost, string? remarks = null,
            CancellationToken ct = default)
        {
            if (qualityScore < 0 || qualityScore > 100)
                throw new InvalidOperationException("Quality score must be between 0 and 100.");
            if (onTimePercent < 0 || onTimePercent > 100)
                throw new InvalidOperationException("On-time percent must be between 0 and 100.");

            var performance = new KarigarPerformance
            {
                KarigarId      = karigarId,
                Year           = year,
                Month          = month,
                ItemsProduced  = itemsProduced,
                QualityScore   = qualityScore,
                OnTimePercent  = onTimePercent,
                WastePercent   = wastePercent,
                TotalLaborCost = totalLaborCost,
                Remarks        = remarks,
                RecordedAt     = DateTime.UtcNow
            };

            await _uow.Repository<KarigarPerformance>().AddAsync(performance, ct);

            // Update karigar's performance rating (rolling average of quality score / 20)
            var karigar = await _karigarRepo.GetByIdAsync(karigarId, ct)
                ?? throw new InvalidOperationException($"Karigar {karigarId} not found.");
            karigar.PerformanceRating = Math.Round(qualityScore / 20m, 2); // 0-100 → 0-5
            karigar.UpdatedAt         = DateTime.UtcNow;
            _uow.Repository<Karigar>().Update(karigar);

            await _uow.SaveChangesAsync(ct);
            return performance;
        }

        /// <summary>Updates a karigar's availability status.</summary>
        public async Task UpdateAvailabilityAsync(int karigarId, string status, CancellationToken ct = default)
        {
            var validStatuses = new[] { "Available", "Busy", "OnLeave", "Inactive" };
            if (!validStatuses.Contains(status))
                throw new InvalidOperationException($"Invalid availability status '{status}'.");

            var karigar = await _karigarRepo.GetByIdAsync(karigarId, ct)
                ?? throw new InvalidOperationException($"Karigar {karigarId} not found.");
            karigar.AvailabilityStatus = status;
            karigar.UpdatedAt          = DateTime.UtcNow;
            _uow.Repository<Karigar>().Update(karigar);
            await _uow.SaveChangesAsync(ct);
        }

        /// <summary>Adds a new skill certification to a karigar.</summary>
        public async Task<KarigarSkill> CertifySkillAsync(int karigarId, string skillName, int proficiencyLevel, string? notes = null, CancellationToken ct = default)
        {
            if (proficiencyLevel < 1 || proficiencyLevel > 5)
                throw new InvalidOperationException("Proficiency level must be between 1 and 5.");

            var skill = new KarigarSkill
            {
                KarigarId        = karigarId,
                SkillName        = skillName,
                ProficiencyLevel = proficiencyLevel,
                Notes            = notes,
                CertifiedOn      = DateTime.UtcNow
            };

            await _uow.Repository<KarigarSkill>().AddAsync(skill, ct);
            await _uow.SaveChangesAsync(ct);
            return skill;
        }

        /// <summary>Returns karigars who have a specific skill and are currently available.</summary>
        public async Task<IEnumerable<Karigar>> GetAvailableBySkillAsync(string skillName, CancellationToken ct = default)
        {
            var bySkill = await _karigarRepo.GetBySkillAsync(skillName, ct);
            return bySkill.Where(k => k.AvailabilityStatus == "Available");
        }

        /// <summary>Returns top-performing karigars by performance rating.</summary>
        public async Task<IEnumerable<Karigar>> GetTopPerformersAsync(int top = 10, CancellationToken ct = default)
            => await _karigarRepo.GetTopPerformersAsync(top, ct);

        /// <summary>Calculates incentive amount for a karigar based on quality score.</summary>
        public decimal CalculateIncentive(decimal dailyRate, decimal qualityScore, int workingDays)
        {
            // Incentive = 10% of monthly wage if quality ≥ 90, 5% if ≥ 75, else 0
            var monthlyWage = dailyRate * workingDays;
            if (qualityScore >= 90) return Math.Round(monthlyWage * 0.10m, 2);
            if (qualityScore >= 75) return Math.Round(monthlyWage * 0.05m, 2);
            return 0;
        }
    }
}
