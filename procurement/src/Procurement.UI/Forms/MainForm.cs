using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Procurement.Core.BusinessLogic;
using Procurement.Core.Entities;
using Procurement.Core.Enums;
using Procurement.Erp;
using Procurement.Suppliers.Farnell;
using Procurement.UI.Composition;
using static Procurement.UI.Support.GridFormatting;
using Procurement.UI.Support;

namespace Procurement.UI.Forms
{
    /// <summary>
    /// Purchase Requests-overzicht (spec §8.1) — the application's main work screen.
    /// Built with the Visual Studio Designer (user request, 16 nov 2026): toolbar, the four filter
    /// group boxes, both grids and the status line are in MainForm.Designer.cs; grid columns come
    /// from the DataSource and are formatted here. No Fill columns (user rule, 16 nov 2026).
    /// </summary>
    public partial class MainForm : Form
    {
        private readonly CompositionRoot _composition;

        // The one PurchaseRequestLine the offers grid currently shows — set whenever
        // LoadOffersForSelectedRequestAsync (re)loads, read when a "Select" checkbox is ticked.
        // Every MAX-synced PR has exactly one line (see PopulateRequestsGrid's own comment), so the
        // offers grid never needs to represent more than one line's offers at a time.
        private int? _currentLineId;

        // "Display" group — which of this prototype's own Workflow statuses to show in the grid.
        // Purely a local display filter (never touches MAX). Ordered has no checkbox (user request,
        // 20 okt 2026: placed orders belong in the separate Orders window). The checkboxes themselves
        // are in the Designer; this maps them to their status.
        private readonly Dictionary<PurchaseRequestStatus, CheckBox> _requestStatusCheckBoxes = new Dictionary<PurchaseRequestStatus, CheckBox>();

        // Every action button shares this one "an operation is in flight" gate (SetBusy) instead of
        // each handler only disabling itself — with only the triggering button disabled, every other
        // button stayed clickable during a Query/sourcing/etc., so e.g. clicking "Orderbevestiging
        // verwerken" while a Query was still running fired two overlapping operations against the
        // same MAX SqlConnections (those aren't behind the ProcurementDbContext gate that serializes
        // EF6 access — see ProcurementDbContext.RunGuardedAsync), and the query could get interrupted
        // outright — reported directly by the user, not a hypothetical.
        private bool _isBusy;

        public MainForm(CompositionRoot composition)
        {
            _composition = composition ?? throw new ArgumentNullException(nameof(composition));
            InitializeComponent();
            ClassicStyle.ApplyClassicStyle(this);
            this.BackColor = ClassicStyle.ClassicScreenBackColor;
            EnableRowNumberHeaders(dgv_requests);
            EnableRowNumberHeaders(dgv_offers);

            // Quantity on an offer is editable (18 nov 2026); accept "25.000" as well as "25000".
            dgv_offers.CellParsing += OffersGrid_CellParsing;

            // ShellForm carries "Component Procurement" with company/user; this MDI child just gets its screen name.
            Text = "Purchase Requests" + (_composition.Session.TestMode ? " – TEST MODE" : string.Empty);

            _requestStatusCheckBoxes[PurchaseRequestStatus.Pending] = chk_statusPending;
            _requestStatusCheckBoxes[PurchaseRequestStatus.Sourcing] = chk_statusSourcing;
            _requestStatusCheckBoxes[PurchaseRequestStatus.WaitingApproval] = chk_statusWaitingApproval;
            _requestStatusCheckBoxes[PurchaseRequestStatus.ReadyToOrder] = chk_statusReadyToOrder;
            _requestStatusCheckBoxes[PurchaseRequestStatus.Exception] = chk_statusException;

            // MAX Order_Master.STATUS_10 filter (1 = Planned, 2 = Approved; default only Approved).
            UpdateIncludedOrderStatuses();
            cb_rangeField.SelectedIndex = 0;

            // Ordering switched off in App.config (Ordering.Enabled): search/select only.
            if (!_composition.OrderingEnabled)
            {
                bt_placeOrders.Enabled = false;
                bt_placeOrders.Text = "Place Order (off)";
            }

            GridColumnSettingsHelper.Attach(this); // remembers column widths per user, like UniPro
            // Nothing loads automatically on open — the grid stays empty until the user clicks "Query".
        }

        // Each MAX order is a request for exactly one article; "Start Sourcing" handles every selected
        // row (Ctrl/Shift-click), "Source All" the whole list. Placing the order is a separate step,
        // so orders can be bundled per supplier. "Process Order Confirmations" handles all
        // confirmations at once and only shows the deviations.
        private async void bt_newRequest_Click(object sender, EventArgs e) => await NewRequestAsync();

        private async void bt_startSourcing_Click(object sender, EventArgs e) => await SourceRequestsAsync(GetSelectedRequestIds(), confirmIfMoreThan: 1);

        private async void bt_sourceAll_Click(object sender, EventArgs e) => await SourceRequestsAsync(GetAllRequestIds(), confirmIfMoreThan: 1);

        private async void bt_placeOrders_Click(object sender, EventArgs e) => await PlaceOrdersAsync(GetSelectedRequestIds());

        private async void bt_processConfirmations_Click(object sender, EventArgs e) => await ProcessOrderConfirmationsAsync();

        private async void bt_query_Click(object sender, EventArgs e) => await QueryOrdersAsync();

        private void chk_dueDateEnable_CheckedChanged(object sender, EventArgs e)
        {
            dtp_dueDateStart.Enabled = chk_dueDateEnable.Checked;
            dtp_dueDateEnd.Enabled = chk_dueDateEnable.Checked;
        }

        // Shared by the five status checkboxes and "Show Test Requests" (off by default: manual
        // TEST- requests are exceptions, user request 20 okt 2026). Re-renders the local data only.
        private async void chk_displayFilter_CheckedChanged(object sender, EventArgs e) => await ReloadWithCurrentStatusFilterAsync();

        // Standard WinForms trick for a checkbox column: commit on click, so CellValueChanged fires right away.
        private void dgv_offers_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            // Only the checkbox commits immediately; the quantity cell commits when editing ends.
            if (dgv_offers.IsCurrentCellDirty && dgv_offers.CurrentCell?.OwningColumn?.Name == "Select")
                dgv_offers.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private async void dgv_offers_CellValueChanged(object sender, DataGridViewCellEventArgs e) => await OffersGrid_CellValueChangedAsync(e);

        private HashSet<PurchaseRequestStatus> GetCheckedRequestStatuses() =>
            new HashSet<PurchaseRequestStatus>(_requestStatusCheckBoxes.Where(kv => kv.Value.Checked).Select(kv => kv.Key));

