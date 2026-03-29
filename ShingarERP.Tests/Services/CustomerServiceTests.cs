using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using ShingarERP.Core.DTOs;
using ShingarERP.Core.Models;
using ShingarERP.Data;
using ShingarERP.Data.Repositories;
using ShingarERP.Services;

namespace ShingarERP.Tests.Services
{
    [TestFixture]
    public class CustomerServiceTests
    {
        private ShingarContext     _context      = null!;
        private UnitOfWork         _uow          = null!;
        private CustomerRepository _customerRepo = null!;
        private CustomerService    _service      = null!;

        [SetUp]
        public void SetUp()
        {
            var options = new DbContextOptionsBuilder<ShingarContext>()
                .UseInMemoryDatabase($"CustomerTest_{Guid.NewGuid()}")
                .Options;

            _context      = new ShingarContext(options);
            _uow          = new UnitOfWork(_context);
            _customerRepo = new CustomerRepository(_context);
            _service      = new CustomerService(_uow, _customerRepo, NullLogger<CustomerService>.Instance);
        }

        [TearDown]
        public void TearDown()
        {
            _uow.Dispose();
            _context.Dispose();
        }

        // ── Create ───────────────────────────────────────────────────

        [Test]
        public async Task CreateCustomerAsync_ValidRequest_ShouldCreate()
        {
            var request = new CreateCustomerRequest
            {
                FirstName = "Ramesh",
                LastName  = "Patel",
                Mobile    = "9876543210",
                City      = "Surat",
                State     = "Gujarat"
            };

            var customer = await _service.CreateCustomerAsync(request);

            Assert.That(customer.FirstName,    Is.EqualTo("Ramesh"));
            Assert.That(customer.Mobile,       Is.EqualTo("9876543210"));
            Assert.That(customer.CustomerCode, Does.StartWith("CUST"));
            Assert.That(customer.KYCVerified,  Is.False);
        }

        [Test]
        public async Task CreateCustomerAsync_DuplicateMobile_ShouldThrow()
        {
            await _service.CreateCustomerAsync(new CreateCustomerRequest
            {
                FirstName = "Suresh",
                Mobile    = "9123456789"
            });

            var ex = Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CreateCustomerAsync(new CreateCustomerRequest
                {
                    FirstName = "Rajesh",
                    Mobile    = "9123456789"
                }));

            Assert.That(ex!.Message, Does.Contain("9123456789"));
        }

        [Test]
        public async Task CreateCustomerAsync_InvalidAadhaar_ShouldThrow()
        {
            var ex = Assert.ThrowsAsync<ArgumentException>(() =>
                _service.CreateCustomerAsync(new CreateCustomerRequest
                {
                    FirstName     = "Test",
                    Mobile        = "9999900000",
                    AadhaarNumber = "12345"   // invalid – must be 12 digits
                }));

            Assert.That(ex!.Message, Does.Contain("Aadhaar"));
        }

        [Test]
        public async Task CreateCustomerAsync_InvalidPAN_ShouldThrow()
        {
            var ex = Assert.ThrowsAsync<ArgumentException>(() =>
                _service.CreateCustomerAsync(new CreateCustomerRequest
                {
                    FirstName = "Test",
                    Mobile    = "9999900001",
                    PANNumber = "INVALID"
                }));

            Assert.That(ex!.Message, Does.Contain("PAN"));
        }

        // ── KYC ──────────────────────────────────────────────────────

        [Test]
        public async Task VerifyKYCAsync_ValidDocument_ShouldMarkVerified()
        {
            var customer = await _service.CreateCustomerAsync(new CreateCustomerRequest
            {
                FirstName = "Priya",
                Mobile    = "8765432109"
            });

            await _service.VerifyKYCAsync(new KycVerificationRequest
            {
                CustomerId     = customer.CustomerId,
                DocumentType   = "Aadhaar",
                DocumentNumber = "123456789012"
            });

            var refreshed = await _service.GetCustomerByIdAsync(customer.CustomerId);
            Assert.That(refreshed!.KYCVerified, Is.True);
            Assert.That(refreshed.KYCVerifiedDate, Is.Not.Null);
        }

        // ── LTV ──────────────────────────────────────────────────────

        [Test]
        public async Task RecalculateLTVAsync_WithPurchaseHistory_ShouldComputeScore()
        {
            var customer = await _service.CreateCustomerAsync(new CreateCustomerRequest
            {
                FirstName = "Kavita",
                Mobile    = "7654321098"
            });

            // Manually update purchase stats
            var entity = await _uow.Repository<Customer>().GetByIdAsync(customer.CustomerId);
            entity!.TotalPurchaseAmount = 100_000m;
            entity.TotalPurchaseCount   = 5;
            entity.LastPurchaseDate     = DateTime.UtcNow.AddDays(-10);
            _uow.Repository<Customer>().Update(entity);
            await _uow.SaveChangesAsync();

            await _service.RecalculateLTVAsync(customer.CustomerId);

            var refreshed = await _uow.Repository<Customer>().GetByIdAsync(customer.CustomerId);
            Assert.That(refreshed!.LTVScore, Is.GreaterThan(0));
            Assert.That(refreshed.LTVScore,  Is.LessThanOrEqualTo(1000));
        }

        // ── Search ───────────────────────────────────────────────────

        [Test]
        public async Task SearchCustomersAsync_ByName_ShouldReturnMatch()
        {
            await _service.CreateCustomerAsync(new CreateCustomerRequest
            {
                FirstName = "Anita",
                LastName  = "Shah",
                Mobile    = "6543210987"
            });

            var result = await _service.SearchCustomersAsync(new CustomerSearchRequest
            {
                SearchTerm = "Anita",
                PageNumber = 1,
                PageSize   = 10
            });

            Assert.That(result.TotalCount,      Is.GreaterThanOrEqualTo(1));
            Assert.That(result.Items,           Has.Some.Property("FirstName").EqualTo("Anita"));
        }

        [Test]
        public async Task SearchCustomersAsync_ByMobile_ShouldReturnMatch()
        {
            await _service.CreateCustomerAsync(new CreateCustomerRequest
            {
                FirstName = "Dinesh",
                Mobile    = "9000000001"
            });

            var result = await _service.SearchCustomersAsync(new CustomerSearchRequest
            {
                SearchTerm = "9000000001",
                PageNumber = 1,
                PageSize   = 10
            });

            Assert.That(result.TotalCount, Is.EqualTo(1));
        }
    }
}
