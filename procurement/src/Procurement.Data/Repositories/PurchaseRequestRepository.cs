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

        public Task<IReadOnlyList<PurchaseRequest>> GetOpenAsync() => _context.RunGuardedAsync(async () =>
        {
            var openStatuses = new[]
            {
                PurchaseRequestStatus.Pending,
                PurchaseRequestStatus.Sourcing,
                PurchaseRequestStatus.WaitingApproval,
                PurchaseRequestStatus.ReadyToOrder
            };

            IReadOnlyList<PurchaseRequest> result = await _context.PurchaseRequests
                .Include(pr => pr.Lines)
                .Where(pr => openStatuses.Contains(pr.Status))
                .OrderBy(pr => pr.Priority).ThenBy(pr => pr.RequestDate)
                .ToListAsync();
            return result;
        });

        public Task<IReadOnlyList<PurchaseRequest>> GetAllAsync() => _context.RunGuardedAsync(async () =>
        {
            IReadOnlyList<PurchaseRequest> result = await _context.PurchaseRequests
                .Include(pr => pr.Lines)
                .OrderByDescending(pr => pr.RequestDate)
                .ToListAsync();
            return result;
        });

        public Task<PurchaseRequest> GetByIdAsync(int id) => _context.RunGuardedAsync(() =>
            _context.PurchaseRequests
                .Include(pr => pr.Lines)
                .FirstOrDefaultAsync(pr => pr.Id == id));

        public Task<PurchaseRequest> AddAsync(PurchaseRequest request) => _context.RunGuardedAsync(async () =>
        {
            _context.PurchaseRequests.Add(request);
            await _context.SaveChangesAsync();
            return request;
        });

        public Task UpdateStatusAsync(int purchaseRequestId, PurchaseRequestStatus status) => _context.RunGuardedAsync(async () =>
        {
            var request = await _context.PurchaseRequests.FindAsync(purchaseRequestId);
            if (request == null) return;
            request.Status = status;
            await _context.SaveChangesAsync();
        });
    }
}
