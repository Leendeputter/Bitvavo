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
    }
}
