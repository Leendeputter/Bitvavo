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
                PurchaseRequestStatus.ReadyToOrder,
                // Exception is a failed sourcing attempt (e.g. a transient supplier API error), not
                // a resolved request — it needs to stay visible/selectable so the existing "worth
                // retrying" re-sourcing path (see MainForm.SourceRequestsAsync) is actually reachable.
                // Only Ordered means the request is genuinely done.
                PurchaseRequestStatus.Exception
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

        /// <summary>PurchaseRequestLine has no repository of its own — used by MaxPurchaseOrderRepository to resolve MAX-native fields (ErpArticleId, StockId, MaxLineNumber/MaxDeliveryNumber, ...) for a line that only carries PurchaseRequestLineId on its PurchaseOrderDraftLine. Eager-loads PurchaseRequest since MaxPurchaseOrderRepository also needs its ErpRequestNumber (Order_Master.ORDNUM_10) for the original PR's composite key.</summary>
        public Task<PurchaseRequestLine> GetLineByIdAsync(int purchaseRequestLineId) => _context.RunGuardedAsync(() =>
            _context.PurchaseRequestLines
                .Include(l => l.PurchaseRequest)
                .FirstOrDefaultAsync(l => l.PurchaseRequest.CompanyId == _session.CompanyId && l.Id == purchaseRequestLineId));

        /// <summary>
        /// Sourcing per article (user decision, 18 nov 2026): every line of this article that is still
        /// open for sourcing (request not ReadyToOrder/Ordered, no selection, not on a PO), with its
        /// request, earliest required date first.
        /// </summary>
        public Task<IReadOnlyList<PurchaseRequestLine>> GetOpenLinesForArticleAsync(string erpArticleId) => _context.RunGuardedAsync(async () =>
        {
            var statuses = new[]
            {
                PurchaseRequestStatus.Pending,
                PurchaseRequestStatus.Sourcing,
                PurchaseRequestStatus.WaitingApproval,
                PurchaseRequestStatus.Exception
            };

            IReadOnlyList<PurchaseRequestLine> result = await _context.PurchaseRequestLines
                .Include(l => l.PurchaseRequest)
                .Where(l => l.ErpArticleId == erpArticleId
                            && l.PurchaseRequest.CompanyId == _session.CompanyId
                            && statuses.Contains(l.PurchaseRequest.Status)
                            && !_context.SupplierSelections.Any(s => s.PurchaseRequestLineId == l.Id)
                            && !_context.PurchaseOrderLines.Any(o => o.PurchaseRequestLineId == l.Id))
                .OrderBy(l => l.RequiredDate ?? System.DateTime.MaxValue)
                .ThenBy(l => l.Id)
                .ToListAsync();
            return result;
        });

        /// <summary>
        /// Every request of this company (with lines), keyed by ErpRequestNumber — one query instead of
        /// one FindByErpRequestNumberAsync per MAX order (17 nov 2026: "Fetch Orders from MAX" was slow
        /// because it did a separate lookup, and a separate save, for every order in the batch).
        /// </summary>
        public Task<Dictionary<string, PurchaseRequest>> GetAllByErpRequestNumberAsync() => _context.RunGuardedAsync(async () =>
        {
            var all = await _context.PurchaseRequests
                .Include(pr => pr.Lines)
                .Where(pr => pr.CompanyId == _session.CompanyId && pr.ErpRequestNumber != null)
                .ToListAsync();
            return all
                .GroupBy(pr => pr.ErpRequestNumber)
                .ToDictionary(g => g.Key, g => g.OrderBy(pr => pr.Id).First());
        });

        /// <summary>Adds several requests with a single SaveChanges.</summary>
        public Task AddRangeAsync(IReadOnlyCollection<PurchaseRequest> requests) => _context.RunGuardedAsync(async () =>
        {
            if (requests.Count == 0) return;
            foreach (var request in requests)
                request.CompanyId = _session.CompanyId;
            _context.PurchaseRequests.AddRange(requests);
            await _context.SaveChangesAsync();
        });

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

        /// <summary>
        /// Hard-deletes a purchase request and everything sourcing/approval ever attached to any of
        /// its lines (ApprovalRequest, SupplierSelection, SupplierOffer — in that order, none of
        /// which cascade at the DB level: every one of those FKs is deliberately
        /// WillCascadeOnDelete(false), so a bare PurchaseRequests.Remove would just throw a
        /// constraint violation once a line has been sourced/approved), then the lines, then the
        /// request itself. Used by MaxErpConnector when a Query confirms the source MAX PR no longer
        /// exists (MAX is leading) — never call this for a request whose workflow Status is Ordered:
        /// that PR was deliberately removed from MAX by this app's own order-placement step, so its
        /// local PurchaseOrder/PurchaseOrderLine history must be kept, not purged along with it.
        /// </summary>
        public Task DeleteAsync(PurchaseRequest request) => _context.RunGuardedAsync(async () =>
        {
            var lineIds = request.Lines.Select(l => l.Id).ToList();

            var approvals = await _context.ApprovalRequests.Where(a => lineIds.Contains(a.PurchaseRequestLineId)).ToListAsync();
            var selections = await _context.SupplierSelections.Where(s => lineIds.Contains(s.PurchaseRequestLineId)).ToListAsync();
            var offers = await _context.SupplierOffers.Where(o => lineIds.Contains(o.PurchaseRequestLineId)).ToListAsync();

            _context.ApprovalRequests.RemoveRange(approvals);
            _context.SupplierSelections.RemoveRange(selections);
            _context.SupplierOffers.RemoveRange(offers);
            _context.PurchaseRequestLines.RemoveRange(request.Lines);
            _context.PurchaseRequests.Remove(request);

            await _context.SaveChangesAsync();
        });
    }
}
