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
    /// SDK. These run without any live MAX connection — both methods are pure object mapping; the due
    /// date itself is computed elsewhere (MaxOrderRepository.CalculatePoDueDateAsync, which calls
    /// MAX's own SQLCLR function and so needs a live connection — not unit-tested here) and handed to
    /// BuildPurchaseOrderLine already resolved.
    /// </summary>
    public class MaxPurchaseOrderRepositoryTests
    {
        private static readonly DateTime DefaultDueDate = new DateTime(2026, 9, 1);

        private static PurchaseRequestLine BuildRequestLine() => new PurchaseRequestLine
        {
            ErpArticleId = "PART-001"
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
                "VEN01", BuildRequestLine(), BuildDraftLine(), partInfo: null, DefaultDueDate, erpPoNumber: null, lineNumber: 1, isFirstLine: true, userName: "tester");

            Assert.Equal("", line.ORDNUM_10);
            Assert.Equal("", line.LINNUM_10);
            Assert.Equal("", line.DELNUM_10);
            Assert.Equal("", line.ORDER_10);
        }

        [Fact]
        public void BuildPurchaseOrderLine_SecondLine_SetsOrdnumLinnumDelnumAndComputesOrder()
        {
            var line = MaxPurchaseOrderRepository.BuildPurchaseOrderLine(
                "VEN01", BuildRequestLine(), BuildDraftLine(), partInfo: null, DefaultDueDate, erpPoNumber: "76012345", lineNumber: 2, isFirstLine: false, userName: "tester");

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
                "VEN01", BuildRequestLine(), BuildDraftLine(), partInfo: null, DefaultDueDate, erpPoNumber: "76012345", lineNumber: 3, isFirstLine: false, userName: "tester");

            Assert.Equal("03", line.LINNUM_10);
        }

        [Fact]
        public void BuildPurchaseOrderLine_UnitPrice_CastsDecimalToDoubleWithoutLoss()
        {
            var line = MaxPurchaseOrderRepository.BuildPurchaseOrderLine(
                "VEN01", BuildRequestLine(), BuildDraftLine(unitPrice: 0.115m), partInfo: null, DefaultDueDate, erpPoNumber: null, lineNumber: 1, isFirstLine: true, userName: "tester");

            Assert.Equal(0.115, line.FORCUR_10, 10);
        }

        [Fact]
        public void BuildPurchaseOrderLine_PassesThroughTheGivenDueDateUnchanged()
        {
            // The due-date calculation itself (MAX's own CalculatePODueDateFromMRPDueDate function)
            // happens before this method is called — BuildPurchaseOrderLine just wires the already-
            // resolved value straight onto CURDUE_10, no further adjustment of its own.
            var dueDate = new DateTime(2026, 9, 1);
            var line = MaxPurchaseOrderRepository.BuildPurchaseOrderLine(
                "VEN01", BuildRequestLine(), BuildDraftLine(), partInfo: null, dueDate, erpPoNumber: null, lineNumber: 1, isFirstLine: true, userName: "tester");

            Assert.Equal(dueDate, line.CURDUE_10);
        }

        [Fact]
        public void BuildPurchaseOrderLine_NoPartInfo_StockBlank_BuyerFallsBackTo000_PlannerBlank()
        {
            var line = MaxPurchaseOrderRepository.BuildPurchaseOrderLine(
                "VEN01", BuildRequestLine(), BuildDraftLine(), partInfo: null, DefaultDueDate, erpPoNumber: null, lineNumber: 1, isFirstLine: true, userName: "tester");

            Assert.Equal("", line.STK_10);
            Assert.Equal("000", line.BUYER_10);
            Assert.Equal("", line.PLANID_10);
        }

        [Fact]
        public void BuildPurchaseOrderLine_PartInfoProvided_CopiesStockBuyerPlannerFromPartMaster()
        {
            var partInfo = new MaxPartInfo { Stock = "MAIN", Buyer = "007", Planner = "PLN1" };
            var line = MaxPurchaseOrderRepository.BuildPurchaseOrderLine(
                "VEN01", BuildRequestLine(), BuildDraftLine(), partInfo, DefaultDueDate, erpPoNumber: null, lineNumber: 1, isFirstLine: true, userName: "tester");

            Assert.Equal("MAIN", line.STK_10);
            Assert.Equal("007", line.BUYER_10);
            Assert.Equal("PLN1", line.PLANID_10);
        }

        [Fact]
        public void BuildPurchaseOrderLine_PartInfoWithoutBuyer_FallsBackTo000()
        {
            var partInfo = new MaxPartInfo { Stock = "MAIN", Buyer = null, Planner = "PLN1" };
            var line = MaxPurchaseOrderRepository.BuildPurchaseOrderLine(
                "VEN01", BuildRequestLine(), BuildDraftLine(), partInfo, DefaultDueDate, erpPoNumber: null, lineNumber: 1, isFirstLine: true, userName: "tester");

            Assert.Equal("000", line.BUYER_10);
        }

        [Fact]
        public void BuildPurchaseOrderLine_NewPo_RevisionIsAlwaysZeroZeroZero()
        {
            var line = MaxPurchaseOrderRepository.BuildPurchaseOrderLine(
                "VEN01", BuildRequestLine(), BuildDraftLine(), partInfo: null, DefaultDueDate, erpPoNumber: null, lineNumber: 1, isFirstLine: true, userName: "tester");

            Assert.Equal("000", line.REVLEV_10);
        }

        [Fact]
        public void BuildPurchaseOrderLine_SetsCreatedAndModifiedByFromExplicitUserName()
        {
            var line = MaxPurchaseOrderRepository.BuildPurchaseOrderLine(
                "VEN01", BuildRequestLine(), BuildDraftLine(), partInfo: null, DefaultDueDate, erpPoNumber: null, lineNumber: 1, isFirstLine: true, userName: "j.doe");

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
