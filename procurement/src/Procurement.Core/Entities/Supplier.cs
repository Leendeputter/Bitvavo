using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using Procurement.Core.Enums;
using Procurement.Core.Security;

namespace Procurement.Core.Entities
{
    /// <summary>Master record for a configured supplier (DigiKey, Farnell, ...).</summary>
    public class Supplier
    {
        public int Id { get; set; }
        public string SupplierCode { get; set; }
        public string Name { get; set; }

        /// <summary>MAX Part_Vendor.VENID_07 that identifies this supplier in MAX — used to translate MAX's vendor-part cross-reference into SupplierProductMapping rows. Editable via Instellingen (§8.6) since it's MAX-environment-specific, not a code constant.</summary>
        public string VendorId { get; set; }

        public bool IsSandbox { get; set; }
        public bool UseMockData { get; set; }

        // API credentials — DigiKey needs ClientId+ClientSecret (OAuth2 client_credentials), Farnell
        // needs only ApiKey; unused fields for a given supplier just stay null. Stored encrypted
        // (SecretProtector, AES-256 with a key from an environment variable — never in the database
        // or source control in plaintext) and only ever exposed decrypted through the [NotMapped]
        // properties below, editable via Instellingen's "Credentials bewerken" dialog (write-only:
        // the UI never displays a stored value back, only lets you overwrite it).
        public string ClientIdEncrypted { get; set; }
        public string ClientSecretEncrypted { get; set; }
        public string ApiKeyEncrypted { get; set; }

        [NotMapped]
        public string ClientId
        {
            get => SecretProtector.Decrypt(ClientIdEncrypted);
            set => ClientIdEncrypted = SecretProtector.Encrypt(value);
        }

        [NotMapped]
        public string ClientSecret
        {
            get => SecretProtector.Decrypt(ClientSecretEncrypted);
            set => ClientSecretEncrypted = SecretProtector.Encrypt(value);
        }

        [NotMapped]
        public string ApiKey
        {
            get => SecretProtector.Decrypt(ApiKeyEncrypted);
            set => ApiKeyEncrypted = SecretProtector.Encrypt(value);
        }

        /// <summary>
        /// OAuth2 refresh token from a supplier's 3-legged (Authorization Code) consent flow — only
        /// DigiKey's Ordering v3 API needs this so far (its Product Information V4 uses plain 2-legged
        /// client-credentials, same as every other supplier here). Persisted the same way as the
        /// other credentials (SecretProtector, never in plaintext) but never edited directly: it's
        /// only ever written by DigiKeyOrderingAuthorizer's interactive consent flow
        /// ("Ordering autoriseren..." in Instellingen) or rotated automatically whenever it's used to
        /// refresh an access token (some providers issue a new refresh token on every use).
        /// </summary>
        public string RefreshTokenEncrypted { get; set; }

        [NotMapped]
        public string RefreshToken
        {
            get => SecretProtector.Decrypt(RefreshTokenEncrypted);
            set => RefreshTokenEncrypted = SecretProtector.Encrypt(value);
        }

        // Account/shipping-contact info a real Ordering API call needs (confirmed so far against
        // DigiKey's Ordering v3 OrderRequest.BuyerContact/ShippingContact — modeled generically here,
        // not DigiKey-specifically, since any supplier's real order-placement will need broadly the
        // same shape: an account reference plus a shipping contact/address). Editable per supplier
        // via Instellingen's "Verzendgegevens bewerken" dialog, unlike ClientId/ClientSecret/ApiKey
        // this is plain (not encrypted) — an account number and a shipping address aren't secrets.
        public string AccountId { get; set; }
        public string ContactName { get; set; }
        public string ContactEmail { get; set; }
        public string ContactTelephone { get; set; }
        public string AddressLine1 { get; set; }
        public string AddressLine2 { get; set; }
        public string City { get; set; }
        public string Province { get; set; }
        public string PostalCode { get; set; }
        public string CountryCode { get; set; }

        public virtual List<SupplierCapabilityRecord> Capabilities { get; set; } = new List<SupplierCapabilityRecord>();
    }

    /// <summary>Persisted row of the capability matrix from spec §5, one row per (Supplier, Capability).</summary>
    public class SupplierCapabilityRecord
    {
        public int Id { get; set; }
        public int SupplierId { get; set; }
        public virtual Supplier Supplier { get; set; }

        /// <summary>Name of the capability, e.g. "ProductSearch", "Pricing", "Ordering", "Shipment", "Tracking", "Invoice".</summary>
        public string Capability { get; set; }
        public CapabilityStatus Status { get; set; }
    }
}
