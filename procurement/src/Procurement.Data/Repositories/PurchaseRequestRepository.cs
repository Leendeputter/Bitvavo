using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Procurement.Core.Entities;
using Procurement.Core.Enums;

namespace Procurement.Data.Repositories
{
    public class PurchaseRequestRepository
    {
        private readonly ProcurementDbContext _context;

        public PurchaseRequestRepository(ProcurementDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<PurchaseRequest>> GetOpenAsync()
        {
            var openStatuses = new[]
            {
                PurchaseRequestStatus.Pending,
                PurchaseRequestStatus.Sourcing,
                PurchaseRequestStatus.WaitingApproval,
                PurchaseRequestStatus.ReadyToOrder
            };

            return await _context.PurchaseRequests
                .Include(pr => pr.Lines)
                .Where(pr => openStatuses.Contains(pr.Status))
                .OrderBy(pr => pr.Priority).ThenBy(pr => pr.RequestDate)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<PurchaseRequest>> GetAllAsync()
        {
            return await _context.PurchaseRequests
                .Include(pr => pr.Lines)
                .OrderByDescending(pr => pr.RequestDate)
                .ToListAsync();
        }

        public async Task<PurchaseRequest> GetByIdAsync(int id)
        {
            return await _context.PurchaseRequests
                .Include(pr => pr.Lines)
                .FirstOrDefaultAsync(pr => pr.Id == id);
        }

        public async Task<PurchaseRequest> AddAsync(PurchaseRequest request)
        {
            _context.PurchaseRequests.Add(request);
            await _context.SaveChangesAsync();
            return request;
        }

        public async Task UpdateStatusAsync(int purchaseRequestId, PurchaseRequestStatus status)
        {
            var request = await _context.PurchaseRequests.FindAsync(purchaseRequestId);
            if (request == null) return;
            request.Status = status;
            await _context.SaveChangesAsync();
        }
    }
}
