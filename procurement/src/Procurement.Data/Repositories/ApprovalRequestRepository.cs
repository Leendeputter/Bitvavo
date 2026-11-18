using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Procurement.Core.Entities;
using Procurement.Core.Enums;

namespace Procurement.Data.Repositories
{
    public class ApprovalRequestRepository
    {
        private readonly ProcurementDbContext _context;

        public ApprovalRequestRepository(ProcurementDbContext context)
        {
            _context = context;
        }

        public Task<ApprovalRequest> AddAsync(ApprovalRequest request) => _context.RunGuardedAsync(async () =>
        {
            _context.ApprovalRequests.Add(request);
            await _context.SaveChangesAsync();
            return request;
        });

        public Task<IReadOnlyList<ApprovalRequest>> GetPendingAsync() => _context.RunGuardedAsync(async () =>
        {
            IReadOnlyList<ApprovalRequest> result = await _context.ApprovalRequests
                // .PurchaseRequest too (not just .PurchaseRequestLine) — ApprovalForm's grid shows
                // the PR number (user request, 20 okt 2026), which needs that one more hop.
                .Include(a => a.PurchaseRequestLine.PurchaseRequest)
                .Include(a => a.ProposedOffer)
                .Where(a => a.Decision == ApprovalDecision.Pending)
                .OrderBy(a => a.CreatedAt)
                .ToListAsync();
            return result;
        });

        /// <summary>Removes a line's undecided approval requests (a manual choice replaces the proposal; 18 nov 2026).</summary>
        public Task DeletePendingForLineAsync(int purchaseRequestLineId) => _context.RunGuardedAsync(async () =>
        {
            var pending = await _context.ApprovalRequests
                .Where(a => a.PurchaseRequestLineId == purchaseRequestLineId && a.Decision == ApprovalDecision.Pending)
                .ToListAsync();
            if (pending.Count == 0) return;
            _context.ApprovalRequests.RemoveRange(pending);
            await _context.SaveChangesAsync();
        });

        public Task DecideAsync(int id, ApprovalDecision decision, string comment, string decidedBy) => _context.RunGuardedAsync(async () =>
        {
            var request = await _context.ApprovalRequests.FindAsync(id);
            if (request == null) return;

            request.Decision = decision;
            request.Comment = comment;
            request.DecidedBy = decidedBy;
            request.DecidedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        });
    }
}
