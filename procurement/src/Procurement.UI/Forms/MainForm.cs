using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Procurement.Core.Entities;
using Procurement.Core.Enums;
using Procurement.Erp;
using Procurement.UI.Composition;
using static Procurement.UI.Support.GridFormatting;

namespace Procurement.UI.Forms
{
    /// <summary>Purchase Requests-overzicht (spec §8.1) — the application's main/start screen.</summary>
    public class MainForm : Form
    {
        private readonly CompositionRoot _composition;

        private DataGridView _requestsGrid;
        private DataGridView _offersGrid;

        // The one PurchaseRequestLine the offers grid currently shows — set whenever
        // LoadOffersForSelectedRequestAsync (re)loads, read when a "Select" checkbox is ticked.
        // Every MAX-synced PR has exactly one line (see PopulateRequestsGrid's own comment), so the
        // offers grid never needs to represent more than one line's offers at a time.
        private int? _currentLineId;

        // "Orders ophalen uit MAX" group — status filter + the Query button that actually triggers
        // the MAX round-trip (nothing here runs automatically, only on Query click).
        private CheckBox _statusPlannedCheckBox;
        private CheckBox _statusApprovedCheckBox;
        private Button _queryButton;

        // "Due Date Range" group.
        private CheckBox _dueDateEnableCheckBox;
        private DateTimePicker _dueDateStartPicker;
        private DateTimePicker _dueDateEndPicker;

        // "Filter" group — generic range filter, applied to whichever column "Select By" picks.
        private ComboBox _rangeFieldCombo;
        private TextBox _rangeStartBox;
        private TextBox _rangeEndBox;

        // "Weergeven" group — which of this prototype's own Workflow statuses to show in the grid.
        // Purely a local display filter (never touches MAX): read by both LoadLocalRequestsAsync and
        // RefreshRequestsAsync so toggling one behaves the same regardless of how the grid was last
        // populated. Ordered has no checkbox here at all (user request, 20 okt 2026: the requests/
        // lines grids are for PRs/PLs only — an actually-placed order belongs in the dedicated
        // "Orders..." window, not toggled back into this one) — see BuildRequestStatusFilterGroup.
        private readonly Dictionary<PurchaseRequestStatus, CheckBox> _requestStatusCheckBoxes = new Dictionary<PurchaseRequestStatus, CheckBox>();

        // Uit by default — testaanvragen zijn uitzonderingen, niet standaard zichtbaar (user request,
        // 20 okt 2026). Overstemt IsManualTestRequest's eigen "altijd tonen"-uitzondering in
        // ApplyRangeAndDueDateFilters/GetOpenPurchaseRequestsAsync wanneer uitgevinkt.
        private CheckBox _showTestRequestsCheckBox;

        private Button _newRequestButton;
        private Button _startSourcingButton;
        private Button _sourceAllButton;
        private Button _placeOrdersButton;
        private Button _processConfirmationsButton;
        private Button _orderDetailButton;
        private Button _ordersButton;
        private Button _approvalsButton;
        private Button _supplierMappingButton;
        private Button _settingsButton;
        private Button _auditLogButton;
        private Label _statusLabel;

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
            // Niets wordt automatisch geladen bij het openen — de grid blijft leeg tot de
            // gebruiker zelf op "Query" klikt (dat geldt ook voor de al lokaal gesynchroniseerde
            // aanvragen, die enkel na een expliciete actie opnieuw worden getoond).
        }

