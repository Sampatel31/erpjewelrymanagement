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
    public class AuditTrailServiceTests
    {
        private ShingarContext _context = null!;
        private UnitOfWork _uow = null!;
        private AuditLogRepository _auditRepo = null!;
        private AuditTrailService _service = null!;

        [SetUp]
        public void SetUp()
        {
            var options = new DbContextOptionsBuilder<ShingarContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _context   = new ShingarContext(options);
            _uow       = new UnitOfWork(_context);
            _auditRepo = new AuditLogRepository(_context);
            _service   = new AuditTrailService(_uow, _auditRepo, NullLogger<AuditTrailService>.Instance);
        }

        [TearDown]
        public void TearDown()
        {
            _uow.Dispose();
            _context.Dispose();
        }

        [Test]
        public async Task LogCreate_Valid()
        {
            var log = await _service.LogCreateAsync("Customer", "1", "{\"name\":\"Ramesh\"}", 1, "admin", "CRM");
            Assert.That(log.OperationType, Is.EqualTo("Create"));
            Assert.That(log.EntityName, Is.EqualTo("Customer"));
            Assert.That(log.NewValues, Is.EqualTo("{\"name\":\"Ramesh\"}"));
            Assert.That(log.OldValues, Is.Null);
        }

        [Test]
        public async Task LogUpdate_Valid()
        {
            var log = await _service.LogUpdateAsync("Customer", "1", "{\"name\":\"Old\"}", "{\"name\":\"New\"}", 1, "admin", "CRM");
            Assert.That(log.OperationType, Is.EqualTo("Update"));
            Assert.That(log.OldValues, Is.EqualTo("{\"name\":\"Old\"}"));
            Assert.That(log.NewValues, Is.EqualTo("{\"name\":\"New\"}"));
        }

        [Test]
        public async Task LogDelete_Valid()
        {
            var log = await _service.LogDeleteAsync("Customer", "1", "{\"name\":\"Ramesh\"}", 1, "admin", "CRM");
            Assert.That(log.OperationType, Is.EqualTo("Delete"));
            Assert.That(log.OldValues, Is.Not.Null);
            Assert.That(log.NewValues, Is.Null);
        }

        [Test]
        public async Task LogAsync_AllFields()
        {
            var log = await _service.LogAsync("Invoice", "42", "Update", "old", "new", 5, "staff", "192.168.1.1", "Billing");
            Assert.That(log.EntityId, Is.EqualTo("42"));
            Assert.That(log.UserId, Is.EqualTo(5));
            Assert.That(log.IpAddress, Is.EqualTo("192.168.1.1"));
            Assert.That(log.Module, Is.EqualTo("Billing"));
        }

        [Test]
        public async Task GetEntityHistory_MultipleOps()
        {
            await _service.LogCreateAsync("Order", "10", null, 1, null, "Sales");
            await _service.LogUpdateAsync("Order", "10", "Draft", "Submitted", 1, null, "Sales");
            var history = (await _service.GetEntityHistoryAsync("Order", "10")).ToList();
            Assert.That(history.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task GetUserActivity_DateRange()
        {
            await _service.LogCreateAsync("Product", "5", null, 3, "staff3", "Inventory");
            await _service.LogUpdateAsync("Product", "5", null, null, 3, "staff3", "Inventory");
            var from = DateTime.UtcNow.AddMinutes(-5);
            var to   = DateTime.UtcNow.AddMinutes(5);
            var logs = (await _service.GetUserActivityAsync(3, from, to)).ToList();
            Assert.That(logs.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task GetAuditReport_ByModule()
        {
            await _service.LogCreateAsync("Account", "1", null, 1, null, "Accounting");
            await _service.LogCreateAsync("Customer", "1", null, 1, null, "CRM");
            var report = (await _service.GetAuditReportAsync(DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1), "Accounting")).ToList();
            Assert.That(report.All(r => r.Module == "Accounting"), Is.True);
        }

        [Test]
        public async Task VerifyDataIntegrity_Valid()
        {
            await _service.LogCreateAsync("SalesOrder", "100", null, 1, null, "Sales");
            var result = await _service.VerifyDataIntegrityAsync("SalesOrder", DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1));
            Assert.That(result, Is.True);
        }

        [Test]
        public async Task GetComplianceReport_Valid()
        {
            await _service.LogCreateAsync("JournalEntry", "200", null, 1, null, "Accounting");
            await _service.LogUpdateAsync("JournalEntry", "200", null, null, 1, null, "Accounting");
            var report = (await _service.GetComplianceReportAsync("Financial", DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1))).ToList();
            Assert.That(report.Count, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public async Task GetModuleActivity_Valid()
        {
            await _service.LogCreateAsync("Item", "1", null, 2, null, "Inventory");
            var from = DateTime.UtcNow.AddMinutes(-5);
            var to   = DateTime.UtcNow.AddMinutes(5);
            var logs = (await _service.GetModuleActivityAsync("Inventory", from, to)).ToList();
            Assert.That(logs.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task LogCreate_NullOptionals()
        {
            var log = await _service.LogCreateAsync("Entity", "1", null, null, null, null);
            Assert.That(log.NewValues, Is.Null);
            Assert.That(log.UserId, Is.Null);
        }

        [Test]
        public async Task GetEntityHistory_Empty()
        {
            var history = (await _service.GetEntityHistoryAsync("Nonexistent", "999")).ToList();
            Assert.That(history.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task Log_GeneratesCorrelationId()
        {
            var log = await _service.LogCreateAsync("Test", "1", null, 1, null, null);
            Assert.That(log.CorrelationId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task GetUserActivity_EmptyRange()
        {
            var logs = (await _service.GetUserActivityAsync(99, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(-29))).ToList();
            Assert.That(logs.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task AuditLog_StoresIPAddress()
        {
            var log = await _service.LogAsync("Entity", "1", "Read", null, null, 1, null, "10.0.0.1", null);
            Assert.That(log.IpAddress, Is.EqualTo("10.0.0.1"));
        }

        [Test]
        public async Task AuditLog_StoresUserAgent()
        {
            var log = await _service.LogAsync("Entity", "2", "Create", null, "{}", 1, null, null, null);
            Assert.That(log.UserAgent, Is.Null); // Not set via LogAsync signature without userAgent
        }

        [Test]
        public async Task GetAuditReport_MultipleModules()
        {
            await _service.LogCreateAsync("A", "1", null, 1, null, "ModA");
            await _service.LogCreateAsync("B", "1", null, 1, null, "ModB");
            var all = (await _service.GetAuditReportAsync(DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1))).ToList();
            Assert.That(all.Count, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public async Task VerifyDataIntegrity_Empty()
        {
            var result = await _service.VerifyDataIntegrityAsync("Ghost", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);
            Assert.That(result, Is.True);
        }

        [Test]
        public async Task GetComplianceReport_Empty()
        {
            var result = (await _service.GetComplianceReportAsync("None", DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(-29))).ToList();
            Assert.That(result.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task Log_MultipleOperations()
        {
            for (int i = 1; i <= 5; i++)
                await _service.LogCreateAsync("Batch", i.ToString(), null, 1, null, "Test");

            var history = (await _service.GetAuditReportAsync(DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(5), "Test")).ToList();
            Assert.That(history.Count, Is.EqualTo(5));
        }
    }
}
