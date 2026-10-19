using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Procurement.Core.Exceptions;
using Procurement.Core.Models;
using Procurement.Data;
using Procurement.Data.Repositories;
using Procurement.Suppliers.DigiKey;
using Procurement.Suppliers.DigiKey.Http;

namespace Procurement.Tools.DigiKeyOrderTester
{
    /// <summary>
    /// Calls DigiKeyAdapter.CreateOrderAsync directly, the same code path MainForm's "Order
    /// plaatsen" uses, but without going through ProcurementEngine/MaxErpConnector first — so this
    /// never writes a PO into MAX. Only reads the already-authorized DIGIKEY row from
    /// Procurement_Supplier (ClientId/ClientSecret/RefreshToken/AccountId/contact fields), all via
    /// the exact same SecretProtector-backed properties the real app uses.
    /// </summary>
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Gebruik: Procurement.Tools.DigiKeyOrderTester.exe <DigiKeyPartNumber> [quantity] [unitPrice]");
                Console.WriteLine();
                Console.WriteLine("Vereiste omgevingsvariabelen:");
                Console.WriteLine("  PROCUREMENT_SECRET_KEY            - dezelfde sleutel als de hoofdapplicatie gebruikt.");
                Console.WriteLine("  PROCUREMENT_TEST_CONNECTION_STRING - connection string naar de echte 'Unitron'-database.");
                return 1;
            }

            var partNumber = args[0];
            var quantity = args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 1;
            var unitPrice = args.Length > 2 ? decimal.Parse(args[2], CultureInfo.InvariantCulture) : 0.10m;

            var connectionString = Environment.GetEnvironmentVariable("PROCUREMENT_TEST_CONNECTION_STRING");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Console.WriteLine("Omgevingsvariabele PROCUREMENT_TEST_CONNECTION_STRING is niet gezet.");
                return 1;
            }

            using (var context = new ProcurementDbContext(connectionString))
            {
                var supplierRepository = new SupplierRepository(context);
                var supplier = await supplierRepository.GetByCodeAsync("DIGIKEY").ConfigureAwait(false);
                if (supplier == null)
                {
                    Console.WriteLine("Geen Supplier-rij gevonden met SupplierCode 'DIGIKEY'.");
                    return 1;
                }
                if (string.IsNullOrEmpty(supplier.RefreshToken))
                {
                    Console.WriteLine("Supplier.RefreshToken is leeg - doorloop eerst \"Ordering autoriseren...\" in Instellingen.");
                    return 1;
                }

                var options = new DigiKeyOptions
                {
                    ClientId = supplier.ClientId,
                    ClientSecret = supplier.ClientSecret,
                    RefreshToken = supplier.RefreshToken,
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
                    // Blocking (not fire-and-forget like CompositionRoot's version) since this
                    // short-lived process would otherwise risk exiting before the write lands.
                    httpClient.OnRefreshTokenRotated = newToken =>
                    {
                        Console.WriteLine("Refresh-token is geroteerd tijdens deze aanroep, wordt teruggeschreven...");
                        supplierRepository.UpdateRefreshTokenAsync("DIGIKEY", newToken).GetAwaiter().GetResult();
                    };

                    var adapter = new DigiKeyAdapter(options, httpClient);
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
    }
}
