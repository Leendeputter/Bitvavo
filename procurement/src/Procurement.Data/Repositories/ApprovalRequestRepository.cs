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

        public async Task<ApprovalRequest> AddAsync(ApprovalRequest request)
        {
            _context.ApprovalRequests.Add(request);
            await _context.SaveChangesAsync();
            return request;
        }

        public async Task<IReadOnlyList<ApprovalRequest>> GetPendingAsync()
        {
            return await _context.ApprovalRequests
                .Include(a => a.PurchaseRequestLine)
                .Include(a => a.ProposedOffer)
                .Where(a => a.Decision == ApprovalDecision.Pending)
                .OrderBy(a => a.CreatedAt)
                .ToListAsync();
        }

        public async Task DecideAsync(int id, ApprovalDecision decision, string comment, string decidedBy)
        {
            var request = await _context.ApprovalRequests.FindAsync(id);
            if (request == null) return;

            request.Decision = decision;
            request.Comment = comment;
            request.DecidedBy = decidedBy;
            request.DecidedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}
