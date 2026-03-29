using System;
using System.Text.RegularExpressions;
using FluentValidation;
using ShingarERP.Core.DTOs;
using ShingarERP.Core.Constants;

namespace ShingarERP.Utilities
{
    // ── FluentValidation validators ──────────────────────────────────

    /// <summary>Validates a new metal lot creation request.</summary>
    public class CreateMetalLotValidator : AbstractValidator<CreateMetalLotRequest>
    {
        public CreateMetalLotValidator()
        {
            RuleFor(x => x.LotNumber)
                .NotEmpty().WithMessage("Lot number is required.")
                .MaximumLength(30).WithMessage("Lot number must be 30 characters or less.")
                .Matches(@"^[A-Za-z0-9\-_\/]+$").WithMessage("Lot number may only contain letters, digits, dashes, underscores and slashes.");

            RuleFor(x => x.MetalId).GreaterThan(0).WithMessage("Valid metal must be selected.");
            RuleFor(x => x.SupplierId).GreaterThan(0).WithMessage("Valid supplier must be selected.");

            RuleFor(x => x.GrossWeight)
                .GreaterThan(0).WithMessage("Gross weight must be greater than zero.")
                .LessThanOrEqualTo(999_999m).WithMessage("Gross weight exceeds maximum allowed.");

            RuleFor(x => x.NetWeight)
                .GreaterThan(0).WithMessage("Net weight must be greater than zero.")
                .LessThanOrEqualTo(x => x.GrossWeight).WithMessage("Net weight cannot exceed gross weight.");

            RuleFor(x => x.PurchaseRatePerGram)
                .GreaterThan(0).WithMessage("Purchase rate per gram must be greater than zero.");

            RuleFor(x => x.PurchaseDate)
                .NotEmpty().WithMessage("Purchase date is required.")
                .LessThanOrEqualTo(DateTime.Today).WithMessage("Purchase date cannot be in the future.");

            RuleFor(x => x.MeltingLossPercent)
                .InclusiveBetween(0, 50).WithMessage("Melting loss must be between 0% and 50%.");
        }
    }

    /// <summary>Validates finished good creation.</summary>
    public class CreateFinishedGoodValidator : AbstractValidator<CreateFinishedGoodRequest>
    {
        public CreateFinishedGoodValidator()
        {
            RuleFor(x => x.SKU)
                .NotEmpty().WithMessage("SKU is required.")
                .MaximumLength(30)
                .Matches(@"^[A-Z0-9\-]+$").WithMessage("SKU must be uppercase alphanumeric with dashes.");

            RuleFor(x => x.ItemName).NotEmpty().MaximumLength(200);
            RuleFor(x => x.CategoryId).GreaterThan(0);
            RuleFor(x => x.MetalId).GreaterThan(0);

            RuleFor(x => x.GrossWeight)
                .GreaterThan(0).WithMessage("Gross weight must be > 0.")
                .LessThanOrEqualTo(9999m);

            RuleFor(x => x.NetWeight)
                .GreaterThan(0)
                .LessThanOrEqualTo(x => x.GrossWeight).WithMessage("Net weight cannot exceed gross weight.");

            RuleFor(x => x.SalePrice)
                .GreaterThan(0).WithMessage("Sale price must be > 0.");

            RuleFor(x => x.StockLocation)
                .NotEmpty()
                .Must(loc => IsValidLocation(loc)).WithMessage("Invalid stock location.");
        }

        private static bool IsValidLocation(string loc) =>
            loc is AppConstants.StockLocation.Showroom
                or AppConstants.StockLocation.Safe
                or AppConstants.StockLocation.Locker
                or AppConstants.StockLocation.Counter
                or AppConstants.StockLocation.Vault;
    }

