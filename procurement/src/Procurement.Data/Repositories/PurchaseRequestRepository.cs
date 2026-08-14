using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Procurement.Core.Entities;
using Procurement.Core.Enums;
using Procurement.Core.Interfaces;

namespace Procurement.Data.Repositories
{
    public class PurchaseRequestRepository
    {
        private readonly ProcurementDbContext _context;
        private readonly ISessionContext _session;

        public PurchaseRequestRepository(ProcurementDbContext context, ISessionContext session)
        {
            _context = context;
            _session = session;
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
                .Where(pr => pr.CompanyId == _session.CompanyId && openStatuses.Contains(pr.Status))
                .OrderBy(pr => pr.Priority).ThenBy(pr => pr.RequestDate)
                .ToListAsync();
            return result;
        });

        public Task<IReadOnlyList<PurchaseRequest>> GetAllAsync() => _context.RunGuardedAsync(async () =>
        {
            IReadOnlyList<PurchaseRequest> result = await _context.PurchaseRequests
                .Include(pr => pr.Lines)
                .Where(pr => pr.CompanyId == _session.CompanyId)
                .OrderByDescending(pr => pr.RequestDate)
                .ToListAsync();
            return result;
        });

        public Task<PurchaseRequest> GetByIdAsync(int id) => _context.RunGuardedAsync(() =>
            _context.PurchaseRequests
                .Include(pr => pr.Lines)
                .FirstOrDefaultAsync(pr => pr.Id == id && pr.CompanyId == _session.CompanyId));

        /// <summary>Used to dedup when syncing an external feed (e.g. MaxErpConnector) — avoids re-inserting a request that's already been synced for this company.</summary>
        public Task<PurchaseRequest> FindByErpRequestNumberAsync(string erpRequestNumber) => _context.RunGuardedAsync(() =>
            _context.PurchaseRequests
                .Include(pr => pr.Lines)
                .FirstOrDefaultAsync(pr => pr.CompanyId == _session.CompanyId && pr.ErpRequestNumber == erpRequestNumber));

        /// <summary>PurchaseRequestLine has no repository of its own — used by MaxPurchaseOrderRepository to resolve MAX-native fields (ErpArticleId, StockId, ...) for a line that only carries PurchaseRequestLineId on its PurchaseOrderDraftLine.</summary>
        public Task<PurchaseRequestLine> GetLineByIdAsync(int purchaseRequestLineId) => _context.RunGuardedAsync(() =>
            _context.PurchaseRequestLines
                .FirstOrDefaultAsync(l => l.PurchaseRequest.CompanyId == _session.CompanyId && l.Id == purchaseRequestLineId));

        public Task<PurchaseRequest> AddAsync(PurchaseRequest request) => _context.RunGuardedAsync(async () =>
        {
            request.CompanyId = _session.CompanyId;
            _context.PurchaseRequests.Add(request);
            await _context.SaveChangesAsync();
            return request;
        });

        public Task UpdateStatusAsync(int purchaseRequestId, PurchaseRequestStatus status) => _context.RunGuardedAsync(async () =>
        {
            var request = await _context.PurchaseRequests.FindAsync(purchaseRequestId);
            if (request == null || request.CompanyId != _session.CompanyId) return;
            request.Status = status;
            await _context.SaveChangesAsync();
        });

        /// <summary>Flushes pending changes on entities already tracked by this context (e.g. a PurchaseRequest/Lines instance returned earlier by FindByErpRequestNumberAsync and mutated in place) — used by MaxErpConnector to refresh display-only MAX fields on an already-synced request without re-fetching it.</summary>
        public Task SaveAsync() => _context.RunGuardedAsync(async () => { await _context.SaveChangesAsync(); });
    }
}