        private void InitializeComponent()
        {
            Text = $"Componenteninkoop – {_composition.Session.CompanyName} ({_composition.Session.UserName})"
                 + (_composition.Session.TestMode ? " – TESTMODUS" : string.Empty);
            Width = 1300;
            Height = 860;
            StartPosition = FormStartPosition.CenterScreen;
            // Geeft alle Dock-ed children (grids, knoppenbalken) een beetje lucht t.o.v. de
            // vensterrand in plaats van er helemaal tegenaan te zitten.
            Padding = new Padding(8);

            var toolPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, FlowDirection = FlowDirection.LeftToRight };
            _newRequestButton = new Button { Text = "Nieuwe testaanvraag toevoegen", AutoSize = true };
            _newRequestButton.Click += async (s, e) => await NewRequestAsync();
            // Elke rij is een MAX-order voor precies 1 artikel — bij tientallen open orders is 1
            // voor 1 klikken onwerkbaar. Sourcing starten verwerkt daarom alle geselecteerde
            // aanvragen (Ctrl/Shift-klik in het grid); Alles sourcen verwerkt de hele lijst zonder
            // eerst te hoeven selecteren.
            _startSourcingButton = new Button { Text = "Sourcing starten (selectie)", AutoSize = true };
            _startSourcingButton.Click += async (s, e) => await SourceRequestsAsync(GetSelectedRequestIds(), confirmIfMoreThan: 1);
            _sourceAllButton = new Button { Text = "Alles sourcen", AutoSize = true };
            _sourceAllButton.Click += async (s, e) => await SourceRequestsAsync(GetAllRequestIds(), confirmIfMoreThan: 1);
            // Losgekoppeld van sourcing (spec-correctie): sourcing/goedkeuring brengt een aanvraag
            // enkel tot ReadyToOrder (een SupplierSelection per regel), het daadwerkelijk plaatsen
            // van de order is een aparte, expliciete stap — zo kan per leverancier gebundeld worden
            // i.p.v. automatisch bij elke match meteen te bestellen.
            _placeOrdersButton = new Button { Text = "Order plaatsen (selectie)", AutoSize = true };
            _placeOrdersButton.Click += async (s, e) => await PlaceOrdersAsync(GetSelectedRequestIds());
            // Eén knop voor alle nog te bevestigen orders — verwerkt bevestigingen automatisch
            // (lokaal altijd, en in real-mode ook de MAX Confirming/Reference/duedate-velden) en
            // toont enkel wat afwijkt (aantal/prijs/te late levering), niet elke geslaagde
            // bevestiging apart (zie ProcurementEngine.ProcessOrderConfirmationsAsync).
            _processConfirmationsButton = new Button { Text = "Orderbevestiging verwerken", AutoSize = true };
            _processConfirmationsButton.Click += async (s, e) => await ProcessOrderConfirmationsAsync();
            _orderDetailButton = new Button { Text = "Order details", AutoSize = true };
            _orderDetailButton.Click += (s, e) => ShowOrderDetail();
            _ordersButton = new Button { Text = "Orders...", AutoSize = true };
            _ordersButton.Click += (s, e) => ShowOrders();
            _approvalsButton = new Button { Text = "Goedkeuringen...", AutoSize = true };
            _approvalsButton.Click += async (s, e) => await ShowApprovalsAsync();
            _supplierMappingButton = new Button { Text = "Supplier mapping...", AutoSize = true };
            _supplierMappingButton.Click += (s, e) => ShowSupplierMapping();
            _settingsButton = new Button { Text = "Instellingen...", AutoSize = true };
            _settingsButton.Click += (s, e) => ShowSettings();
            _auditLogButton = new Button { Text = "Audit-log...", AutoSize = true };
            _auditLogButton.Click += (s, e) => ShowAuditLog();

            toolPanel.Controls.AddRange(new Control[]
            {
                _newRequestButton, _startSourcingButton, _sourceAllButton, _placeOrdersButton, _processConfirmationsButton, _orderDetailButton,
                _ordersButton, _approvalsButton, _supplierMappingButton, _settingsButton, _auditLogButton
            });

