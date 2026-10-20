using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Procurement.Core.Entities;
using Procurement.Core.Enums;
using Procurement.Data.Repositories;
using Procurement.Erp;

namespace Procurement.UI.Forms
{
    /// <summary>Simple test-data entry form (spec §8.1) — stands in for the not-yet-connected real ERP.</summary>
    public class NewPurchaseRequestForm : Form
    {
        private readonly PurchaseRequestRepository _repository;
        private readonly MaxOrderRepository _maxOrderRepository;

        private string _lookedUpDesc1;
        private string _lookedUpDesc2;
        private string _lookedUpPartType;
        private string _lookedUpCustomer;

        private TextBox _erpArticleIdBox;
        private TextBox _manufacturerBox;
        private TextBox _mpnBox;
        private TextBox _descriptionBox;
        private NumericUpDown _quantityBox;
        private DateTimePicker _requiredDatePicker;
        private ComboBox _packagingCombo;
        private CheckBox _reelRequiredCheck;
        private TextBox _warehouseBox;
        private TextBox _projectBox;
        private NumericUpDown _priorityBox;

        public NewPurchaseRequestForm(PurchaseRequestRepository repository, MaxOrderRepository maxOrderRepository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _maxOrderRepository = maxOrderRepository ?? throw new ArgumentNullException(nameof(maxOrderRepository));
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = "Add New Test Request";
            Width = 480;
            Height = 480;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(10), AutoSize = true };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));

            _erpArticleIdBox = new TextBox { Dock = DockStyle.Fill, Text = "ART-" + DateTime.UtcNow.Ticks % 100000 };
            var lookupButton = new Button { Text = "Fetch from MAX", AutoSize = true };
            lookupButton.Click += async (s, e) => await LookupFromMaxAsync();
            var articleIdPanel = new Panel { Dock = DockStyle.Fill, Height = 24 };
            _erpArticleIdBox.Dock = DockStyle.Left;
            _erpArticleIdBox.Width = 140;
            lookupButton.Dock = DockStyle.Right;
            articleIdPanel.Controls.Add(_erpArticleIdBox);
            articleIdPanel.Controls.Add(lookupButton);
            _manufacturerBox = new TextBox { Dock = DockStyle.Fill, Text = "Vishay" };
            _mpnBox = new TextBox { Dock = DockStyle.Fill, Text = "CRCW060310K0FKEA" };
            _descriptionBox = new TextBox { Dock = DockStyle.Fill };
            _quantityBox = new NumericUpDown { Dock = DockStyle.Fill, Minimum = 1, Maximum = 10000000, Value = 12000 };
            _requiredDatePicker = new DateTimePicker { Dock = DockStyle.Fill, Value = DateTime.Today.AddDays(14) };
            _packagingCombo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _packagingCombo.Items.AddRange(Enum.GetNames(typeof(PackagingRequirement)));
            _packagingCombo.SelectedItem = nameof(PackagingRequirement.OriginalReel);
            _reelRequiredCheck = new CheckBox { Dock = DockStyle.Fill, Checked = true, Text = "Reel Required" };
            _warehouseBox = new TextBox { Dock = DockStyle.Fill, Text = "WAREHOUSE-1" };
            _projectBox = new TextBox { Dock = DockStyle.Fill, Text = "PROTOTYPE" };
            _priorityBox = new NumericUpDown { Dock = DockStyle.Fill, Minimum = 1, Maximum = 10, Value = 5 };

            AddRow(layout, "Internal Article Number:", articleIdPanel);
            AddRow(layout, "Manufacturer:", _manufacturerBox);
            AddRow(layout, "Manufacturer Part Number:", _mpnBox);
            AddRow(layout, "Description:", _descriptionBox);
            AddRow(layout, "Requested Quantity:", _quantityBox);
            AddRow(layout, "Required Date:", _requiredDatePicker);
            AddRow(layout, "Packaging Requirement:", _packagingCombo);
            AddRow(layout, string.Empty, _reelRequiredCheck);
            AddRow(layout, "Warehouse:", _warehouseBox);
            AddRow(layout, "Project:", _projectBox);
            AddRow(layout, "Priority (1=high):", _priorityBox);

            var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 40 };
            var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
            var okButton = new Button { Text = "Save" };
            okButton.Click += async (s, e) => await SaveAsync();
            buttonPanel.Controls.Add(cancelButton);
            buttonPanel.Controls.Add(okButton);

            Controls.Add(layout);
            Controls.Add(buttonPanel);
            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        private static void AddRow(TableLayoutPanel layout, string label, Control control)
        {
            var row = layout.RowCount;
            layout.RowCount++;
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 0, row);
            layout.Controls.Add(control, 1, row);
        }

        /// <summary>
        /// Looks up Part_Master by the typed "intern artikelnummer" (MaxOrderRepository.
        /// GetPartByIdAsync) and fills in Desc1/Desc2/PartType/Customer — the same MAX fields a real
        /// synced order already gets (MaxOrder/MaxErpConnector), but a manually-entered test request
        /// never had an equivalent lookup for. Also fills "Omschrijving" from Desc1+Desc2 (only if
        /// still empty, so it never overwrites something the user already typed), mirroring
        /// MaxOrder.Description's own convention — that field is what sourcing/matching actually
        /// reads, Desc1/Desc2 themselves stay display-only.
        /// </summary>
        private async System.Threading.Tasks.Task LookupFromMaxAsync()
        {
            var partId = _erpArticleIdBox.Text.Trim();
            if (string.IsNullOrEmpty(partId)) return;

            var part = await _maxOrderRepository.GetPartByIdAsync(partId);
            if (part == null)
            {
                MessageBox.Show(this, $"Geen Part_Master-rij gevonden voor '{partId}'.", "Niet gevonden", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _lookedUpDesc1 = part.Desc1;
            _lookedUpDesc2 = part.Desc2;
            _lookedUpPartType = part.PartType;
            _lookedUpCustomer = part.Customer;

            if (string.IsNullOrWhiteSpace(_descriptionBox.Text))
            {
                var descriptionParts = new List<string>();
                if (!string.IsNullOrWhiteSpace(part.Desc1)) descriptionParts.Add(part.Desc1);
                if (!string.IsNullOrWhiteSpace(part.Desc2)) descriptionParts.Add(part.Desc2);
                _descriptionBox.Text = string.Join(" ", descriptionParts);
            }
        }

        private async System.Threading.Tasks.Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(_mpnBox.Text))
            {
                MessageBox.Show(this, "Manufacturer part number is verplicht.", "Validatie", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var request = new PurchaseRequest
            {
                ErpRequestNumber = "TEST-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
                RequestDate = DateTime.UtcNow,
                RequiredDate = _requiredDatePicker.Value,
                Warehouse = _warehouseBox.Text,
                Project = _projectBox.Text,
                Priority = (int)_priorityBox.Value,
                Status = PurchaseRequestStatus.Pending,
                Lines = new List<PurchaseRequestLine>
                {
                    new PurchaseRequestLine
                    {
                        ErpArticleId = _erpArticleIdBox.Text,
                        Manufacturer = _manufacturerBox.Text,
                        ManufacturerPartNumber = _mpnBox.Text,
                        Description = _descriptionBox.Text,
                        Desc1 = _lookedUpDesc1,
                        Desc2 = _lookedUpDesc2,
                        PartType = _lookedUpPartType,
                        Customer = _lookedUpCustomer,
                        RequestedQuantity = (int)_quantityBox.Value,
                        RequiredDate = _requiredDatePicker.Value,
                        PackagingRequirement = (PackagingRequirement)Enum.Parse(typeof(PackagingRequirement), (string)_packagingCombo.SelectedItem),
                        ReelRequirement = _reelRequiredCheck.Checked
                    }
                }
            };

            await _repository.AddAsync(request);
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
