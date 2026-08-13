using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Procurement.Core.Entities;
using Procurement.Core.Enums;
using Procurement.Data.Repositories;

namespace Procurement.UI.Forms
{
    /// <summary>Simple test-data entry form (spec §8.1) — stands in for the not-yet-connected real ERP.</summary>
    public class NewPurchaseRequestForm : Form
    {
        private readonly PurchaseRequestRepository _repository;

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

        public NewPurchaseRequestForm(PurchaseRequestRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = "Nieuwe testaanvraag toevoegen";
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
            _manufacturerBox = new TextBox { Dock = DockStyle.Fill, Text = "Vishay" };
            _mpnBox = new TextBox { Dock = DockStyle.Fill, Text = "CRCW060310K0FKEA" };
            _descriptionBox = new TextBox { Dock = DockStyle.Fill };
            _quantityBox = new NumericUpDown { Dock = DockStyle.Fill, Minimum = 1, Maximum = 10000000, Value = 12000 };
            _requiredDatePicker = new DateTimePicker { Dock = DockStyle.Fill, Value = DateTime.Today.AddDays(14) };
            _packagingCombo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _packagingCombo.Items.AddRange(Enum.GetNames(typeof(PackagingRequirement)));
            _packagingCombo.SelectedItem = nameof(PackagingRequirement.OriginalReel);
            _reelRequiredCheck = new CheckBox { Dock = DockStyle.Fill, Checked = true, Text = "Reel vereist" };
            _warehouseBox = new TextBox { Dock = DockStyle.Fill, Text = "MAGAZIJN-1" };
            _projectBox = new TextBox { Dock = DockStyle.Fill, Text = "PROTOTYPE" };
            _priorityBox = new NumericUpDown { Dock = DockStyle.Fill, Minimum = 1, Maximum = 10, Value = 5 };

            AddRow(layout, "Intern artikelnummer:", _erpArticleIdBox);
            AddRow(layout, "Manufacturer:", _manufacturerBox);
            AddRow(layout, "Manufacturer part number:", _mpnBox);
            AddRow(layout, "Omschrijving:", _descriptionBox);
            AddRow(layout, "Gevraagde hoeveelheid:", _quantityBox);
            AddRow(layout, "Gewenste leverdatum:", _requiredDatePicker);
            AddRow(layout, "Packaging-eis:", _packagingCombo);
            AddRow(layout, string.Empty, _reelRequiredCheck);
            AddRow(layout, "Magazijn:", _warehouseBox);
            AddRow(layout, "Project:", _projectBox);
            AddRow(layout, "Prioriteit (1=hoog):", _priorityBox);

            var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 40 };
            var cancelButton = new Button { Text = "Annuleren", DialogResult = DialogResult.Cancel };
            var okButton = new Button { Text = "Opslaan" };
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
