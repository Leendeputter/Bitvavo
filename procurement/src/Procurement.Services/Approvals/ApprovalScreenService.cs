using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Procurement.Services.Approvals
{
    /// <summary>One row of the Approvals grid (same columns as the desktop's ApprovalForm).</summary>
    public class ApprovalRow
    {
        public int Id { get; set; }
        public string ErpRequestNumber { get; set; }
        public string PartID { get; set; }
        public string Part { get; set; }
        public string Supplier { get; set; }
        public int? OfferedQty { get; set; }
        public decimal? Price { get; set; }
        public string Reason { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Approvals (spec §8.3) for the web app (18 nov 2026): only lines that failed the auto-approve
    /// criteria. Same engine calls as the desktop's ApprovalForm; approving a bundled line approves
    /// the whole bundle (ProcurementEngine.ApproveAsync).
    /// </summary>
    public class ApprovalScreenService
    {
        private readonly ProcurementServices _services;

        public ApprovalScreenService(ProcurementServices services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public async Task<IReadOnlyList<ApprovalRow>> GetPendingAsync()
        {
            var pending = await _services.Engine.GetPendingApprovalsAsync();
            return pending.Select(a => new ApprovalRow
            {
                Id = a.Id,
                ErpRequestNumber = a.PurchaseRequestLine?.PurchaseRequest?.ErpRequestNumber,
                PartID = a.PurchaseRequestLine?.ErpArticleId,
                Part = a.PurchaseRequestLine?.ManufacturerPartNumber,
                Supplier = a.ProposedOffer?.SupplierCode,
                OfferedQty = a.ProposedOffer?.OfferedQuantity,
                Price = a.ProposedOffer?.UnitPrice,
                Reason = a.Reasons,
                CreatedAt = a.CreatedAt
            }).ToList();
        }

        /// <summary>Recorded under the logged-in user (the desktop records the Windows user, which is the same account).</summary>
        public Task DecideAsync(int approvalId, bool approve, string comment) =>
            _services.Engine.ApproveAsync(approvalId, approve, comment, _services.Session.UserName);

        /// <summary>
        /// Several approvals at once (19 nov 2026, user: "meerdere regels selecteren in 1x te Approven").
        /// Deciding one line of a bundle already decides its other lines, so an id that is no longer
        /// pending by the time it's reached is counted as done, not as an error. One failure doesn't stop the rest.
        /// </summary>
        public async Task<string> DecideManyAsync(IReadOnlyList<int> approvalIds, bool approve, string comment)
        {
            int decided = 0, viaBundle = 0, failed = 0;
            string firstError = null;
            foreach (var id in approvalIds.Distinct())
            {
                var stillPending = (await _services.Engine.GetPendingApprovalsAsync()).Any(a => a.Id == id);
                if (!stillPending) { viaBundle++; continue; }
                try
                {
                    await DecideAsync(id, approve, comment);
                    decided++;
                }
                catch (Exception ex)
                {
                    failed++;
                    if (firstError == null) firstError = ex.Message;
                }
            }

            var text = $"{decided} regel(s) {(approve ? "goedgekeurd" : "afgewezen")}";
            if (viaBundle > 0) text += $", {viaBundle} al meegenomen via de bundel";
            if (failed > 0) text += $", {failed} mislukt (eerste fout: {firstError})";
            return text + ".";
        }
    }
}
