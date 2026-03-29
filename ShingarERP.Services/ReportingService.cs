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
    /// Generates business reports including sales, purchase, financial, customer, and KPI dashboards.
    /// </summary>
    public class ReportingService
    {
        private readonly IUnitOfWork _uow;
        private readonly SalesOrderRepository _salesRepo;
        private readonly PurchaseOrderRepository _poRepo;
        private readonly ReportRepository _reportRepo;
        private readonly ILogger<ReportingService> _logger;

        /// <summary>Initialises the service with required repositories and logger.</summary>
        public ReportingService(
            IUnitOfWork uow,
            SalesOrderRepository salesRepo,
            PurchaseOrderRepository poRepo,
            ReportRepository reportRepo,
            ILogger<ReportingService> logger)
        {
            _uow = uow;
            _salesRepo = salesRepo;
            _poRepo = poRepo;
            _reportRepo = reportRepo;
            _logger = logger;
        }

        /// <summary>Returns a sales summary for the given date range.</summary>
        public async Task<(IEnumerable<SalesOrder> Orders, decimal TotalValue, int TotalOrders, decimal AverageOrderValue)> GetSalesReportAsync(DateTime from, DateTime to, CancellationToken ct = default)
        {
            var orders = (await _salesRepo.GetByDateRangeAsync(from, to, ct)).ToList();
            var total  = orders.Sum(o => o.NetAmount);
            var count  = orders.Count;
            var avg    = count > 0 ? total / count : 0;
            return (orders, total, count, avg);
        }

        /// <summary>Returns a purchase summary for the given date range.</summary>
        public async Task<(IEnumerable<PurchaseOrder> POs, decimal TotalValue, int TotalPOs)> GetPurchaseReportAsync(DateTime from, DateTime to, CancellationToken ct = default)
        {
            var pos    = (await _poRepo.GetByDateRangeAsync(from, to, ct)).ToList();
            var total  = pos.Sum(p => p.NetAmount);
            return (pos, total, pos.Count);
        }

        /// <summary>Returns order performance metrics for a specific customer within a date range.</summary>
        public async Task<(IEnumerable<SalesOrder> Orders, decimal TotalSpend, int OrderCount, decimal AverageOrderValue)> GetCustomerPerformanceAsync(int customerId, DateTime from, DateTime to, CancellationToken ct = default)
        {
            var orders = (await _salesRepo.GetByDateRangeAsync(from, to, ct))
                .Where(o => o.CustomerId == customerId)
                .ToList();

            var spend = orders.Sum(o => o.NetAmount);
            var count = orders.Count;
            var avg   = count > 0 ? spend / count : 0;
            return (orders, spend, count, avg);
        }

        /// <summary>Creates a new report template.</summary>
        public async Task<ReportTemplate> CreateReportTemplateAsync(string name, string type, string queryDef, string columnDefs, CancellationToken ct = default)
        {
            var template = new ReportTemplate
            {
                TemplateName      = name,
                ReportType        = type,
                QueryDefinition   = queryDef,
                ColumnDefinitions = columnDefs,
                IsActive          = true,
                CreatedAt         = DateTime.UtcNow,
                UpdatedAt         = DateTime.UtcNow
            };

            await _uow.Repository<ReportTemplate>().AddAsync(template, ct);
            await _uow.SaveChangesAsync(ct);
            return template;
        }

        /// <summary>Returns active report templates, optionally filtered by type.</summary>
        public async Task<IEnumerable<ReportTemplate>> GetReportTemplatesAsync(string? type = null, CancellationToken ct = default)
        {
            if (type == null)
                return await _reportRepo.GetActiveTemplatesAsync(ct);

            return await _reportRepo.GetByTypeAsync(type, ct);
        }

        /// <summary>Creates a report schedule for a template.</summary>
        public async Task<ReportSchedule> ScheduleReportAsync(int templateId, string scheduleName, string frequency, string recipients, CancellationToken ct = default)
        {
            var nextRun = frequency switch
            {
                "Daily"     => DateTime.UtcNow.AddDays(1),
                "Weekly"    => DateTime.UtcNow.AddDays(7),
                "Monthly"   => DateTime.UtcNow.AddMonths(1),
                "Quarterly" => DateTime.UtcNow.AddMonths(3),
                "Yearly"    => DateTime.UtcNow.AddYears(1),
                _           => DateTime.UtcNow.AddDays(1)
            };

            var schedule = new ReportSchedule
            {
                TemplateId    = templateId,
                ScheduleName  = scheduleName,
                Frequency     = frequency,
                NextRunDate   = nextRun,
                Recipients    = recipients,
                IsActive      = true,
                CreatedAt     = DateTime.UtcNow,
                UpdatedAt     = DateTime.UtcNow
            };

            await _uow.Repository<ReportSchedule>().AddAsync(schedule, ct);
            await _uow.SaveChangesAsync(ct);
            return schedule;
        }

        /// <summary>Creates a new KPI definition.</summary>
        public async Task<KPI> CreateKPIAsync(string code, string name, string category, decimal targetValue, string unit, CancellationToken ct = default)
        {
            if (await _uow.Repository<KPI>().AnyAsync(k => k.KPICode == code, ct))
                throw new InvalidOperationException($"A KPI with code '{code}' already exists.");

            var kpi = new KPI
            {
                KPICode       = code,
                KPIName       = name,
                Category      = category,
                TargetValue   = targetValue,
                CurrentValue  = 0,
                Unit          = unit,
                AsOfDate      = DateTime.UtcNow,
                UpdatedAt     = DateTime.UtcNow
            };

            await _uow.Repository<KPI>().AddAsync(kpi, ct);
            await _uow.SaveChangesAsync(ct);
            return kpi;
        }

        /// <summary>Updates the current and previous values for a KPI.</summary>
        public async Task<KPI> UpdateKPIValueAsync(int kpiId, decimal currentValue, decimal? previousValue, CancellationToken ct = default)
        {
            var kpi = await _uow.Repository<KPI>().GetByIdAsync(kpiId, ct)
                ?? throw new InvalidOperationException($"KPI {kpiId} not found.");

            kpi.PreviousValue = previousValue ?? kpi.CurrentValue;
            kpi.CurrentValue  = currentValue;
            kpi.AsOfDate      = DateTime.UtcNow;
            kpi.UpdatedAt     = DateTime.UtcNow;

            _uow.Repository<KPI>().Update(kpi);
            await _uow.SaveChangesAsync(ct);
            return kpi;
        }

        /// <summary>Returns all KPIs in a specific category.</summary>
        public async Task<IEnumerable<KPI>> GetKPIsByCategory(string category, CancellationToken ct = default)
            => await _uow.Repository<KPI>().FindAsync(k => k.Category == category, ct);

        /// <summary>Creates a new dashboard widget.</summary>
        public async Task<DashboardWidget> CreateDashboardWidgetAsync(string name, string widgetType, string dataSource, string config, CancellationToken ct = default)
        {
            var widget = new DashboardWidget
            {
                WidgetName    = name,
                WidgetType    = widgetType,
                DataSource    = dataSource,
                Configuration = config,
                IsActive      = true,
                CreatedAt     = DateTime.UtcNow,
                UpdatedAt     = DateTime.UtcNow
            };

            await _uow.Repository<DashboardWidget>().AddAsync(widget, ct);
            await _uow.SaveChangesAsync(ct);
            return widget;
        }

        /// <summary>Returns all active dashboard widgets sorted by display order.</summary>
        public async Task<IEnumerable<DashboardWidget>> GetActiveDashboardWidgetsAsync(CancellationToken ct = default)
        {
            var widgets = await _uow.Repository<DashboardWidget>().FindAsync(w => w.IsActive, ct);
            return widgets.OrderBy(w => w.SortOrder);
        }

        /// <summary>Returns a financial summary including revenue, expenses, net profit, and gross margin.</summary>
        public async Task<(decimal TotalRevenue, decimal TotalExpenses, decimal NetProfit, decimal GrossMargin)> GetFinancialSummaryAsync(DateTime from, DateTime to, CancellationToken ct = default)
        {
            var orders = (await _salesRepo.GetByDateRangeAsync(from, to, ct))
                .Where(o => o.Status != "Cancelled")
                .ToList();

            var pos = (await _poRepo.GetByDateRangeAsync(from, to, ct))
                .Where(p => p.Status != "Cancelled")
                .ToList();

            var revenue  = orders.Sum(o => o.NetAmount);
            var expenses = pos.Sum(p => p.NetAmount);
            var profit   = revenue - expenses;
            var margin   = revenue > 0 ? profit / revenue * 100 : 0;

            return (revenue, expenses, profit, margin);
        }
    }
}
