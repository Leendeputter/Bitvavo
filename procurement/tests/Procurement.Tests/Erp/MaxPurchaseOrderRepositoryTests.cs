using System;
using Procurement.Core.Entities;
using Procurement.Core.Models;
using Procurement.Erp;
using Xunit;

namespace Procurement.Tests.Erp
{
    /// <summary>
    /// Pure field-mapping tests for MaxPurchaseOrderRepository.BuildPurchaseOrderLine/
    /// BuildConfirmedReference — the two places a wrong-but-plausible-looking guess already slipped
    /// through manual review twice this project (FORCUR_10's decimal-to-double cast, ORDER_10 left
    /// blank on non-first PO lines), caught only once someone actually rebuilt against the real MAX
    /// SDK. These run without any live MAX connection — both methods are pure object mapping.
    /// </summary>
    public class MaxPurchaseOrderRepositoryTests
    {
        private static PurchaseRequestLine BuildRequestLine(DateTime? requiredDate = null, string stockId = null) => new PurchaseRequestLine
        {
            ErpArticleId = "PART-001",
            RequiredDate = requiredDate,
            StockId = stockId
        };

        private static PurchaseOrderDraftLine BuildDraftLine(int quantity = 5000, decimal unitPrice = 0.11m) => new PurchaseOrderDraftLine
        {
            PurchaseRequestLineId = 42,
            SupplierPartNumber = "DK-123",
            Quantity = quantity,
            UnitPrice = unitPrice,
            LineTotal = quantity * unitPrice
        };

        [Fact]
        public void BuildPurchaseOrderLine_FirstLine_LeavesOrdnumLinnumDelnumOrderBlank()
        {
            var line = MaxPurchaseOrderRepository.BuildPurchaseOrderLine(
                "VEN01", BuildRequestLine(), BuildDraftLine(), erpPoNumber: null, lineNumber: 1, isFirstLine: true, userName: "tester");

            Assert.Equal("", line.ORDNUM_10);
            Assert.Equal("", line.LINNUM_10);
            Assert.Equal("", line.DELNUM_10);
            Assert.Equal("", line.ORDER_10);
        }

        [Fact]
        public void BuildPurchaseOrderLine_SecondLine_SetsOrdnumLinnumDelnumAndComputesOrder()
        {
            var line = MaxPurchaseOrderRepository.BuildPurchaseOrderLine(
                "VEN01", BuildRequestLine(), BuildDraftLine(), erpPoNumber: "76012345", lineNumber: 2, isFirstLine: false, userName: "tester");

            Assert.Equal("76012345", line.ORDNUM_10);
            Assert.Equal("02", line.LINNUM_10);
            Assert.Equal("01", line.DELNUM_10);
            // The exact bug fixed after the ORDER_10 review: AddPODetail only builds this itself in
            // its CreateHeader:true branch, so every later line needs it computed here instead.
            Assert.Equal("76012345" + "02" + "01", line.ORDER_10);
        }

        [Fact]
        public void BuildPurchaseOrderLine_ThirdLine_UsesTwoDigitSequentialLinnum()
        {
            var line = MaxPurchaseOrderRepository.BuildPurchaseOrderLine(
                "VEN01", BuildRequestLine(), BuildDraftLine(), erpPoNumber: "76012345", lineNumber: 3, isFirstLine: false, userName: "tester");

            Assert.Equal("03", line.LINNUM_10);
        }

        [Fact]
        public void BuildPurchaseOrderLine_UnitPrice_CastsDecimalToDoubleWithoutLoss()
        {
            var line = MaxPurchaseOrderRepository.BuildPurchaseOrderLine(
                "VEN01", BuildRequestLine(), BuildDraftLine(unitPrice: 0.115m), erpPoNumber: null, lineNumber: 1, isFirstLine: true, userName: "tester");

            Assert.Equal(0.115, line.FORCUR_10, 10);
        }

        [Fact]
        public void BuildPurchaseOrderLine_RequiredDateProvided_UsesItAsDueDate()
        {
            var requiredDate = new DateTime(2026, 9, 1);
            var line = MaxPurchaseOrderRepository.BuildPurchaseOrderLine(
                "VEN01", BuildRequestLine(requiredDate), BuildDraftLine(), erpPoNumber: null, lineNumber: 1, isFirstLine: true, userName: "tester");

            Assert.Equal(requiredDate, line.CURDUE_10);
        }

