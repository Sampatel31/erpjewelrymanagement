namespace ShingarERP.Core.Constants
{
    /// <summary>Application-wide constants for Shringar Jewellers ERP.</summary>
    public static class AppConstants
    {
        public static class MinStockAlert
        {
            public const decimal Gold24K  = 100m;
            public const decimal Gold22K  = 200m;
            public const decimal Gold18K  = 100m;
            public const decimal Silver99 = 500m;
        }

        public static class StockLocation
        {
            public const string Showroom = "Showroom";
            public const string Safe     = "Safe";
            public const string Locker   = "Locker";
            public const string Counter  = "Counter";
            public const string Vault    = "Vault";
        }

        public static class TransactionType
        {
            public const string Purchase = "Purchase";
            public const string Sale     = "Sale";
            public const string Transfer = "Transfer";
            public const string Adjust   = "Adjust";
            public const string Return   = "Return";
        }

        public static class VoucherType
        {
            public const string Cash     = "Cash";
            public const string Bank     = "Bank";
            public const string Journal  = "Journal";
            public const string Sales    = "Sales";
            public const string Purchase = "Purchase";
            public const string Receipt  = "Receipt";
            public const string Payment  = "Payment";
        }

        public static class KYCDocType
        {
            public const string Aadhaar       = "Aadhaar";
            public const string PAN           = "PAN";
            public const string Passport      = "Passport";
            public const string DrivingLicence = "DrivingLicense";
            public const string VoterID       = "VoterID";
        }
    }
}
