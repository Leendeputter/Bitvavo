using System;
using Procurement.Core.BusinessLogic;
using Procurement.Core.Entities;
using Procurement.Core.Enums;
using Xunit;

namespace Procurement.Tests.BusinessLogic
{
    public class ApprovalEvaluatorTests
    {
        private static PurchaseRequestLine BuildLine() => new PurchaseRequestLine
        {
            Id = 1,
            Manufacturer = "Vishay",
            ManufacturerPartNumber = "CRCW060310K0FKEA",
            RequestedQuantity = 100
        };

        private static SupplierOffer BuildOffer(decimal unitPrice, decimal totalPrice, MatchConfidence confidence = MatchConfidence.Exact,
            ReelType? reelType = null, string mpn = "CRCW060310K0FKEA") => new SupplierOffer
        {
            SupplierCode = "DIGIKEY",
            ManufacturerPartNumber = mpn,
            UnitPrice = unitPrice,
            TotalPrice = totalPrice,
            MatchConfidence = confidence,
            ReelType = reelType
        };

        private static ApprovalPolicy DefaultPolicy() => new ApprovalPolicy
        {
            MaxOrderValueForAutoApproval = 500m,
            MaxPriceVariancePercentage = 10m,
            AllowExternalSupplier = false,
            AllowNonOriginalPackaging = true,
            AllowAlternativePart = false
        };

        [Fact]
        public void WithinAllThresholds_AndKnownSupplier_AutoApproves()
        {
            var offer = BuildOffer(unitPrice: 0.11m, totalPrice: 100m);
            var preference = new SupplierPreference { SupplierCode = "DIGIKEY", AllowedForAutoOrder = true };

            var result = ApprovalEvaluator.Evaluate(offer, BuildLine(), DefaultPolicy(), preference, referenceUnitPrice: 0.11m);

            Assert.True(result.IsAutoApproved);
            Assert.Empty(result.Reasons);
        }

        [Fact]
        public void OrderValueAboveThreshold_RequiresApproval()
        {
            var offer = BuildOffer(unitPrice: 1m, totalPrice: 5000m);
            var preference = new SupplierPreference { SupplierCode = "DIGIKEY", AllowedForAutoOrder = true };

            var result = ApprovalEvaluator.Evaluate(offer, BuildLine(), DefaultPolicy(), preference, referenceUnitPrice: 1m);

            Assert.False(result.IsAutoApproved);
            Assert.Contains(result.Reasons, r => r.Contains("Orderwaarde"));
        }

        [Fact]
        public void UnknownSupplier_RequiresApproval_WhenExternalSupplierNotAllowed()
        {
            var offer = BuildOffer(unitPrice: 0.11m, totalPrice: 100m);

            var result = ApprovalEvaluator.Evaluate(offer, BuildLine(), DefaultPolicy(), supplierPreference: null, referenceUnitPrice: 0.11m);

            Assert.False(result.IsAutoApproved);
            Assert.Contains(result.Reasons, r => r.Contains("niet geconfigureerd voor automatisch bestellen"));
        }

        [Fact]
        public void LargePriceVariance_RequiresApproval()
        {
            var offer = BuildOffer(unitPrice: 0.20m, totalPrice: 100m);
            var preference = new SupplierPreference { SupplierCode = "DIGIKEY", AllowedForAutoOrder = true };

            var result = ApprovalEvaluator.Evaluate(offer, BuildLine(), DefaultPolicy(), preference, referenceUnitPrice: 0.10m);

            Assert.False(result.IsAutoApproved);
            Assert.Contains(result.Reasons, r => r.Contains("Prijsafwijking"));
        }

        [Fact]
        public void AlternativePart_RequiresApproval_WhenNotAllowed()
        {
            var offer = BuildOffer(unitPrice: 0.11m, totalPrice: 100m, mpn: "SOME-OTHER-PART");
            var preference = new SupplierPreference { SupplierCode = "DIGIKEY", AllowedForAutoOrder = true };

            var result = ApprovalEvaluator.Evaluate(offer, BuildLine(), DefaultPolicy(), preference, referenceUnitPrice: 0.11m);

            Assert.False(result.IsAutoApproved);
            Assert.Contains(result.Reasons, r => r.Contains("alternatief onderdeel"));
        }

        [Fact]
        public void FuzzyMatch_RequiresApproval()
        {
            var offer = BuildOffer(unitPrice: 0.11m, totalPrice: 100m, confidence: MatchConfidence.Medium);
            var preference = new SupplierPreference { SupplierCode = "DIGIKEY", AllowedForAutoOrder = true };

            var result = ApprovalEvaluator.Evaluate(offer, BuildLine(), DefaultPolicy(), preference, referenceUnitPrice: 0.11m);

            Assert.False(result.IsAutoApproved);
            Assert.Contains(result.Reasons, r => r.Contains("fuzzy match"));
        }

        [Fact]
        public void NullOffer_NeverAutoApproves()
        {
            var result = ApprovalEvaluator.Evaluate(null, BuildLine(), DefaultPolicy(), null, null);
            Assert.False(result.IsAutoApproved);
        }
    }
}