        [Fact]
        public void BuildPurchaseOrderLine_NoStockId_FallsBackToFGI()
        {
            var line = MaxPurchaseOrderRepository.BuildPurchaseOrderLine(
                "VEN01", BuildRequestLine(stockId: null), BuildDraftLine(), erpPoNumber: null, lineNumber: 1, isFirstLine: true, userName: "tester");

            Assert.Equal("FGI", line.STK_10);
        }

        [Fact]
        public void BuildPurchaseOrderLine_StockIdProvided_UsesItInsteadOfFallback()
        {
            var line = MaxPurchaseOrderRepository.BuildPurchaseOrderLine(
                "VEN01", BuildRequestLine(stockId: "MAIN"), BuildDraftLine(), erpPoNumber: null, lineNumber: 1, isFirstLine: true, userName: "tester");

            Assert.Equal("MAIN", line.STK_10);
        }

        [Fact]
        public void BuildPurchaseOrderLine_SetsCreatedAndModifiedByFromExplicitUserName()
        {
            var line = MaxPurchaseOrderRepository.BuildPurchaseOrderLine(
                "VEN01", BuildRequestLine(), BuildDraftLine(), erpPoNumber: null, lineNumber: 1, isFirstLine: true, userName: "j.doe");

            Assert.Equal("j.doe", line.CreatedBy);
            Assert.Equal("j.doe", line.ModifiedBy);
        }

        // --- BuildConfirmedReference (ORDREF_10 "O "/"OP " prefix — spec confirmed with the user) ---

        [Fact]
        public void BuildConfirmedReference_ConfirmedAsOrdered_PrependsOPrefix()
        {
            var result = MaxPurchaseOrderRepository.BuildConfirmedReference("PROJECT-X", dateChanged: false);
            Assert.Equal("O PROJECT-X", result);
        }

        [Fact]
        public void BuildConfirmedReference_DateChanged_PrependsOPPrefix()
        {
            var result = MaxPurchaseOrderRepository.BuildConfirmedReference("PROJECT-X", dateChanged: true);
            Assert.Equal("OP PROJECT-X", result);
        }

        [Fact]
        public void BuildConfirmedReference_ReprocessingAConfirmedLine_DoesNotStackPrefixes()
        {
            var firstPass = MaxPurchaseOrderRepository.BuildConfirmedReference("PROJECT-X", dateChanged: false);
            var secondPass = MaxPurchaseOrderRepository.BuildConfirmedReference(firstPass, dateChanged: false);

            Assert.Equal("O PROJECT-X", secondPass);
        }

        [Fact]
        public void BuildConfirmedReference_FlippingFromCleanToDateChanged_ReplacesPrefixRatherThanStacking()
        {
            var confirmedClean = MaxPurchaseOrderRepository.BuildConfirmedReference("PROJECT-X", dateChanged: false);
            var laterFoundLate = MaxPurchaseOrderRepository.BuildConfirmedReference(confirmedClean, dateChanged: true);

            Assert.Equal("OP PROJECT-X", laterFoundLate);
        }

        [Fact]
        public void BuildConfirmedReference_CombinedTextOverLimit_TruncatesFromTheEnd()
        {
            // ORDREF_10 is char(25) in MAX — "OP " (3) + a 24-char reference is 27, must come back at 25.
            var result = MaxPurchaseOrderRepository.BuildConfirmedReference("123456789012345678901234", dateChanged: true);

            Assert.Equal(25, result.Length);
            Assert.Equal("OP 1234567890123456789012", result);
        }

        [Fact]
        public void BuildConfirmedReference_EmptyExistingReference_IsJustTheMarker()
        {
            var result = MaxPurchaseOrderRepository.BuildConfirmedReference("", dateChanged: false);
            Assert.Equal("O ", result);
        }

        [Fact]
        public void BuildConfirmedReference_NullExistingReference_IsJustTheMarker()
        {
            var result = MaxPurchaseOrderRepository.BuildConfirmedReference(null, dateChanged: false);
            Assert.Equal("O ", result);
        }
    }
}
