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
using Procurement.Services.PurchaseRequests;
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

        // Screen logic shared with the web app (18 nov 2026): filtering, rows, sourcing, choosing.
        private readonly PurchaseRequestScreenService _screen;

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
            _screen = new PurchaseRequestScreenService(_composition);
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

            // MAX Order_Master.STATUS_10 filter (1 = Planned, 2 = Approved; default only Approved) is
            // read from the checkboxes on every Query (BuildFilter).
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

        /// <summary>The filter boxes as plain data for <see cref="PurchaseRequestScreenService"/>.</summary>
        private RequestListFilter BuildFilter() => new RequestListFilter
        {
            Statuses = GetCheckedRequestStatuses(),
            IncludePlanned = chk_statusPlanned.Checked,
            IncludeApproved = chk_statusApproved.Checked,
            DueDateStart = chk_dueDateEnable.Checked ? dtp_dueDateStart.Value.Date : (DateTime?)null,
            DueDateEnd = chk_dueDateEnable.Checked ? dtp_dueDateEnd.Value.Date : (DateTime?)null,
            RangeField = GetSelectedRangeField(),
            RangeStart = txt_rangeStart.Text,
            RangeEnd = txt_rangeEnd.Text,
            ShowTestRequests = chk_showTestRequests.Checked
        };

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
            var result = await _screen.LoadLocalAsync(BuildFilter());
            PopulateRequestsGrid(result.Rows);
            SetStatus(result.StatusText);
            await LoadOffersForSelectedRequestAsync();
        }

        /// <summary>Vraagt open orders op uit MAX met de huidige filterinstellingen en synchroniseert ze naar lokale aanvragen — enkel via de "Query"-knop.</summary>
        private async System.Threading.Tasks.Task RefreshRequestsAsync()
        {
            SetStatus("Orders opvragen uit MAX...");
            var result = await _screen.QueryMaxAsync(BuildFilter());
            PopulateRequestsGrid(result.Rows);
            SetStatus(result.StatusText);
            await LoadOffersForSelectedRequestAsync();
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

        /// <summary>Rows come from PurchaseRequestScreenService.BuildRowsAsync; this only binds and formats.</summary>
        private void PopulateRequestsGrid(IReadOnlyList<RequestRow> rows)
        {
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

            var offers = await _screen.GetOffersAsync(requestId.Value);
            _currentLineId = offers.LineId;
            if (offers.LineId == null)
            {
                dgv_offers.DataSource = null;
                return;
            }

            BindSortable(dgv_offers, offers.Rows);

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
            ApplyQuantityColumns(dgv_offers, "OfferedQuantity", "AttritionQuantity", "SurplusQuantity", "AvailableQuantity");
            ApplyCurrencyColumns(dgv_offers, "UnitPrice", "ReelingFee", "LandedCost", "ComparisonCost");
            ApplyDateColumns(dgv_offers, "DueDate");
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

            // Enkel "Select" en het aantal (18 nov 2026) mogen bewerkt worden — zie de ReadOnly=false op de grid zelf.
            foreach (DataGridViewColumn column in dgv_offers.Columns)
                column.ReadOnly = column.Name != "Select" && column.Name != "OfferedQuantity";
        }

        /// <summary>
        /// Aanvinken kiest die offerte (SelectOfferManuallyAsync); uitvinken van de al-gekozen regel
        /// maakt die keuze weer ongedaan (DeselectOfferAsync, user request, 20 okt 2026 — workflow
        /// springt dan terug van ReadyToOrder naar Sourcing). Uitvinken van een andere (niet eerder
        /// gekozen) regel kan niet gebeuren — die stond al uit — dus die tak wordt nooit bereikt.
        /// </summary>
        private void OffersGrid_CellParsing(object sender, DataGridViewCellParsingEventArgs e)
        {
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
                await _screen.ChangeQuantityAsync(_currentLineId.Value, newQuantity);
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

        private async System.Threading.Tasks.Task OffersGrid_CellValueChangedAsync(DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (dgv_offers.Columns[e.ColumnIndex].Name == "OfferedQuantity")
            {
                await OffersGrid_QuantityChangedAsync(e);
                return;
            }
            if (dgv_offers.Columns[e.ColumnIndex].Name != "Select") return;
            if (_currentLineId == null) return;

            var row = dgv_offers.Rows[e.RowIndex];
            var nowChecked = (bool)row.Cells["Select"].Value;

            try
            {
                SetBusy(true);
                var offerId = (int)row.Cells["Id"].Value;
                await _screen.SetOfferSelectedAsync(_currentLineId.Value, offerId, nowChecked);
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
                var summary = await _screen.SourceRequestsAsync(requestIds, new Progress<string>(SetStatus));
                SetStatus(summary.ToString());

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