        /// <summary>Re-renders whatever's already locally synced with the current checkbox selection — never touches MAX, so this is safe/cheap to run on every checkbox toggle.</summary>
        private async System.Threading.Tasks.Task ReloadWithCurrentStatusFilterAsync()
        {
            if (_isBusy) return;
            SetBusy(true);
            try
            {
                await LoadLocalRequestsAsync();
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void UpdateIncludedOrderStatuses()
        {
            var statuses = new HashSet<string>();
            if (chk_statusPlanned.Checked) statuses.Add("1");
            if (chk_statusApproved.Checked) statuses.Add("2");
            _composition.ErpConnector.IncludedOrderStatuses = statuses;
        }

        /// <summary>
        /// Laadt enkel de al lokaal gesynchroniseerde aanvragen — raakt MAX niet aan. Gefilterd op de
        /// "Weergeven"-checkboxen (welke Workflow-statussen getoond worden) én op de huidige Due Date
        /// Range/Select By-instelling (zelfde filter als "Query" tegen MAX toepast, hier lokaal
        /// nagebouwd — zie ApplyRangeAndDueDateFilters) zodat een checkbox omzetten niet alsnog alles
        /// laat verschijnen dat net met "Query" buiten beeld was gefilterd. Handmatige testaanvragen
        /// blijven, net als bij GetOpenPurchaseRequestsAsync, altijd zichtbaar ongeacht dat filter —
        /// zij waren nooit onderdeel van enige MAX-query. Gebruikt na lokale wijzigingen (nieuwe
        /// testaanvraag, sourcing, goedkeuringen) en bij het wijzigen van de statusfilter zelf, niet
        /// bij het openen van het scherm.
        /// </summary>
        private async System.Threading.Tasks.Task LoadLocalRequestsAsync()
        {
            SetStatus("Aanvragen laden...");
            var statuses = GetCheckedRequestStatuses();
            var allRequests = await _composition.PurchaseRequestRepository.GetAllAsync();
            var requests = ApplyRangeAndDueDateFilters(allRequests.Where(r => statuses.Contains(r.Status)))
                .Where(r => chk_showTestRequests.Checked || !IsManualTestRequest(r.ErpRequestNumber))
                .ToList();
            await PopulateRequestsGrid(requests);
            SetStatus($"{requests.Count} aanvraag/aanvragen getoond (statusfilter).");
            await LoadOffersForSelectedRequestAsync();
        }

        /// <summary>
        /// Client-side herbouw van het Due Date Range/Select By-filter dat ApplyFiltersToErpConnector
        /// op MAX-niveau doorgeeft aan MaxOrderRepository.GetOpenOrdersAsync — nodig omdat
        /// LoadLocalRequestsAsync (anders dan RefreshRequestsAsync/"Query") nooit MAX raakt, dus moet
        /// hetzelfde filter hier lokaal nagebootst worden, anders verdwijnt het zodra een
        /// "Weergeven"-checkbox wordt omgezet. Handmatige testaanvragen ("TEST-"-prefix) slaan dit
        /// filter altijd over, consistent met GetOpenPurchaseRequestsAsync's eigen
        /// IsManualTestRequest-uitzondering.
        /// </summary>
        private IReadOnlyList<PurchaseRequest> ApplyRangeAndDueDateFilters(IEnumerable<PurchaseRequest> requests)
        {
            IEnumerable<PurchaseRequest> result = requests.ToList();

            if (chk_dueDateEnable.Checked)
            {
                var start = dtp_dueDateStart.Value.Date;
                var end = dtp_dueDateEnd.Value.Date;
                result = result.Where(r => IsManualTestRequest(r.ErpRequestNumber) ||
                    (r.RequiredDate.HasValue && r.RequiredDate.Value.Date >= start && r.RequiredDate.Value.Date <= end));
            }

            var rangeStart = txt_rangeStart.Text.Trim();
            var rangeEnd = txt_rangeEnd.Text.Trim();
            if (!string.IsNullOrEmpty(rangeStart) || !string.IsNullOrEmpty(rangeEnd))
            {
                var field = GetSelectedRangeField();
                result = result.Where(r => IsManualTestRequest(r.ErpRequestNumber) || MatchesRange(r, field, rangeStart, rangeEnd));
            }

            return result.ToList();
        }

        private static bool IsManualTestRequest(string erpRequestNumber) =>
            !string.IsNullOrEmpty(erpRequestNumber) && erpRequestNumber.StartsWith("TEST-", StringComparison.Ordinal);

        private static bool MatchesRange(PurchaseRequest request, MaxOrderRangeField field, string rangeStart, string rangeEnd)
        {
            string value;
            switch (field)
            {
                case MaxOrderRangeField.Part:
                    value = request.Lines?.FirstOrDefault()?.ErpArticleId;
                    break;
                case MaxOrderRangeField.Customer:
                    value = request.Lines?.FirstOrDefault()?.Customer;
                    break;
                default:
                    value = request.ErpRequestNumber;
                    break;
            }

            if (string.IsNullOrEmpty(value)) return false;
            if (!string.IsNullOrEmpty(rangeStart) && string.CompareOrdinal(value, rangeStart) < 0) return false;
            if (!string.IsNullOrEmpty(rangeEnd) && string.CompareOrdinal(value, rangeEnd) > 0) return false;
            return true;
        }

        /// <summary>Vraagt open orders op uit MAX (Order_Master/Part_Master, incl. Part_Vendor-mapping sync) met de huidige filterinstellingen, en synchroniseert ze naar lokale aanvragen — enkel aangeroepen via de "Query"-knop.</summary>
        private async System.Threading.Tasks.Task RefreshRequestsAsync()
        {
            SetStatus("Orders opvragen uit MAX...");
            ApplyFiltersToErpConnector();
            var requests = await _composition.Engine.GetOpenPurchaseRequestsAsync();
            // Dezelfde "Weergeven"-statusfilter als LoadLocalRequestsAsync, voor consistent gedrag
            // ongeacht hoe het grid gevuld werd. Ordered zou hier toch nooit iets opleveren, ook los
            // van het ontbreken van een checkbox ervoor (zie BuildRequestStatusFilterGroup):
            // GetOpenPurchaseRequestsAsync synchroniseert MAX-orders en geeft enkel nog-openstaande
            // aanvragen terug (Query = "wat moet er nog gesourced worden") — een al bestelde aanvraag
            // bekijk je voortaan via het aparte "Orders..."-scherm.
            var statuses = GetCheckedRequestStatuses();
            var filteredRequests = requests.Where(r => statuses.Contains(r.Status))
                .Where(r => chk_showTestRequests.Checked || !IsManualTestRequest(r.ErpRequestNumber))
                .ToList();
            await PopulateRequestsGrid(filteredRequests);
            // Toont ook hoeveel orders de MAX-query zelf teruggaf (voor het filter toepassen op de
            // lokale lijst) — handig om te zien of een filter al op MAX-niveau iets doet, los van
            // hoeveel er uiteindelijk in de grid staan.
            SetStatus($"{filteredRequests.Count} aanvraag/aanvragen getoond ({_composition.ErpConnector.LastMaxOrderCount} orders uit MAX opgehaald met huidig filter).");
            await LoadOffersForSelectedRequestAsync();
        }

        private void ApplyFiltersToErpConnector()
        {
            UpdateIncludedOrderStatuses();

            _composition.ErpConnector.DueDateFilterStart = chk_dueDateEnable.Checked ? dtp_dueDateStart.Value.Date : (DateTime?)null;
            _composition.ErpConnector.DueDateFilterEnd = chk_dueDateEnable.Checked ? dtp_dueDateEnd.Value.Date : (DateTime?)null;

            var rangeStart = txt_rangeStart.Text.Trim();
            var rangeEnd = txt_rangeEnd.Text.Trim();
            var hasRange = !string.IsNullOrEmpty(rangeStart) || !string.IsNullOrEmpty(rangeEnd);

            _composition.ErpConnector.RangeFilterField = hasRange ? GetSelectedRangeField() : (MaxOrderRangeField?)null;
            _composition.ErpConnector.RangeFilterStart = string.IsNullOrEmpty(rangeStart) ? null : rangeStart;
            _composition.ErpConnector.RangeFilterEnd = string.IsNullOrEmpty(rangeEnd) ? null : rangeEnd;
        }

        private MaxOrderRangeField GetSelectedRangeField()
        {
            var text = cb_rangeField.SelectedItem as string;
            if (text == "Customer") return MaxOrderRangeField.Customer;
            if (text == "Part") return MaxOrderRangeField.Part;
            return MaxOrderRangeField.OrderNumber;
        }

        private async System.Threading.Tasks.Task QueryOrdersAsync()
        {
            if (_isBusy) return;
            SetBusy(true);
            try
            {
                await RefreshRequestsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Fout bij opvragen van orders", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async System.Threading.Tasks.Task PopulateRequestsGrid(IReadOnlyList<PurchaseRequest> requests)
        {
            // Kolomvolgorde/-namen op verzoek: Order, Workflow, Status, Firm, Type, PartID, Rev,
            // Desc1, Desc2, Quantity, Cost, Cnv, DueDate, Reference, Manufacturing Part, Customer,
            // StockID, dan de gekozen-offerte-kolommen — Order/Status/etc. zoals in de MAX
            // Order_Master/Part_Master-query, Workflow bewust vooraan (naast Order) i.p.v. achteraan
            // zodat je in één oogopslag ziet waar een aanvraag staat zonder helemaal naar rechts te
            // hoeven scrollen. Elke MAX-order sync't naar precies één PurchaseRequestLine (zie
            // MaxErpConnector), dus de eerste regel volstaat hier; handmatige testaanvragen hebben
            // ook altijd precies één regel.
            var selectionsByLineId = await GetSelectionsByLineIdAsync(requests.Select(r => r.Lines.FirstOrDefault()?.Id));
            var poDueDatesByKey = await GetPoDueDatesByKeyAsync(requests.Select(r => r.Lines.FirstOrDefault()));
            var preferredVendorIdsByPartId = await _composition.MaxOrderRepository.GetPreferredVendorIdsByPartIdsAsync(
                requests.Select(r => r.Lines.FirstOrDefault()?.ErpArticleId).Where(p => !string.IsNullOrEmpty(p)).ToList());

            var rows = requests
                .Select(r =>
                {
                    var line = r.Lines.FirstOrDefault();
                    var selection = line != null && selectionsByLineId.TryGetValue(line.Id, out var s) ? s : null;
                    var selectedOffer = selection?.SelectedOffer;
                    return new
                    {
                        Id = r.Id,
                        Order = r.ErpRequestNumber,
                        // Niet uit de MAX-query — dit is dit prototype's eigen workflow-status
                        // (Pending/Sourcing/WaitingApproval/ReadyToOrder/Ordered/Exception), los van
                        // de ruwe MAX Order_Master.STATUS_10 die in de "Status"-kolom staat.
                        Workflow = r.Status.ToString(),
                        Status = r.MaxOrderStatus,
                        Firm = line?.Firm ?? false,
                        Type = line?.PartType,
                        PartID = line?.ErpArticleId,
                        Rev = line?.Revision,
                        Desc1 = line?.Desc1,
                        Desc2 = line?.Desc2,
                        Quantity = line?.RequestedQuantity ?? 0,
                        Cost = line?.Cost,
                        Cnv = line?.CostConv,
                        // Blank (null) rather than 0 or an exception when Cost/CostConv niet allebei
                        // bekend zijn of CostConv 0 is — anders zou een deling door nul of een
                        // stille 0-waarde een verkeerde indruk geven.
                        ExtCost = ComputeExtCost(line?.RequestedQuantity ?? 0, line?.Cost, line?.CostConv),
                        DueDate = r.RequiredDate,
                        // MAX's own CalculateMRPDueDateFromPODueDate applied to DueDate above — the
                        // date to actually hand the supplier, were a PO created for this line right
                        // now (see MaxOrderRepository.CalculatePoDueDatesAsync and the class comment
                        // on MaxPurchaseOrderRepository). Blank if DueDate or PartID is unknown.
                        PoDueDate = GetPoDueDate(line, poDueDatesByKey),
                        Reference = r.Project,
                        ManufacturingPart = line?.ManufacturerPartNumber,
                        Customer = line?.Customer,
                        StockID = line?.StockId,
                        // Part_Master.RECVEN_01 — confirmed by the user (okt 2026) as "the article's
                        // preferred vendor". Live lookup, not synced/stored on the line itself (see
                        // MaxOrderRepository.GetPreferredVendorIdsByPartIdsAsync's own comment for why).
                        // Column header shown as "Rec. Vendor" (user request, 20 okt 2026).
                        RecVendor = line?.ErpArticleId != null && preferredVendorIdsByPartId.TryGetValue(line.ErpArticleId, out var preferredVendorId)
                            ? preferredVendorId
                            : null,
                        // De oorspronkelijke MAX-aanvraag hierboven blijft ongewijzigd zichtbaar (dat
                        // is wat er gevraagd is) — deze vier tonen apart wat er daadwerkelijk gekozen
                        // is bij sourcing, leeg zolang er nog geen (automatische of handmatige)
                        // SupplierSelection is. Zie "Offers bekijken" voor alle offertes, niet alleen
                        // de gekozen.
                        SelectedSupplier = selectedOffer?.SupplierCode,
                        SelectedPrice = selectedOffer?.UnitPrice,
                        SelectedQuantity = (int?)selectedOffer?.OfferedQuantity,
                        SelectedDueDate = selectedOffer?.EstimatedDeliveryDate
                    };
                })
                .ToList();

            // Assigning DataSource can itself raise SelectionChanged (when the grid picks a
            // default current cell); detach first so that doesn't race with the explicit reload
            // below against the same shared DbContext.
            // Sortable by clicking a column header (user request, 17 nov 2026); the current sort is
            // re-applied after a refresh, so a Query or Source run keeps the order.
            dgv_requests.SelectionChanged -= dgv_requests_SelectionChanged;
            BindSortable(dgv_requests, rows);
            dgv_requests.SelectionChanged += dgv_requests_SelectionChanged;

            if (dgv_requests.Columns["Id"] != null)
                dgv_requests.Columns["Id"].Visible = false;
            SetHeaderText(dgv_requests, "ManufacturingPart", "Manufacturing Part");
            SetHeaderText(dgv_requests, "ExtCost", "Ext. Cost");
            SetHeaderText(dgv_requests, "PoDueDate", "PO DueDate");
            SetHeaderText(dgv_requests, "RecVendor", "Rec. Vendor");
            SetHeaderText(dgv_requests, "SelectedSupplier", "Selected Supplier");
            SetHeaderText(dgv_requests, "SelectedPrice", "Selected Price");
            SetHeaderText(dgv_requests, "SelectedQuantity", "Selected Qty");
            SetHeaderText(dgv_requests, "SelectedDueDate", "Selected DueDate");
            ApplyCurrencyColumns(dgv_requests, "SelectedPrice");
            ApplyQuantityColumns(dgv_requests, "SelectedQuantity");
            ApplyDateColumns(dgv_requests, "SelectedDueDate");
            ApplyMaxColumnWidths(dgv_requests);
            ApplyFrozenColumns(dgv_requests,
                "Order", "Workflow", "PartID", "Desc1", "Quantity", "Cost", "DueDate", "PoDueDate",
                "SelectedSupplier", "SelectedPrice", "SelectedQuantity", "SelectedDueDate");
        }

        /// <summary>
        /// One SupplierSelection lookup per line (never more than one active selection per line —
        /// SupplierSelectionRepository.GetByLineIdAsync already picks the latest by SelectedAt), with
        /// SelectedOffer eager-loaded so SupplierCode/UnitPrice/OfferedQuantity/EstimatedDeliveryDate
        /// are available without a further round-trip per row. Sequential, not parallel — same
        /// reasoning as SourceRequestsAsync: this shares one ProcurementDbContext serialized through
        /// RunGuardedAsync, so concurrent calls wouldn't save time, only make a mid-refresh failure
        /// harder to attribute to a specific line. Fine at prototype scale (one line per request, see
        /// PopulateRequestsGrid's own comment).
        /// </summary>
        private async System.Threading.Tasks.Task<Dictionary<int, SupplierSelection>> GetSelectionsByLineIdAsync(IEnumerable<int?> lineIds)
        {
            var result = new Dictionary<int, SupplierSelection>();
            foreach (var lineId in lineIds)
            {
                if (lineId == null || result.ContainsKey(lineId.Value)) continue;
                var selection = await _composition.Engine.GetSelectionForLineAsync(lineId.Value);
                if (selection != null)
                    result[lineId.Value] = selection;
            }
            return result;
        }

        /// <summary>Ext. Cost voor een regel = Quantity * Cost / Cost Conv.</summary>
        private static decimal? ComputeExtCost(int quantity, decimal? cost, decimal? costConv)
        {
            if (!cost.HasValue || !costConv.HasValue || costConv.Value == 0m) return null;
            return quantity * cost.Value / costConv.Value;
        }

        /// <summary>
        /// One batched MAX round-trip (MaxOrderRepository.CalculatePoDueDatesAsync — MAX's own
        /// CalculateMRPDueDateFromPODueDate SQLCLR function, see MaxPurchaseOrderRepository's class
        /// comment) for every distinct (RequiredDate, PartID) pair among the given lines, instead of
        /// one query per grid row. Lines without both a RequiredDate and a PartID are skipped — the
        /// function needs both arguments — and simply show a blank PoDueDate (see GetPoDueDate).
        /// </summary>
        private async System.Threading.Tasks.Task<IReadOnlyDictionary<(DateTime MrpDate, string PartNumber), DateTime>> GetPoDueDatesByKeyAsync(
            IEnumerable<PurchaseRequestLine> lines)
        {
            var items = lines
                .Where(l => l != null && l.RequiredDate.HasValue && !string.IsNullOrEmpty(l.ErpArticleId))
                .Select(l => (l.RequiredDate.Value, l.ErpArticleId))
                .ToList();
            return await _composition.MaxOrderRepository.CalculatePoDueDatesAsync(items);
        }

        private static DateTime? GetPoDueDate(
            PurchaseRequestLine line,
            IReadOnlyDictionary<(DateTime MrpDate, string PartNumber), DateTime> poDueDatesByKey)
        {
            if (line == null || !line.RequiredDate.HasValue || string.IsNullOrEmpty(line.ErpArticleId)) return null;
            return poDueDatesByKey.TryGetValue((line.RequiredDate.Value, line.ErpArticleId), out var poDueDate)
                ? poDueDate
                : (DateTime?)null;
        }

        /// <summary>
        /// Column widths for the requests grid (see PopulateRequestsGrid) — used to be shared with
        /// the old "lines of selected request" grid too, before that was replaced by the offers grid
        /// (LoadOffersForSelectedRequestAsync, which has its own, much smaller column set and doesn't
        /// call this). Every column named here gets a fixed width (no Fill since 16 nov 2026, user
        /// rule: columns must not stretch when the window is maximized). SetHeaderText/SetFixedColumnWidth calls for a
        /// missing column are harmless no-ops (see GridFormatting).
        /// </summary>
        private static void ApplyMaxColumnWidths(DataGridView grid)
        {
            // PartID/ManufacturingPart were originally given a flat pixel width, but that turned
            // out too narrow for real values — DataGridView doesn't drop the overflow, it shows an
            // ellipsis ("...") instead, which looked like the trimming fix hadn't actually run.
            // Character-based widths (like Desc2 already used) size to actual typical content
            // instead of a guessed pixel number.
            SetCharacterBasedColumnWidth(grid, "PartID", 14);
            SetFixedColumnWidth(grid, "Rev", 45);
            SetCharacterBasedColumnWidth(grid, "ManufacturingPart", 22);
            SetCharacterBasedColumnWidth(grid, "Desc2", 30);

            // Order/Status/Type only ever hold a short code (order number, single MAX status digit,
            // single part-type letter) — these should never grow/shrink with content, so a tight
            // character count instead of the earlier, too-generous guess.
            SetCharacterBasedColumnWidth(grid, "Order", 11);
            // Status/Firm/Type/Cnv only ever hold a single digit, a checkbox (Firm binds as bool, so
            // DataGridView auto-renders it as a CheckBox cell, not "True"/"False" text) or a short
            // number — narrowed further on request to free up room for Desc1/Reference, which were
            // being squeezed down to nearly nothing by the growing list of fixed-width columns.
            SetCharacterBasedColumnWidth(grid, "Status", 4);
            SetCharacterBasedColumnWidth(grid, "Firm", 3);
            SetCharacterBasedColumnWidth(grid, "Type", 4);
            SetCharacterBasedColumnWidth(grid, "Quantity", 10);
            SetCharacterBasedColumnWidth(grid, "Cost", 12);
            SetCharacterBasedColumnWidth(grid, "Cnv", 6);
            SetCharacterBasedColumnWidth(grid, "ExtCost", 14);
            SetCharacterBasedColumnWidth(grid, "DueDate", 12);
            SetCharacterBasedColumnWidth(grid, "PoDueDate", 12);
            SetCharacterBasedColumnWidth(grid, "Customer", 14);
            SetCharacterBasedColumnWidth(grid, "StockID", 14);
            SetCharacterBasedColumnWidth(grid, "RecVendor", 10);
            SetCharacterBasedColumnWidth(grid, "Workflow", 16);
            SetCharacterBasedColumnWidth(grid, "Manufacturer", 16);
            SetCharacterBasedColumnWidth(grid, "Packaging", 14);
            SetCharacterBasedColumnWidth(grid, "ReelRequirement", 8);
            SetCharacterBasedColumnWidth(grid, "SelectedSupplier", 14);
            SetCharacterBasedColumnWidth(grid, "SelectedPrice", 12);
            SetCharacterBasedColumnWidth(grid, "SelectedQuantity", 10);
            SetCharacterBasedColumnWidth(grid, "SelectedDueDate", 12);
            // Desc1/Reference get a fixed width too since 16 nov 2026 (no Fill: columns must not
            // stretch when the window is maximized); widen by dragging, the width is remembered.
            SetCharacterBasedColumnWidth(grid, "Desc1", 30);
            SetCharacterBasedColumnWidth(grid, "Reference", 30);

            // App-wide conventions (GridFormatting): quantities at 4 decimals, cost/price fields at
            // 4 decimals with a € prefix, Cnv (Cost Conv, a conversion factor rather than a price) at
            // 2, DueDate as d-MM-yyyy — all right-aligned.
            ApplyQuantityColumns(grid, "Quantity");
            ApplyCurrencyColumns(grid, "Cost", "ExtCost");
            ApplyDecimalColumns(grid, 2, "Cnv");
            ApplyDateColumns(grid, "DueDate", "PoDueDate");
        }

        /// <summary>
        /// Pins the given columns to the left, in the given order, so they stay visible while the
        /// rest of the grid scrolls horizontally — user request (sep 2026): too many fixed-width
        /// columns for one screen, keep the most important ones ("what is this, what did we pick")
        /// always in view instead of shrinking everything to fit. DataGridView requires every frozen
        /// column to precede every non-frozen one by DisplayIndex, and refuses to set Frozen out of
        /// that order — so DisplayIndex is settled for every column first, then Frozen is toggled.
        /// Columns not named here explicitly get Frozen=false, since DataGridView recreates all
        /// columns from scratch on every DataSource reassignment (AutoGenerateColumns), but a stale
        /// Frozen=true from column reuse elsewhere would otherwise silently break the contiguity rule.
        /// </summary>
        private static void ApplyFrozenColumns(DataGridView grid, params string[] frozenColumnNamesInOrder)
        {
            var frozenColumns = frozenColumnNamesInOrder
                .Select(name => grid.Columns[name])
                .Where(c => c != null)
                .ToList();
            if (frozenColumns.Count == 0) return;

            var otherColumns = grid.Columns.Cast<DataGridViewColumn>()
                .Where(c => !frozenColumns.Contains(c))
                .ToList();

            var displayIndex = 0;
            foreach (var column in frozenColumns) column.DisplayIndex = displayIndex++;
            foreach (var column in otherColumns) column.DisplayIndex = displayIndex++;

            foreach (var column in frozenColumns) column.Frozen = true;
            foreach (var column in otherColumns) column.Frozen = false;
        }

        private async void dgv_requests_SelectionChanged(object sender, EventArgs e)
        {
            try
            {
                await LoadOffersForSelectedRequestAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Fout bij laden van offers", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Toont de offers van de ene regel van de geselecteerde aanvraag rechtstreeks (user request,
        /// 20 okt 2026: "ik zou het logischer vinden als onderaan dan direct de offers worden
        /// weergegeven... met de optie om daar het gewenste offer te kiezen") — vervangt het oude
        /// "regels van deze aanvraag"-grid, dat toch altijd precies één (vrijwel identieke) regel liet
        /// zien, zie PopulateRequestsGrid's eigen commentaar. Het losse "Offers bekijken"-scherm
        /// (OfferComparisonForm) verviel hiermee; kiezen/deselecteren gebeurt nu via de
        /// "Select"-checkbox hieronder.
        /// </summary>
        private async System.Threading.Tasks.Task LoadOffersForSelectedRequestAsync()
        {
            var requestId = GetSelectedRequestId();
            if (requestId == null)
            {
                _currentLineId = null;
                dgv_offers.DataSource = null;
                return;
            }

            var request = await _composition.PurchaseRequestRepository.GetByIdAsync(requestId.Value);
            var line = request?.Lines.FirstOrDefault();
            _currentLineId = line?.Id;
            if (line == null)
            {
                dgv_offers.DataSource = null;
                return;
            }

            var offers = await _composition.Engine.GetOffersForLineAsync(line.Id);
            var selection = await _composition.Engine.GetSelectionForLineAsync(line.Id);

            BindSortable(dgv_offers, offers.Select(o => new OfferGridRow
            {
                Id = o.Id,
                Select = selection != null && selection.SelectedOfferId == o.Id,
                SupplierCode = o.SupplierCode,
                // Combination offer (full reels + rest, 18 nov 2026): both part numbers and the split.
                SupplierPartNumber = o.IsCombination ? $"{o.SupplierPartNumber} + {o.RemainderSupplierPartNumber}" : o.SupplierPartNumber,
                MatchConfidence = o.MatchConfidence.ToString(),
                OfferedQuantity = o.OfferedQuantity,
                AttritionQuantity = o.AttritionQuantity,
                SurplusQuantity = o.SurplusQuantity,
                // Sourcing per article (18 nov 2026): quantities are for all lines of the bundle together.
                Bundle = o.BundleLineCount > 1 ? $"{o.BundleLineCount} regels, totaal {o.BundleNeededQuantity:N0}"
                    : o.BundleNeededQuantity > 0 ? $"totaal {o.BundleNeededQuantity:N0}" : string.Empty,
                UnitPrice = o.UnitPrice,
                Currency = o.Currency,
                Packaging = o.IsCombination ? o.Composition : o.PackagingType.ToString(),
                Reel = o.ReelType?.ToString() ?? "-",
                ReelingFee = o.ReelingFee,
                LandedCost = o.LandedCost,
                ComparisonCost = SupplierSelectionEngine.EffectiveComparisonCost(o),
                LeadTimeDays = o.LeadTimeDays,
                AvailableQuantity = o.AvailableQuantity,
                DueDate = o.EstimatedDeliveryDate,
                RequestedDelivery = o.RequestedDeliveryDate
            }));

            if (dgv_offers.Columns["Id"] != null)
                dgv_offers.Columns["Id"].Visible = false;
            SetHeaderText(dgv_offers, "SupplierCode", "Supplier");
            SetHeaderText(dgv_offers, "SupplierPartNumber", "Supplier Part Number");
            SetHeaderText(dgv_offers, "MatchConfidence", "Match");
            SetHeaderText(dgv_offers, "OfferedQuantity", "Quantity");
            SetHeaderText(dgv_offers, "UnitPrice", "Unit Price");
            SetHeaderText(dgv_offers, "LandedCost", "Landed Cost");
            // Packaging per component type (17 nov 2026): each packaging variant is its own row; the
            // selection compares "Vergelijkingskosten" = landed cost + the surplus penalty.
            SetHeaderText(dgv_offers, "AttritionQuantity", "Extra uitval");
            SetHeaderText(dgv_offers, "SurplusQuantity", "Overschot");
            SetHeaderText(dgv_offers, "Bundle", "Bundel");
            SetHeaderText(dgv_offers, "ReelingFee", "Reeling Fee");
            SetHeaderText(dgv_offers, "ComparisonCost", "Vergelijkingskosten");
            SetHeaderText(dgv_offers, "LeadTimeDays", "Lead Time (Working Days)");
            SetHeaderText(dgv_offers, "AvailableQuantity", "Available Qty");
            SetHeaderText(dgv_offers, "DueDate", "Due Date");
            SetHeaderText(dgv_offers, "RequestedDelivery", "Gewenste leverdatum");
            ApplyQuantityColumns(dgv_offers, "OfferedQuantity", "AttritionQuantity", "SurplusQuantity", "AvailableQuantity");
            ApplyCurrencyColumns(dgv_offers, "UnitPrice", "ReelingFee", "LandedCost", "ComparisonCost");
            ApplyDateColumns(dgv_offers, "DueDate", "RequestedDelivery");
            // Every column gets an explicit width (user request, 20 okt 2026: "ik heb liever dat we
            // rechts wat ruimte over houden dan dat alles zo breed wordt dat het ook niet meer leesbaar
            // is"); no Fill at all since 16 nov 2026.
            SetCharacterBasedColumnWidth(dgv_offers, "Select", 8);
            SetCharacterBasedColumnWidth(dgv_offers, "SupplierCode", 10);
            SetCharacterBasedColumnWidth(dgv_offers, "SupplierPartNumber", 32);
            SetCharacterBasedColumnWidth(dgv_offers, "MatchConfidence", 10);
            SetCharacterBasedColumnWidth(dgv_offers, "OfferedQuantity", 10);
            SetCharacterBasedColumnWidth(dgv_offers, "AttritionQuantity", 12);
            SetCharacterBasedColumnWidth(dgv_offers, "SurplusQuantity", 10);
            SetCharacterBasedColumnWidth(dgv_offers, "Bundle", 22);
            SetCharacterBasedColumnWidth(dgv_offers, "UnitPrice", 12);
            SetCharacterBasedColumnWidth(dgv_offers, "Currency", 8);
            SetCharacterBasedColumnWidth(dgv_offers, "Packaging", 30);
            SetCharacterBasedColumnWidth(dgv_offers, "Reel", 10);
            SetCharacterBasedColumnWidth(dgv_offers, "ReelingFee", 12);
            SetCharacterBasedColumnWidth(dgv_offers, "LandedCost", 12);
            SetCharacterBasedColumnWidth(dgv_offers, "ComparisonCost", 18);
            SetCharacterBasedColumnWidth(dgv_offers, "LeadTimeDays", 22);
            SetCharacterBasedColumnWidth(dgv_offers, "AvailableQuantity", 12);
            SetCharacterBasedColumnWidth(dgv_offers, "DueDate", 12);
            SetCharacterBasedColumnWidth(dgv_offers, "RequestedDelivery", 20);

            // Enkel "Select" en het aantal (18 nov 2026) mogen bewerkt worden — zie de ReadOnly=false op de grid zelf.
            foreach (DataGridViewColumn column in dgv_offers.Columns)
                column.ReadOnly = column.Name != "Select" && column.Name != "OfferedQuantity" && column.Name != "RequestedDelivery";
        }

        /// <summary>
        /// Een gewone klasse i.p.v. een anoniem type (zoals elk ander grid in dit scherm wél
        /// gebruikt) — nodig omdat een anoniem type alleen getters heeft, en DataGridView een
        /// "ReadOnly moet True zijn voor een kolom gekoppeld aan een alleen-lezen veld"-foutmelding
        /// geeft zodra de grid zelf niet grid-breed ReadOnly staat (wat hier moet, voor "Select") maar
        /// de gebonden eigenschap geen setter heeft — ontdekt pas bij het echt selecteren van een rij
        /// in de praktijk, niet bij het bouwen. "Select" heeft hierdoor een echte setter, net als elke
        /// andere eigenschap hier (ook al wordt alleen "Select" zelf echt bewerkt — zie de ReadOnly-lus
        /// hierboven).
        /// </summary>
        private class OfferGridRow
        {
            public int Id { get; set; }
            public bool Select { get; set; }
            public string SupplierCode { get; set; }
            public string SupplierPartNumber { get; set; }
            public string MatchConfidence { get; set; }
            public int OfferedQuantity { get; set; }
            public int AttritionQuantity { get; set; }
            public int SurplusQuantity { get; set; }
            public string Bundle { get; set; }
            public decimal UnitPrice { get; set; }
            public string Currency { get; set; }
            public string Packaging { get; set; }
            public string Reel { get; set; }
            public decimal ReelingFee { get; set; }
            public decimal LandedCost { get; set; }
            public decimal ComparisonCost { get; set; }
            public int LeadTimeDays { get; set; }
            public int AvailableQuantity { get; set; }
            public DateTime DueDate { get; set; }
            public DateTime? RequestedDelivery { get; set; }
        }

        /// <summary>
        /// Aanvinken kiest die offerte (SelectOfferManuallyAsync); uitvinken van de al-gekozen regel
        /// maakt die keuze weer ongedaan (DeselectOfferAsync, user request, 20 okt 2026 — workflow
        /// springt dan terug van ReadyToOrder naar Sourcing). Uitvinken van een andere (niet eerder
        /// gekozen) regel kan niet gebeuren — die stond al uit — dus die tak wordt nooit bereikt.
        /// </summary>
        private void OffersGrid_CellParsing(object sender, DataGridViewCellParsingEventArgs e)
        {
            if (dgv_offers.Columns[e.ColumnIndex].Name == "RequestedDelivery")
            {
                var text = e.Value?.ToString();
                if (string.IsNullOrWhiteSpace(text))
                {
                    e.Value = null;
                    e.ParsingApplied = true;
                }
                else if (DateTime.TryParse(text, System.Globalization.CultureInfo.CurrentCulture,
                             System.Globalization.DateTimeStyles.None, out var date))
                {
                    e.Value = date.Date;
                    e.ParsingApplied = true;
                }
                return;
            }

            if (dgv_offers.Columns[e.ColumnIndex].Name != "OfferedQuantity" || e.Value == null) return;
            if (decimal.TryParse(e.Value.ToString(), System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.CurrentCulture, out var parsed) && parsed > 0 && parsed <= int.MaxValue)
            {
                e.Value = (int)Math.Ceiling(parsed);
                e.ParsingApplied = true;
            }
        }

        /// <summary>
        /// A different quantity typed on an offer (user, 18 nov 2026: "Wat nu als ik besluit om er
        /// 25000 stuks te willen bestellen") sources the line and its article bundle again for that
        /// total: fresh prices at the new quantity, a new comparison, and a new choice/approval.
        /// </summary>
        private async System.Threading.Tasks.Task OffersGrid_QuantityChangedAsync(DataGridViewCellEventArgs e)
        {
            if (_currentLineId == null) return;
            var newQuantity = (int)dgv_offers.Rows[e.RowIndex].Cells["OfferedQuantity"].Value;

            var answer = MessageBox.Show(this,
                $"Opnieuw sourcen voor een totaal van {newQuantity:N0} stuks?\n\n" +
                "Alle offers van deze regel (en van de andere regels in de bundel) worden opnieuw opgehaald voor dat aantal; " +
                "een bestaande keuze of goedkeuring vervalt.",
                "Aantal aanpassen", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (answer != DialogResult.OK)
            {
                await LoadLocalRequestsAsync();
                return;
            }

            try
            {
                SetBusy(true);
                SetStatus($"Opnieuw sourcen voor {newQuantity:N0} stuks...");
                await _composition.Engine.ResourceLineWithQuantityAsync(_currentLineId.Value, newQuantity, _composition.Session.UserName);
                SetStatus($"Opnieuw gesourced voor {newQuantity:N0} stuks.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Fout bij aanpassen van het aantal", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                await LoadLocalRequestsAsync();
                SetBusy(false);
            }
        }

        /// <summary>Gewenste leverdatum (18 nov 2026): one date for the line and its whole bundle; leeg = geen voorkeur.</summary>
        private async System.Threading.Tasks.Task OffersGrid_RequestedDeliveryChangedAsync(DataGridViewCellEventArgs e)
        {
            if (_currentLineId == null) return;
            var date = dgv_offers.Rows[e.RowIndex].Cells["RequestedDelivery"].Value as DateTime?;

            try
            {
                SetBusy(true);
                await _composition.Engine.SetRequestedDeliveryDateAsync(_currentLineId.Value, date, _composition.Session.UserName);
                SetStatus(date.HasValue
                    ? $"Gewenste leverdatum {date.Value:d} ingesteld (ook voor de andere regels in de bundel)."
                    : "Gewenste leverdatum gewist.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Fout bij aanpassen van de leverdatum", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                await LoadLocalRequestsAsync();
                SetBusy(false);
            }
        }

        private async System.Threading.Tasks.Task OffersGrid_CellValueChangedAsync(DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (dgv_offers.Columns[e.ColumnIndex].Name == "OfferedQuantity")
            {
                await OffersGrid_QuantityChangedAsync(e);
                return;
            }
            if (dgv_offers.Columns[e.ColumnIndex].Name == "RequestedDelivery")
            {
                await OffersGrid_RequestedDeliveryChangedAsync(e);
                return;
            }
            if (dgv_offers.Columns[e.ColumnIndex].Name != "Select") return;
            if (_currentLineId == null) return;

            var row = dgv_offers.Rows[e.RowIndex];
            var nowChecked = (bool)row.Cells["Select"].Value;

            try
            {
                SetBusy(true);
                if (nowChecked)
                {
                    var offerId = (int)row.Cells["Id"].Value;
                    await _composition.Engine.SelectOfferManuallyAsync(_currentLineId.Value, offerId, reason: null, _composition.Session.UserName);
                }
                else
                {
                    await _composition.Engine.DeselectOfferAsync(_currentLineId.Value, _composition.Session.UserName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Fout bij kiezen van offerte", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                await LoadLocalRequestsAsync();
                SetBusy(false);
            }
        }

        private int? GetSelectedRequestId()
        {
            if (dgv_requests.CurrentRow == null) return null;
            return (int)dgv_requests.CurrentRow.Cells["Id"].Value;
        }

        // Derived from SelectedCells rather than SelectedRows, on purpose, even though
        // RowHeaderSelect (user request, sep 2026: a plain cell click selects/copies just that cell;
        // clicking the row header still selects the whole row, which is what this method is really
        // for) does populate SelectedRows when a row is selected via its header. SelectedCells
        // additionally still resolves correctly if a row only has individual cells selected (e.g. a
        // stray cell click instead of the header) — a row selected via its header shows up here too,
        // since DataGridView marks every cell of a fully-selected row as Selected. Either way of
        // selecting a row for "Sourcing starten (selectie)"/"Order plaatsen (selectie)" works.
        private List<int> GetSelectedRequestIds() =>
            dgv_requests.SelectedCells.Cast<DataGridViewCell>().Select(c => c.OwningRow).Distinct()
                .Select(r => (int)r.Cells["Id"].Value).ToList();

        private List<int> GetAllRequestIds() =>
            dgv_requests.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow).Select(r => (int)r.Cells["Id"].Value).ToList();

        private async System.Threading.Tasks.Task NewRequestAsync()
        {
            using (var form = new NewPurchaseRequestForm(_composition.PurchaseRequestRepository, _composition.MaxOrderRepository))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    await LoadLocalRequestsAsync();
                }
            }
        }

        /// <summary>
        /// Sourcet één of meerdere aanvragen na elkaar (nooit parallel — de app deelt toch al één
        /// ProcurementDbContext die alles serialiseert via RunGuardedAsync, dus gelijktijdig
        /// aanroepen zou geen tijd schelen, enkel de voortgangsmelding minder duidelijk maken).
        /// Gebruikt door zowel "Sourcing starten (selectie)" als "Alles sourcen". Eén mislukte
        /// aanvraag stopt de rest niet — aan het eind volgt een samenvatting i.p.v. per aanvraag
        /// een los berichtvenster.
        /// </summary>
        private async System.Threading.Tasks.Task SourceRequestsAsync(IReadOnlyList<int> requestIds, int confirmIfMoreThan)
        {
            if (_isBusy) return;

            if (requestIds.Count == 0)
            {
                MessageBox.Show(this, "Selecteer eerst een of meer aanvragen.", "Geen selectie", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (requestIds.Count > confirmIfMoreThan)
            {
                var confirm = MessageBox.Show(this,
                    $"Sourcing starten voor {requestIds.Count} aanvragen?",
                    "Bevestigen", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirm != DialogResult.Yes) return;
            }

            SetBusy(true);
            try
            {
                var readyToOrder = 0;
                var waitingApproval = 0;
                var exceptionCount = 0;
                var failed = 0;
                var skippedOrdered = 0;

                for (var i = 0; i < requestIds.Count; i++)
                {
                    SetStatus($"Sourcing {i + 1}/{requestIds.Count}...");
                    try
                    {
                        // WaitingApproval is re-sourced too since 18 nov 2026: re-sourcing now replaces
                        // the line's old offers and pending approval request (no duplicates anymore),
                        // and sourcing per article needs it to rebuild a bundle.
                        //
                        // Ordered must be skipped outright, not just "harmless to re-run": every line on
                        // an Ordered request already has a SupplierSelection and no pending approval or
                        // exception, so SourcePurchaseRequestAsync would recompute finalStatus as
                        // ReadyToOrder and silently downgrade an already-placed order back into the
                        // sourcing queue. Only reachable now that the "Weergeven" status filter can show
                        // Ordered rows at all — before that they were never in the grid to select.
                        var current = await _composition.PurchaseRequestRepository.GetByIdAsync(requestIds[i]);
                        if (current == null) continue;
                        if (current.Status == PurchaseRequestStatus.Ordered)
                        {
                            skippedOrdered++;
                            continue;
                        }

                        await _composition.Engine.SourcePurchaseRequestAsync(requestIds[i]);
                        var updated = await _composition.PurchaseRequestRepository.GetByIdAsync(requestIds[i]);
                        if (updated == null) continue;
                        if (updated.Status == PurchaseRequestStatus.ReadyToOrder) readyToOrder++;
                        else if (updated.Status == PurchaseRequestStatus.WaitingApproval) waitingApproval++;
                        else if (updated.Status == PurchaseRequestStatus.Exception) exceptionCount++;
                    }
                    catch (Exception)
                    {
                        failed++;
                    }
                }

                var summary = $"Sourcing klaar: {readyToOrder} klaar om te bestellen, {waitingApproval} wacht op goedkeuring, {exceptionCount} met fout";
                if (failed > 0) summary += $", {failed} mislukt";
                if (skippedOrdered > 0) summary += $", {skippedOrdered} overgeslagen (al besteld)";
                SetStatus(summary + ".");

                await LoadLocalRequestsAsync();
            }
            finally
            {
                SetBusy(false);
            }
        }

        /// <summary>
        /// Plaatst orders voor de geselecteerde aanvragen — enkel regels die al een SupplierSelection
        /// hebben (status ReadyToOrder) en nog niet op een PO staan worden meegenomen. Regels worden
        /// per leverancier gebundeld tot één PO per leverancier (nooit per project/klant — zie
        /// ProcurementEngine.PlaceOrdersAsync), op dezelfde manier opgezet als "Sourcing starten
        /// (selectie)" hierboven.
        /// </summary>
        private async System.Threading.Tasks.Task PlaceOrdersAsync(IReadOnlyList<int> requestIds)
        {
            if (_isBusy) return;

            if (requestIds.Count == 0)
            {
                MessageBox.Show(this, "Selecteer eerst een of meer aanvragen.", "Geen selectie", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirm = MessageBox.Show(this,
                $"Order(s) plaatsen voor {requestIds.Count} aanvraag/aanvragen? Regels worden per leverancier gebundeld tot één PO per leverancier.",
                "Bevestigen", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            SetBusy(true);
            try
            {
                var supplierCodes = await _composition.Engine.PreviewSupplierCodesForOrderAsync(requestIds);
                if (!await EnsureFarnellShipToAddressSelectedAsync(supplierCodes))
                {
                    SetStatus("Geannuleerd — geen orders geplaatst.");
                    return;
                }

                await _composition.Engine.PlaceOrdersAsync(requestIds, _composition.Session.UserName);
                SetStatus("Order(s) geplaatst.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Fout bij plaatsen van order", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // Refreshed unconditionally, success or failure — a partial batch (some supplier
                // groups placed before one failed) should still show what did go through instead of
                // only refreshing on a clean run.
                await LoadLocalRequestsAsync();
                SetBusy(false);
            }
        }

        /// <summary>
        /// If this batch touches Farnell, shows a picker built from the live
        /// GetAvailableShipToAddressesAsync list and sets FarnellAdapter.SelectedShipToAddressId
        /// from it — asked fresh every time rather than falling back to a stored default (user
        /// request, 21 okt 2026: "dit gaat bij Farnell wel eens fout... dan hoeven we de database
        /// ook niet aan te passen"). Skipped entirely when there's zero or one address to choose
        /// from (nothing to actually decide) or Farnell isn't part of this batch at all. Returns
        /// false if the user cancelled the picker — PlaceOrdersAsync then places nothing at all for
        /// this batch, including any non-Farnell suppliers in the same selection, rather than
        /// partially proceed.
        /// </summary>
        private async System.Threading.Tasks.Task<bool> EnsureFarnellShipToAddressSelectedAsync(IReadOnlyList<string> supplierCodes)
        {
            if (!supplierCodes.Contains("FARNELL", StringComparer.OrdinalIgnoreCase)) return true;

            var farnellAdapter = _composition.Adapters.OfType<FarnellAdapter>().FirstOrDefault();
            if (farnellAdapter == null) return true;

            var addresses = await farnellAdapter.GetAvailableShipToAddressesAsync();
            if (addresses.Count <= 1)
            {
                farnellAdapter.SelectedShipToAddressId = addresses.FirstOrDefault()?.AddressId;
                return true;
            }

            using (var dialog = new FarnellShipToAddressPickerForm(addresses))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return false;
                farnellAdapter.SelectedShipToAddressId = dialog.SelectedAddressId;
                return true;
            }
        }

        /// <summary>
        /// Checkt elke PO die nog op een leveranciersbevestiging wacht (spec, aug 2026: Confirming/
        /// Reference/duedate direct in MAX bijwerken zodra dat bevestigd is — geen aparte "controleer
        /// eerst"-stap nodig, alles wat schoon binnenkomt wordt automatisch verwerkt). Enkel wat
        /// afwijkt (aantal/prijs/te late levering) verschijnt in een los venster ter beoordeling.
        /// </summary>
        private async System.Threading.Tasks.Task ProcessOrderConfirmationsAsync()
        {
            if (_isBusy) return;
            SetBusy(true);
            Core.Models.OrderConfirmationResult result;
            try
            {
                result = await _composition.Engine.ProcessOrderConfirmationsAsync(_composition.Session.UserName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Fout bij verwerken van orderbevestiging", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            finally
            {
                SetBusy(false);
            }

            var summary = $"Orderbevestiging verwerkt: {result.CheckedOrderCount} order(s) gecontroleerd";
            // "Overgeslagen" covers two different causes that ProcessOrderConfirmationsAsync doesn't
            // distinguish in its own count: no OrderStatus-ondersteuning/nog niet ingediend (silent,
            // no audit entry), or a SupplierException from GetOrderStatusAsync itself (logged as
            // ORDER_CONFIRMATION_CHECK_FAILED) — the Audit Log is the place to tell those apart.
            if (result.SkippedOrderCount > 0) summary += $", {result.SkippedOrderCount} overgeslagen (zie Audit Log voor de reden per order)";
            if (result.Exceptions.Count > 0) summary += $", {result.Exceptions.Count} afwijking(en)";
            SetStatus(summary + ".");

            // Shown only after SetBusy(false) — a modal dialog on top of a still-busy wait cursor
            // looked like the cursor was stuck for as long as the user kept the dialog open.
            if (result.Exceptions.Count > 0)
            {
                using (var form = new OrderConfirmationExceptionsForm(result.Exceptions))
                {
                    form.ShowDialog(this);
                }
            }
            else if (result.CheckedOrderCount > 0)
            {
                MessageBox.Show(this, "Alle gecontroleerde orders zijn bevestigd zoals besteld — geen afwijkingen.",
                    "Orderbevestiging verwerkt", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (result.SkippedOrderCount > 0)
            {
                // Orders die wél op bevestiging wachten, maar stuk voor stuk zijn overgeslagen (geen
                // ondersteuning, of een fout bij het opvragen van de status) — dit is dus niet "er
                // waren geen orders", en moet dat ook niet zeggen (user feedback, 20 okt 2026: deze
                // melding verscheen terwijl er wel degelijk een order op Acknowledged stond).
                MessageBox.Show(this,
                    $"{result.SkippedOrderCount} order(s) wachten op een bevestiging, maar konden niet gecontroleerd worden — zie Audit Log voor de reden per order.",
                    "Orderbevestiging verwerkt", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                MessageBox.Show(this, "Geen orders gevonden die op een bevestiging wachten.",
                    "Orderbevestiging verwerkt", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        /// <summary>
        /// Called by ShellForm right after its Approvals window closes — a manual approval decision
        /// can move a request's Workflow status out of WaitingApproval, which used to be refreshed
        /// here directly when this window owned the "Approvals..." button/menu item itself. Now that
        /// Orders/Approvals/Supplier Mapping/Settings/Audit Log live on ShellForm instead (user
        /// request, 20 okt 2026 — see ShellForm's own class comment), this is only reachable, and
        /// only meaningful, while this window happens to be open at the same time.
        /// </summary>
        public async System.Threading.Tasks.Task RefreshLocalRequestsAsync() => await LoadLocalRequestsAsync();

        private void SetStatus(string text) => lbl_status.Text = text;

        /// <summary>
        /// Every action-button handler calls this instead of managing its own Enabled/UseWaitCursor
        /// — see the _isBusy field comment for why "only disable the button that was clicked" isn't
        /// enough. SetBusy(true) at the start of a handler (before the first await) and
        /// SetBusy(false) in that handler's finally.
        /// </summary>
        private void SetBusy(bool busy)
        {
            _isBusy = busy;
            UseWaitCursor = busy;

            // DataGridView is known to hold onto its own cached cursor after the wait cursor
            // cascades back to false on the form (a long-standing WinForms quirk, not something
            // toggling UseWaitCursor alone fixes) — most noticeable here because PopulateRequestsGrid
            // rebinds dgv_requests.DataSource while UseWaitCursor is still true, mid-query. Forcing
            // both grids back to Cursors.Default on the way out clears that stale state explicitly.
            if (!busy)
            {
                dgv_requests.Cursor = Cursors.Default;
                dgv_offers.Cursor = Cursors.Default;
            }

            var enabled = !busy;
            bt_query.Enabled = enabled;
            bt_newRequest.Enabled = enabled;
            bt_startSourcing.Enabled = enabled;
            bt_sourceAll.Enabled = enabled;
            bt_placeOrders.Enabled = enabled && _composition.OrderingEnabled;
            bt_processConfirmations.Enabled = enabled;
            // Not strictly a race on shared MAX SqlConnections like the buttons above (their
            // CheckedChanged handler already no-ops via the same _isBusy check) — disabled anyway so
            // a toggle during a Query can't silently fail to visually match the grid until the next
            // unrelated reload.
            foreach (var checkBox in _requestStatusCheckBoxes.Values)
                checkBox.Enabled = enabled;
        }
    }
}