    /// <summary>Validates a new customer creation request.</summary>
    public class CreateCustomerValidator : AbstractValidator<CreateCustomerRequest>
    {
        public CreateCustomerValidator()
        {
            RuleFor(x => x.FirstName)
                .NotEmpty().WithMessage("First name is required.")
                .MaximumLength(100)
                .Matches(@"^[A-Za-z\s\.\-']+$").WithMessage("Name contains invalid characters.");

            RuleFor(x => x.LastName)
                .MaximumLength(100)
                .Matches(@"^[A-Za-z\s\.\-']*$").WithMessage("Name contains invalid characters.")
                .When(x => !string.IsNullOrEmpty(x.LastName));

            RuleFor(x => x.Mobile)
                .NotEmpty().WithMessage("Mobile number is required.")
                .Matches(@"^[6-9]\d{9}$").WithMessage("Enter a valid 10-digit Indian mobile number.");

            RuleFor(x => x.Email)
                .EmailAddress().WithMessage("Enter a valid email address.")
                .MaximumLength(100)
                .When(x => !string.IsNullOrEmpty(x.Email));

            RuleFor(x => x.AadhaarNumber)
                .Matches(@"^\d{12}$").WithMessage("Aadhaar number must be exactly 12 digits.")
                .When(x => !string.IsNullOrEmpty(x.AadhaarNumber));

            RuleFor(x => x.PANNumber)
                .Matches(@"^[A-Z]{5}[0-9]{4}[A-Z]{1}$").WithMessage("Invalid PAN format. Example: ABCDE1234F.")
                .When(x => !string.IsNullOrEmpty(x.PANNumber));

            RuleFor(x => x.DateOfBirth)
                .LessThan(DateTime.Today).WithMessage("Date of birth must be in the past.")
                .GreaterThan(DateTime.Today.AddYears(-120)).WithMessage("Date of birth seems too old.")
                .When(x => x.DateOfBirth.HasValue);

            RuleFor(x => x.PinCode)
                .Matches(@"^\d{6}$").WithMessage("PIN code must be 6 digits.")
                .When(x => !string.IsNullOrEmpty(x.PinCode));
        }
    }

    /// <summary>Validates a stone creation request.</summary>
    public class CreateStoneValidator : AbstractValidator<CreateStoneRequest>
    {
        private static readonly string[] ValidLabs  = { "GIA", "IGI", "HRD", "SGL" };
        private static readonly string[] ValidCuts  = { "Excellent", "Very Good", "Good", "Fair", "Poor" };

        public CreateStoneValidator()
        {
            RuleFor(x => x.StoneCode).NotEmpty().MaximumLength(30);
            RuleFor(x => x.StoneType).NotEmpty().MaximumLength(50);

            RuleFor(x => x.CaratWeight)
                .GreaterThan(0).WithMessage("Carat weight must be > 0.")
                .LessThanOrEqualTo(500m).WithMessage("Carat weight exceeds maximum (500 ct).");

            RuleFor(x => x.PurchasePrice).GreaterThan(0);
            RuleFor(x => x.SalePrice).GreaterThanOrEqualTo(x => x.PurchasePrice)
                .WithMessage("Sale price should not be less than purchase price.");

            RuleFor(x => x.CertLab)
                .Must(lab => ValidLabs.Contains(lab!)).WithMessage($"Certificate lab must be one of: {string.Join(", ", ValidLabs)}.")
                .When(x => !string.IsNullOrEmpty(x.CertLab));

            RuleFor(x => x.Cut)
                .Must(cut => ValidCuts.Contains(cut!)).WithMessage($"Cut must be one of: {string.Join(", ", ValidCuts)}.")
                .When(x => !string.IsNullOrEmpty(x.Cut));
        }
    }

    // ── Generic validation helper ────────────────────────────────────

    /// <summary>Centralized validation runner to surface errors as exceptions.</summary>
    public static class ValidationHelper
    {
        /// <summary>Validate and throw <see cref="ValidationException"/> if invalid.</summary>
        public static void ValidateAndThrow<T>(AbstractValidator<T> validator, T instance)
        {
            var result = validator.Validate(instance);
            if (!result.IsValid)
                throw new FluentValidation.ValidationException(result.Errors);
        }
    }
}
