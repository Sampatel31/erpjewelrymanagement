using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using ShingarERP.Core.Constants;
using ShingarERP.Core.Models;
using ShingarERP.Data;
using ShingarERP.Data.Repositories;
using ShingarERP.Services;

namespace ShingarERP.Tests.Services
{
    [TestFixture]
    public class AccountingServiceTests
    {
        private ShingarContext    _context = null!;
        private UnitOfWork        _uow     = null!;
        private AccountingService _service = null!;

        [SetUp]
        public void SetUp()
        {
            var options = new DbContextOptionsBuilder<ShingarContext>()
                .UseInMemoryDatabase($"AccountingTest_{Guid.NewGuid()}")
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _context = new ShingarContext(options);
            _uow     = new UnitOfWork(_context);
            _service = new AccountingService(_uow, _context, NullLogger<AccountingService>.Instance);

            SeedAccounts();
        }

        [TearDown]
        public void TearDown()
        {
            _uow.Dispose();
            _context.Dispose();
        }

        // ── Journal Entry ────────────────────────────────────────────

        [Test]
        public async Task PostJournalEntry_BalancedEntry_ShouldSucceed()
        {
            var request = new CreateJournalEntryRequest
            {
                VoucherType = AppConstants.VoucherType.Cash,
                VoucherDate = DateTime.Today,
                Narration   = "Gold purchase – 100g @ ₹6500/g",
                Lines       = new System.Collections.Generic.List<CreateJournalLineRequest>
                {
                    new() { AccountId = 1, DebitAmount  = 650_000m, CreditAmount = 0 },
                    new() { AccountId = 2, DebitAmount  = 0,        CreditAmount = 650_000m }
                }
            };

            var entry = await _service.PostJournalEntryAsync(request);

            Assert.That(entry.IsPosted,     Is.True);
            Assert.That(entry.TotalDebit,   Is.EqualTo(650_000m));
            Assert.That(entry.TotalCredit,  Is.EqualTo(650_000m));
            Assert.That(entry.VoucherNo,    Is.Not.Empty);
        }

        [Test]
        public async Task PostJournalEntry_ImbalancedEntry_ShouldThrow()
        {
            var request = new CreateJournalEntryRequest
            {
                VoucherType = AppConstants.VoucherType.Journal,
                VoucherDate = DateTime.Today,
                Lines       = new System.Collections.Generic.List<CreateJournalLineRequest>
                {
                    new() { AccountId = 1, DebitAmount = 100m, CreditAmount = 0 },
                    new() { AccountId = 2, DebitAmount = 0,    CreditAmount = 50m }  // unbalanced
                }
            };

            var ex = Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.PostJournalEntryAsync(request));

            Assert.That(ex!.Message, Does.Contain("not balanced"));
        }

        [Test]
        public async Task PostJournalEntry_LessThan2Lines_ShouldThrow()
        {
            var request = new CreateJournalEntryRequest
            {
                VoucherType = AppConstants.VoucherType.Journal,
                VoucherDate = DateTime.Today,
                Lines       = new System.Collections.Generic.List<CreateJournalLineRequest>
                {
                    new() { AccountId = 1, DebitAmount = 1000m, CreditAmount = 0 }
                }
            };

            Assert.ThrowsAsync<ArgumentException>(() => _service.PostJournalEntryAsync(request));
        }

        [Test]
        public async Task PostJournalEntry_PostingToControlAccount_ShouldThrow()
        {
            // Account 3 is a control account (AllowPosting = false in seed data)
            var request = new CreateJournalEntryRequest
            {
                VoucherType = AppConstants.VoucherType.Journal,
                VoucherDate = DateTime.Today,
                Lines       = new System.Collections.Generic.List<CreateJournalLineRequest>
                {
                    new() { AccountId = 3, DebitAmount  = 1000m, CreditAmount = 0 },
                    new() { AccountId = 2, DebitAmount  = 0,     CreditAmount = 1000m }
                }
            };

            var ex = Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.PostJournalEntryAsync(request));

            Assert.That(ex!.Message, Does.Contain("control account"));
        }

        // ── Trial Balance ────────────────────────────────────────────

        [Test]
        public async Task GetTrialBalance_AfterPosting_ShouldReflectEntries()
        {
            var request = new CreateJournalEntryRequest
            {
                VoucherType = AppConstants.VoucherType.Cash,
                VoucherDate = DateTime.Today,
                Lines = new System.Collections.Generic.List<CreateJournalLineRequest>
                {
                    new() { AccountId = 1, DebitAmount  = 50_000m, CreditAmount = 0 },
                    new() { AccountId = 2, DebitAmount  = 0,       CreditAmount = 50_000m }
                }
            };

            await _service.PostJournalEntryAsync(request);

            var tb = await _service.GetTrialBalanceAsync(DateTime.Today);

            Assert.That(tb, Is.Not.Empty);
            var acc1 = tb.Find(r => r.AccountCode == "1001");
            Assert.That(acc1, Is.Not.Null);
            Assert.That(acc1!.TotalDebit, Is.EqualTo(50_000m));
        }

        // ── Tally XML ────────────────────────────────────────────────

        [Test]
        public async Task ExportToTallyXmlAsync_WithEntries_ShouldProduceValidXml()
        {
            await _service.PostJournalEntryAsync(new CreateJournalEntryRequest
            {
                VoucherType = AppConstants.VoucherType.Journal,
                VoucherDate = DateTime.Today,
                Narration   = "Test entry",
                Lines = new System.Collections.Generic.List<CreateJournalLineRequest>
                {
                    new() { AccountId = 1, DebitAmount  = 10_000m, CreditAmount = 0 },
                    new() { AccountId = 2, DebitAmount  = 0,       CreditAmount = 10_000m }
                }
            });

            var xml = await _service.ExportToTallyXmlAsync(
                DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1));

            Assert.That(xml, Does.Contain("<ENVELOPE>"));
            Assert.That(xml, Does.Contain("<TALLYMESSAGE"));
        }

        // ── Seed ─────────────────────────────────────────────────────

        private void SeedAccounts()
        {
            _context.Accounts.AddRange(
                new Account { AccountId = 1, AccountCode = "1001", AccountName = "Cash in Hand",   AccountType = "Asset",     NormalBalance = "Dr", AllowPosting = true,  IsActive = true, OpeningBalance = 0 },
                new Account { AccountId = 2, AccountCode = "2001", AccountName = "Accounts Payable",AccountType = "Liability", NormalBalance = "Cr", AllowPosting = true,  IsActive = true, OpeningBalance = 0 },
                new Account { AccountId = 3, AccountCode = "1000", AccountName = "Assets (Control)",AccountType = "Asset",     NormalBalance = "Dr", AllowPosting = false, IsControl = true, IsActive = true }
            );
            _context.SaveChanges();
        }
    }
}