            // Apart van de actieknoppen hierboven: het ophalen/filteren van MAX-orders krijgt een
            // eigen rij met groepsvakken (Query + status-filter, Due Date Range, en de generieke
            // "Select By"-filter).
            var filterPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(4)
            };
            filterPanel.Controls.Add(BuildQueryGroup());
            filterPanel.Controls.Add(BuildDueDateRangeGroup());
            filterPanel.Controls.Add(BuildRangeFilterGroup());
            filterPanel.Controls.Add(BuildRequestStatusFilterGroup());

            _requestsGrid = new DataGridView
            {
                Dock = DockStyle.Top,
                Height = 300,
                ReadOnly = true,
                AllowUserToAddRows = false,
                // AllCells meet de tekstbreedte van élke cel in élke rij om kolombreedtes te bepalen
                // — bij een paar honderd rijen is dat traag genoeg om zichtbaar te haperen, en het
                // grid herberekent dat opnieuw zodra rijen zichtbaar worden tijdens het scrollen,
                // wat als "verspringende velden" oogt. Fill verdeelt de resterende breedte enkel
                // proportioneel (FillWeight), zonder ook maar één cel te hoeven meten, en blijft dus
                // stabiel ongeacht het aantal rijen — de kolommen met een expliciete breedte
                // (ApplyMaxColumnWidths, AutoSizeMode.None) doen niet mee met die verdeling.
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                // DisableResizing houdt de headerbreedte vast op het DataGridView-default (genoeg
                // voor de rijnummers die EnableRowNumberHeaders erin tekent) — zonder deze instelling
                // telt die breedte bovendien mee bij Fill-kolomherberekening (venster resizen).
                RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing,
                SelectionMode = DataGridViewSelectionMode.RowHeaderSelect,
                // Ctrl/Shift-klik om meerdere aanvragen tegelijk te selecteren voor "Sourcing
                // starten (selectie)" — het regels-grid onder de detailweergave blijft altijd de
                // regels van de laatst-actieve rij tonen (CurrentRow), ook met meerdere geselecteerd.
                MultiSelect = true
            };
            EnableDoubleBuffering(_requestsGrid);
            EnableRowNumberHeaders(_requestsGrid);
            _requestsGrid.SelectionChanged += RequestsGrid_SelectionChanged;

            var offersLabel = new Label { Text = "Offers voor geselecteerde aanvraag (vink \"Select\" aan om een offerte te selecteren/deselecteren):", Dock = DockStyle.Top, Height = 20, Padding = new Padding(4) };

            _offersGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                // Niet grid-breed ReadOnly (anders dan elk ander grid in dit scherm) — enkel de
                // "Select"-kolom mag bewerkt worden, de rest wordt individueel op ReadOnly gezet na
                // het vullen (zie het eind van LoadOffersForSelectedRequestAsync). Nodig omdat
                // DataGridView geen per-kolom ReadOnly toestaat zolang de grid zelf ReadOnly staat.
                ReadOnly = false,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing,
                SelectionMode = DataGridViewSelectionMode.RowHeaderSelect,
                MultiSelect = false
            };
            EnableDoubleBuffering(_offersGrid);
            EnableRowNumberHeaders(_offersGrid);
            // Standaard WinForms-truc voor een checkbox-kolom: CellValueChanged vuurt pas nadat de
            // cel "committed" is, en een checkbox-cel committed vanzelf pas zodra de focus verplaatst
            // — CurrentCellDirtyStateChanged forceert dat meteen bij het klikken zelf.
            _offersGrid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (_offersGrid.IsCurrentCellDirty)
                    _offersGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            _offersGrid.CellValueChanged += async (s, e) => await OffersGrid_CellValueChangedAsync(e);

            _statusLabel = new Label { Dock = DockStyle.Bottom, Height = 24, Text = "Gereed. Klik op \"Query\" om orders op te halen uit MAX.", Padding = new Padding(4) };

            // Volgorde bepaalt de docking-stapeling (laatst toegevoegde Top-control komt bovenaan):
            // toolPanel (buitenste rand), dan filterPanel, dan requestsGrid.
            Controls.Add(_offersGrid);
            Controls.Add(offersLabel);
            Controls.Add(_requestsGrid);
            Controls.Add(filterPanel);
            Controls.Add(toolPanel);
            Controls.Add(_statusLabel);
        }

        private GroupBox BuildQueryGroup()
        {
            var layout = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true };

            // MAX Order_Master.STATUS_10 filter (user-supplied query): 1 = Planned, 2 = Approved.
            // Default: only Approved, matching what should auto-source without review. Alleen van
            // invloed op de *volgende* keer dat op Query wordt geklikt.
            _statusPlannedCheckBox = new CheckBox { Text = "1 - Planned", AutoSize = true, Checked = false, Margin = new Padding(0, 0, 0, 2) };
            _statusApprovedCheckBox = new CheckBox { Text = "2 - Approved", AutoSize = true, Checked = true, Margin = new Padding(0, 0, 0, 2) };
            UpdateIncludedOrderStatuses();

            _queryButton = new Button { Text = "Query", AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
            _queryButton.Click += async (s, e) => await QueryOrdersAsync();

            layout.Controls.Add(_statusPlannedCheckBox);
            layout.Controls.Add(_statusApprovedCheckBox);
            layout.Controls.Add(_queryButton);
            return WrapInGroupBox("Orders ophalen uit MAX", layout);
        }

        private GroupBox BuildDueDateRangeGroup()
        {
            var layout = new TableLayoutPanel { ColumnCount = 2, AutoSize = true };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _dueDateEnableCheckBox = new CheckBox { Text = "Enable", AutoSize = true };
            _dueDateStartPicker = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 105, Enabled = false };
            _dueDateEndPicker = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 105, Enabled = false };
            _dueDateEnableCheckBox.CheckedChanged += (s, e) =>
            {
                _dueDateStartPicker.Enabled = _dueDateEnableCheckBox.Checked;
                _dueDateEndPicker.Enabled = _dueDateEnableCheckBox.Checked;
            };

            AddFilterRow(layout, string.Empty, _dueDateEnableCheckBox);
            AddFilterRow(layout, "Start Date", _dueDateStartPicker);
            AddFilterRow(layout, "End Date", _dueDateEndPicker);

            return WrapInGroupBox("Due Date Range", layout);
        }

        private GroupBox BuildRangeFilterGroup()
        {
            var layout = new TableLayoutPanel { ColumnCount = 2, AutoSize = true };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _rangeFieldCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 105 };
            _rangeFieldCombo.Items.AddRange(new object[] { "Order Number", "Customer", "Part" });
            _rangeFieldCombo.SelectedIndex = 0;
            _rangeStartBox = new TextBox { Width = 105 };
            _rangeEndBox = new TextBox { Width = 105 };

            AddFilterRow(layout, "Select By", _rangeFieldCombo);
            AddFilterRow(layout, "Start", _rangeStartBox);
            AddFilterRow(layout, "End", _rangeEndBox);

            return WrapInGroupBox("Filter", layout);
        }

        private GroupBox BuildRequestStatusFilterGroup()
        {
            var layout = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true };

            var defaultChecked = new HashSet<PurchaseRequestStatus>
            {
                PurchaseRequestStatus.Pending,
                PurchaseRequestStatus.Sourcing,
                PurchaseRequestStatus.WaitingApproval,
                PurchaseRequestStatus.ReadyToOrder,
                PurchaseRequestStatus.Exception
            };

            foreach (PurchaseRequestStatus status in Enum.GetValues(typeof(PurchaseRequestStatus)))
            {
                // Ordered gets no checkbox at all (user request, 20 okt 2026) — this grid is for
                // PRs/PLs only, an actually-placed order belongs in the dedicated "Orders..." window.
                // Previously Ordered had a checkbox here too (unchecked by default, reachable on
                // demand); now it can never appear in this grid, full stop.
                if (status == PurchaseRequestStatus.Ordered) continue;

                // Checked is set before CheckedChanged is wired up, on purpose — otherwise building
                // these five checkboxes here would itself fire five local DB reloads while the form
                // is still being constructed, before Load ever runs (the constructor's whole point is
                // that nothing loads until the user clicks something).
                var checkBox = new CheckBox { Text = status.ToString(), AutoSize = true, Checked = defaultChecked.Contains(status), Margin = new Padding(0, 0, 0, 2) };
                _requestStatusCheckBoxes[status] = checkBox;
                layout.Controls.Add(checkBox);
            }

            foreach (var checkBox in _requestStatusCheckBoxes.Values)
            {
                checkBox.CheckedChanged += async (s, e) => await ReloadWithCurrentStatusFilterAsync();
            }

            // Uit by default (user request, 20 okt 2026: "dit zijn uitzonderingen, die wil ik niet
            // standaard zien") — handmatige testaanvragen ("TEST-"-prefix) worden normaal altijd
            // getoond ongeacht de status-/Due Date-/Select By-filters hierboven (zie
            // ApplyRangeAndDueDateFilters/GetOpenPurchaseRequestsAsync's eigen IsManualTestRequest-
            // uitzondering); dit vakje overstemt die uitzondering juist weer, los van de rest.
            _showTestRequestsCheckBox = new CheckBox
            {
                Text = "Testaanvragen tonen",
                AutoSize = true,
                Checked = false,
                Margin = new Padding(0, 8, 0, 2)
            };
            _showTestRequestsCheckBox.CheckedChanged += async (s, e) => await ReloadWithCurrentStatusFilterAsync();
            layout.Controls.Add(_showTestRequestsCheckBox);

            return WrapInGroupBox("Weergeven", layout);
        }

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

        private static void AddFilterRow(TableLayoutPanel layout, string label, Control control)
        {
            var row = layout.RowCount;
            layout.RowCount++;
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            if (string.IsNullOrEmpty(label))
            {
                layout.Controls.Add(control, 0, row);
                layout.SetColumnSpan(control, 2);
                return;
            }
            layout.Controls.Add(new Label { Text = label, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 4, 3, 2) }, 0, row);
            control.Margin = new Padding(3, 2, 0, 2);
            layout.Controls.Add(control, 1, row);
        }

        /// <summary>
        /// GroupBox.AutoSize with a manually-positioned (non-Dock) child turned out unreliable, so
        /// this reads the child's own (properly computed) PreferredSize and sizes the GroupBox
        /// explicitly around it instead. Dock=Fill for the child (tried in an earlier round) turned
        /// out worse, not better: GroupBox reserves its own built-in space for the border/label on
        /// top of whatever Padding is set, so a Dock=Fill child ends up squeezed into less room than
        /// this method actually sized the box for. A manually-placed (non-Dock) child at an explicit
        /// Location doesn't hit that double reservation, so that's what this goes back to — sized
        /// generously on the right/bottom since content clipping is a much worse failure mode than
        /// a little unused space in the box.
        /// </summary>
        private static GroupBox WrapInGroupBox(string title, Control content)
        {
            const int left = 12, top = 20, right = 24, bottom = 20;

            var group = new GroupBox { Text = title, Margin = new Padding(4) };
            content.Location = new Point(left, top);
            group.Controls.Add(content);

            var contentSize = content.PreferredSize;
            var titleWidth = TextRenderer.MeasureText(title, group.Font).Width + 24;
            group.Size = new Size(Math.Max(contentSize.Width + left + right, titleWidth), contentSize.Height + top + bottom);

            return group;
        }

        private void UpdateIncludedOrderStatuses()
        {
            var statuses = new HashSet<string>();
            if (_statusPlannedCheckBox.Checked) statuses.Add("1");
            if (_statusApprovedCheckBox.Checked) statuses.Add("2");
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
                .Where(r => _showTestRequestsCheckBox.Checked || !IsManualTestRequest(r.ErpRequestNumber))
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

            if (_dueDateEnableCheckBox.Checked)
            {
                var start = _dueDateStartPicker.Value.Date;
                var end = _dueDateEndPicker.Value.Date;
                result = result.Where(r => IsManualTestRequest(r.ErpRequestNumber) ||
                    (r.RequiredDate.HasValue && r.RequiredDate.Value.Date >= start && r.RequiredDate.Value.Date <= end));
            }

            var rangeStart = _rangeStartBox.Text.Trim();
            var rangeEnd = _rangeEndBox.Text.Trim();
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
                .Where(r => _showTestRequestsCheckBox.Checked || !IsManualTestRequest(r.ErpRequestNumber))
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

            _composition.ErpConnector.DueDateFilterStart = _dueDateEnableCheckBox.Checked ? _dueDateStartPicker.Value.Date : (DateTime?)null;
            _composition.ErpConnector.DueDateFilterEnd = _dueDateEnableCheckBox.Checked ? _dueDateEndPicker.Value.Date : (DateTime?)null;

            var rangeStart = _rangeStartBox.Text.Trim();
            var rangeEnd = _rangeEndBox.Text.Trim();
            var hasRange = !string.IsNullOrEmpty(rangeStart) || !string.IsNullOrEmpty(rangeEnd);

            _composition.ErpConnector.RangeFilterField = hasRange ? GetSelectedRangeField() : (MaxOrderRangeField?)null;
            _composition.ErpConnector.RangeFilterStart = string.IsNullOrEmpty(rangeStart) ? null : rangeStart;
            _composition.ErpConnector.RangeFilterEnd = string.IsNullOrEmpty(rangeEnd) ? null : rangeEnd;
        }

        private MaxOrderRangeField GetSelectedRangeField()
        {
            var text = _rangeFieldCombo.SelectedItem as string;
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
                        VendorID = line?.ErpArticleId != null && preferredVendorIdsByPartId.TryGetValue(line.ErpArticleId, out var preferredVendorId)
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
            _requestsGrid.SelectionChanged -= RequestsGrid_SelectionChanged;
            _requestsGrid.DataSource = rows;
            _requestsGrid.SelectionChanged += RequestsGrid_SelectionChanged;

            if (_requestsGrid.Columns["Id"] != null)
                _requestsGrid.Columns["Id"].Visible = false;
            SetHeaderText(_requestsGrid, "ManufacturingPart", "Manufacturing Part");
            SetHeaderText(_requestsGrid, "ExtCost", "Ext. Cost");
            SetHeaderText(_requestsGrid, "PoDueDate", "PO DueDate");
            SetHeaderText(_requestsGrid, "SelectedSupplier", "Selected Supplier");
            SetHeaderText(_requestsGrid, "SelectedPrice", "Selected Price");
            SetHeaderText(_requestsGrid, "SelectedQuantity", "Selected Qty");
            SetHeaderText(_requestsGrid, "SelectedDueDate", "Selected DueDate");
            ApplyCurrencyColumns(_requestsGrid, "SelectedPrice");
            ApplyQuantityColumns(_requestsGrid, "SelectedQuantity");
            ApplyDateColumns(_requestsGrid, "SelectedDueDate");
            ApplyMaxColumnWidths(_requestsGrid);
            ApplyFrozenColumns(_requestsGrid,
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
        /// call this). Every column named here gets AutoSizeMode.None, i.e. is excluded from the
        /// grid-level Fill distribution — without that, Fill would spread the remaining width evenly
        /// across short fixed-format columns (Status, Firm, Quantity, DueDate, ...) and long free-text
        /// ones (Desc1, Reference) alike, which looks arbitrary. Only genuinely variable-length text
        /// columns (Desc1, Reference) are left to Fill. SetHeaderText/SetFixedColumnWidth calls for a
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
            SetCharacterBasedColumnWidth(grid, "VendorID", 14);
            SetCharacterBasedColumnWidth(grid, "Workflow", 16);
            SetCharacterBasedColumnWidth(grid, "Manufacturer", 16);
            SetCharacterBasedColumnWidth(grid, "Packaging", 14);
            SetCharacterBasedColumnWidth(grid, "ReelRequirement", 8);
            SetCharacterBasedColumnWidth(grid, "SelectedSupplier", 14);
            SetCharacterBasedColumnWidth(grid, "SelectedPrice", 12);
            SetCharacterBasedColumnWidth(grid, "SelectedQuantity", 10);
            SetCharacterBasedColumnWidth(grid, "SelectedDueDate", 12);
            // Desc1/Reference (variable-length free text) are deliberately left out of the fixed
            // widths above — those are the columns Fill actually distributes the remaining window
            // width across. A MinimumWidth floor on both instead of leaving them fully unconstrained:
            // once the fixed columns above add up to more than the viewport (easily happens with this
            // many columns), Fill would otherwise keep shrinking Desc1/Reference toward unreadable
            // instead of ever giving up and showing a horizontal scrollbar for the overflow.
            SetMinimumWidth(grid, "Desc1", 180);
            SetMinimumWidth(grid, "Reference", 180);

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

        private async void RequestsGrid_SelectionChanged(object sender, EventArgs e)
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
                _offersGrid.DataSource = null;
                return;
            }

            var request = await _composition.PurchaseRequestRepository.GetByIdAsync(requestId.Value);
            var line = request?.Lines.FirstOrDefault();
            _currentLineId = line?.Id;
            if (line == null)
            {
                _offersGrid.DataSource = null;
                return;
            }

            var offers = await _composition.Engine.GetOffersForLineAsync(line.Id);
            var selection = await _composition.Engine.GetSelectionForLineAsync(line.Id);

            _offersGrid.DataSource = offers.Select(o => new OfferGridRow
            {
                Id = o.Id,
                Select = selection != null && selection.SelectedOfferId == o.Id,
                SupplierCode = o.SupplierCode,
                SupplierPartNumber = o.SupplierPartNumber,
                MatchConfidence = o.MatchConfidence.ToString(),
                OfferedQuantity = o.OfferedQuantity,
                UnitPrice = o.UnitPrice,
                Currency = o.Currency,
                Packaging = o.PackagingType.ToString(),
                Reel = o.ReelType?.ToString() ?? "-",
                LandedCost = o.LandedCost,
                LeadTimeDays = o.LeadTimeDays,
                AvailableQuantity = o.AvailableQuantity,
                DueDate = o.EstimatedDeliveryDate
            }).ToList();

            if (_offersGrid.Columns["Id"] != null)
                _offersGrid.Columns["Id"].Visible = false;
            SetHeaderText(_offersGrid, "SupplierCode", "Supplier");
            SetHeaderText(_offersGrid, "SupplierPartNumber", "Supplier Part Number");
            SetHeaderText(_offersGrid, "MatchConfidence", "Match");
            SetHeaderText(_offersGrid, "OfferedQuantity", "Quantity");
            SetHeaderText(_offersGrid, "UnitPrice", "Unit Price");
            SetHeaderText(_offersGrid, "LandedCost", "Landed Cost");
            SetHeaderText(_offersGrid, "LeadTimeDays", "Lead Time (Working Days)");
            SetHeaderText(_offersGrid, "AvailableQuantity", "Available Qty");
            SetHeaderText(_offersGrid, "DueDate", "Due Date");
            ApplyQuantityColumns(_offersGrid, "OfferedQuantity", "AvailableQuantity");
            ApplyCurrencyColumns(_offersGrid, "UnitPrice", "LandedCost");
            ApplyDateColumns(_offersGrid, "DueDate");
            // Smal genoeg voor een checkbox + "Select"-tekst, resp. voor "Currency" (de waarden zelf,
            // bv. "EUR", zijn altijd korter dan de kolomnaam) — anders trekt AutoSizeColumnsMode.Fill
            // ze even breed als de rest (user request, 20 okt 2026).
            SetCharacterBasedColumnWidth(_offersGrid, "Select", 8);
            SetCharacterBasedColumnWidth(_offersGrid, "Currency", 8);

            // Enkel "Select" mag bewerkt worden — zie de ReadOnly=false op de grid zelf.
            foreach (DataGridViewColumn column in _offersGrid.Columns)
                column.ReadOnly = column.Name != "Select";
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
            public decimal UnitPrice { get; set; }
            public string Currency { get; set; }
            public string Packaging { get; set; }
            public string Reel { get; set; }
            public decimal LandedCost { get; set; }
            public int LeadTimeDays { get; set; }
            public int AvailableQuantity { get; set; }
            public DateTime DueDate { get; set; }
        }

        /// <summary>
        /// Aanvinken kiest die offerte (SelectOfferManuallyAsync); uitvinken van de al-gekozen regel
        /// maakt die keuze weer ongedaan (DeselectOfferAsync, user request, 20 okt 2026 — workflow
        /// springt dan terug van ReadyToOrder naar Sourcing). Uitvinken van een andere (niet eerder
        /// gekozen) regel kan niet gebeuren — die stond al uit — dus die tak wordt nooit bereikt.
        /// </summary>
        private async System.Threading.Tasks.Task OffersGrid_CellValueChangedAsync(DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || _offersGrid.Columns[e.ColumnIndex].Name != "Select") return;
            if (_currentLineId == null) return;

            var row = _offersGrid.Rows[e.RowIndex];
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
            if (_requestsGrid.CurrentRow == null) return null;
            return (int)_requestsGrid.CurrentRow.Cells["Id"].Value;
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
            _requestsGrid.SelectedCells.Cast<DataGridViewCell>().Select(c => c.OwningRow).Distinct()
                .Select(r => (int)r.Cells["Id"].Value).ToList();

        private List<int> GetAllRequestIds() =>
            _requestsGrid.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow).Select(r => (int)r.Cells["Id"].Value).ToList();

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
                var skippedWaitingApproval = 0;
                var skippedOrdered = 0;

                for (var i = 0; i < requestIds.Count; i++)
                {
                    SetStatus($"Sourcing {i + 1}/{requestIds.Count}...");
                    try
                    {
                        // Re-sourcing a request that's already WaitingApproval would re-run every line
                        // that hasn't been decided on yet — SourceAndProcessLineAsync only skips a line
                        // once it has a SupplierSelection, which a line awaiting approval doesn't have
                        // yet, so it would get sourced again and end up with a second, duplicate
                        // ApprovalRequest. Harmless to re-run for Pending (never sourced) or Exception
                        // (worth retrying).
                        //
                        // Ordered must be skipped outright, not just "harmless to re-run": every line on
                        // an Ordered request already has a SupplierSelection and no pending approval or
                        // exception, so SourcePurchaseRequestAsync would recompute finalStatus as
                        // ReadyToOrder and silently downgrade an already-placed order back into the
                        // sourcing queue. Only reachable now that the "Weergeven" status filter can show
                        // Ordered rows at all — before that they were never in the grid to select.
                        var current = await _composition.PurchaseRequestRepository.GetByIdAsync(requestIds[i]);
                        if (current == null) continue;
                        if (current.Status == PurchaseRequestStatus.WaitingApproval)
                        {
                            skippedWaitingApproval++;
                            continue;
                        }
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
                if (skippedWaitingApproval > 0) summary += $", {skippedWaitingApproval} overgeslagen (al in afwachting van goedkeuring)";
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

        private void ShowOrderDetail()
        {
            var requestId = GetSelectedRequestId();
            if (requestId == null)
            {
                MessageBox.Show(this, "Selecteer eerst een aanvraag.", "Geen selectie", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var form = new OrderDetailForm(_composition.PurchaseRequestRepository, _composition.PurchaseOrderRepository, requestId.Value))
            {
                form.ShowDialog(this);
            }
        }

        private void ShowOrders()
        {
            using (var form = new OrdersForm(_composition.PurchaseOrderRepository, _composition.PurchaseRequestRepository))
            {
                form.ShowDialog(this);
            }
        }

        private async System.Threading.Tasks.Task ShowApprovalsAsync()
        {
            using (var form = new ApprovalForm(_composition.Engine))
            {
                form.ShowDialog(this);
            }
            await LoadLocalRequestsAsync();
        }

        private void ShowSupplierMapping()
        {
            using (var form = new SupplierMappingForm(_composition.SupplierProductMappingRepository))
            {
                form.ShowDialog(this);
            }
        }

        private void ShowSettings()
        {
            using (var form = new SettingsForm(_composition.PolicyRepository, _composition.SupplierRepository))
            {
                form.ShowDialog(this);
            }
        }

        private void ShowAuditLog()
        {
            using (var form = new AuditLogForm(_composition.ProcurementEventRepository))
            {
                form.ShowDialog(this);
            }
        }

        private void SetStatus(string text) => _statusLabel.Text = text;

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
            // rebinds _requestsGrid.DataSource while UseWaitCursor is still true, mid-query. Forcing
            // both grids back to Cursors.Default on the way out clears that stale state explicitly.
            if (!busy)
            {
                _requestsGrid.Cursor = Cursors.Default;
                _offersGrid.Cursor = Cursors.Default;
            }

            var enabled = !busy;
            _queryButton.Enabled = enabled;
            _newRequestButton.Enabled = enabled;
            _startSourcingButton.Enabled = enabled;
            _sourceAllButton.Enabled = enabled;
            _placeOrdersButton.Enabled = enabled;
            _processConfirmationsButton.Enabled = enabled;
            _orderDetailButton.Enabled = enabled;
            _ordersButton.Enabled = enabled;
            _approvalsButton.Enabled = enabled;
            _supplierMappingButton.Enabled = enabled;
            _settingsButton.Enabled = enabled;
            _auditLogButton.Enabled = enabled;
            // Not strictly a race on shared MAX SqlConnections like the buttons above (their
            // CheckedChanged handler already no-ops via the same _isBusy check) — disabled anyway so
            // a toggle during a Query can't silently fail to visually match the grid until the next
            // unrelated reload.
            foreach (var checkBox in _requestStatusCheckBoxes.Values)
                checkBox.Enabled = enabled;
        }
    }
}
