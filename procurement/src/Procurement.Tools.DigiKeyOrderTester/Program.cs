using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.Threading.Tasks;
using Procurement.Core.Exceptions;
using Procurement.Core.Models;
using Procurement.Core.Session;
using Procurement.Data;
using Procurement.Data.Repositories;
using Procurement.Data.Secrets;
using Procurement.Suppliers.DigiKey;
using Procurement.Suppliers.DigiKey.Http;

namespace Procurement.Tools.DigiKeyOrderTester
{
    /// <summary>
    /// Calls DigiKeyAdapter.CreateOrderAsync/GetOrderStatusAsync directly, the same code paths
    /// MainForm's "Order plaatsen"/"Orderbevestiging verwerken" use, but without going through
    /// ProcurementEngine/MaxErpConnector first — so this never writes a PO into MAX, nor requires one
    /// to already exist locally for a status lookup. Only reads the already-authorized DIGIKEY row
    /// from Procurement_Supplier (IsSandbox/AccountId/contact fields) plus the DigiKey secrets from
    /// the SharedSecrets database (ClientId/ClientSecret/RefreshToken), via the exact same
    /// SecretSettings the real app uses — so the Windows user running this needs a SecretAccess
    /// grant for those three, same as in the app.
    /// </summary>
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Gebruik:");
                Console.WriteLine("  Procurement.Tools.DigiKeyOrderTester.exe <DigiKeyPartNumber> [quantity] [unitPrice]");
                Console.WriteLine("  Procurement.Tools.DigiKeyOrderTester.exe status <SalesOrderId>");
                Console.WriteLine();
                Console.WriteLine("Vereiste omgevingsvariabele:");
                Console.WriteLine("  PROCUREMENT_TEST_CONNECTION_STRING - connection string naar de echte 'Unitron'-database.");
                return 1;
            }

            var connectionString = Environment.GetEnvironmentVariable("PROCUREMENT_TEST_CONNECTION_STRING");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Console.WriteLine("Omgevingsvariabele PROCUREMENT_TEST_CONNECTION_STRING is niet gezet.");
                return 1;
            }

            // Same server/login, SharedSecrets catalog — mirrors LoginForm/ProcurementConnectionStringHelper.
            ProcurementSession.SharedSecretsConnectionString =
                new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "SharedSecrets" }.ConnectionString;
            ProcurementSession.UserName = Environment.UserName;

            using (var context = new ProcurementDbContext(connectionString))
            {
                var supplierRepository = new SupplierRepository(context);
                var supplier = await supplierRepository.GetByCodeAsync("DIGIKEY").ConfigureAwait(false);
                if (supplier == null)
                {
                    Console.WriteLine("Geen Supplier-rij gevonden met SupplierCode 'DIGIKEY'.");
                    return 1;
                }
                string clientId, clientSecret, refreshToken;
                try
                {
                    clientId = SecretSettings.DigiKeyClientId;
                    clientSecret = SecretSettings.DigiKeyClientSecret;
                    refreshToken = SecretSettings.DigiKeyRefreshToken;
                }
                catch (UnauthorizedAccessException ex)
                {
                    Console.WriteLine(ex.Message);
                    return 1;
                }
                if (string.IsNullOrEmpty(refreshToken))
                {
                    Console.WriteLine("DigiKeyRefreshToken is leeg - doorloop eerst \"Authorize Ordering...\" in Settings.");
                    return 1;
                }

                var options = new DigiKeyOptions
                {
                    ClientId = clientId,
                    ClientSecret = clientSecret,
                    RefreshToken = refreshToken,
                    IsSandbox = supplier.IsSandbox,
                    UseMockData = false,
                    AccountId = supplier.AccountId,
                    ContactName = supplier.ContactName,
                    ContactEmail = supplier.ContactEmail,
                    ContactTelephone = supplier.ContactTelephone,
                    AddressLine1 = supplier.AddressLine1,
                    AddressLine2 = supplier.AddressLine2,
                    City = supplier.City,
                    Province = supplier.Province,
                    PostalCode = supplier.PostalCode,
                    CountryCode = supplier.CountryCode
                };

                Console.WriteLine($"IsSandbox={options.IsSandbox}  AccountId={options.AccountId}  Contact={options.ContactName}");

                using (var httpClient = new DigiKeyHttpClientWrapper(options))
                {
                    // Blocking, since this short-lived process would otherwise risk exiting before
                    // the write lands. Now goes to SharedSecrets (db_datawriter for the MAX login per
                    // UniPro's permission script) instead of Procurement_Supplier, which this tool's
                    // login couldn't UPDATE — hence the old "paste this UPDATE in SSMS" fallback is gone.
                    httpClient.OnRefreshTokenRotated = newToken =>
                    {
                        Console.WriteLine("Refresh-token is geroteerd tijdens deze aanroep, wordt teruggeschreven...");
                        try
                        {
                            SecretSettings.SaveDigiKeyRefreshToken(newToken);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"WAARSCHUWING: kon het nieuwe refresh-token niet opslaan ({ex.Message}).");
                            Console.WriteLine("Deze run gaat door, maar de VOLGENDE run werkt pas weer na opnieuw");
                            Console.WriteLine("\"Authorize Ordering...\" in Settings.");
                        }
                    };

                    var adapter = new DigiKeyAdapter(options, httpClient);

                    return string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase)
                        ? await RunStatusAsync(adapter, args)
                        : await RunPlaceOrderAsync(adapter, args);
                }
            }
        }

        private static async Task<int> RunStatusAsync(DigiKeyAdapter adapter, string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Gebruik: Procurement.Tools.DigiKeyOrderTester.exe status <SalesOrderId>");
                return 1;
            }

            var salesOrderId = args[1];
            Console.WriteLine($"Status opvragen voor SalesOrderId {salesOrderId} ...");

            try
            {
                var status = await adapter.GetOrderStatusAsync(salesOrderId).ConfigureAwait(false);
                Console.WriteLine("Gelukt:");
                Console.WriteLine($"  Status      = {status.Status}");
                Console.WriteLine($"  ConfirmedAt = {status.ConfirmedAt}");
                foreach (var line in status.Lines)
                {
                    Console.WriteLine($"  Regel: {line.SupplierPartNumber}  ConfirmedQuantity={line.ConfirmedQuantity}  " +
                        $"ConfirmedUnitPrice={line.ConfirmedUnitPrice}  EstimatedShipDate={line.EstimatedShipDate}");
                }
                return 0;
            }
            catch (SupplierException ex)
            {
                Console.WriteLine($"Mislukt ({ex.ErrorCode}): {ex.Message}");
                return 1;
            }
        }

        private static async Task<int> RunPlaceOrderAsync(DigiKeyAdapter adapter, string[] args)
        {
            var partNumber = args[0];
            var quantity = args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 1;
            var unitPrice = args.Length > 2 ? decimal.Parse(args[2], CultureInfo.InvariantCulture) : 0.10m;

            var request = new SupplierOrderRequest
            {
                SupplierCode = "DIGIKEY",
                ErpPoNumber = $"TEST-{DateTime.UtcNow:yyyyMMddHHmmss}",
                Currency = "EUR",
                Lines = new List<SupplierOrderRequestLine>
                {
                    new SupplierOrderRequestLine
                    {
                        SupplierPartNumber = partNumber,
                        Quantity = quantity,
                        UnitPrice = unitPrice
                    }
                }
            };

            Console.WriteLine($"Order plaatsen: {quantity}x {partNumber} @ {unitPrice.ToString("0.00", CultureInfo.InvariantCulture)} EUR, referentie {request.ErpPoNumber} ...");

            try
            {
                var result = await adapter.CreateOrderAsync(request, Guid.NewGuid().ToString("N")).ConfigureAwait(false);
                Console.WriteLine("Gelukt:");
                Console.WriteLine($"  SupplierOrderNumber = {result.SupplierOrderNumber}");
                Console.WriteLine($"  Status              = {result.Status}");
                Console.WriteLine($"  OrderTotal          = {result.OrderTotal} {result.Currency}");
                return 0;
            }
            catch (SupplierException ex)
            {
                Console.WriteLine($"Mislukt ({ex.ErrorCode}): {ex.Message}");
                return 1;
            }
        }
    }
}
