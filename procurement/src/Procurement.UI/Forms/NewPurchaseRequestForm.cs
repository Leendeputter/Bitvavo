using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Procurement.Core.Entities;
using Procurement.Core.Enums;
using Procurement.Data.Repositories;
using Procurement.Erp;
using Procurement.UI.Support;

namespace Procurement.UI.Forms
{
    /// <summary>Simple test-data entry form (spec §8.1) — stands in for the not-yet-connected real ERP.
    /// Built with the Visual Studio Designer (user request, 16 nov 2026).</summary>
    public partial class NewPurchaseRequestForm : Form
    {
        private readonly PurchaseRequestRepository _repository;
        private readonly MaxOrderRepository _maxOrderRepository;

        private string _lookedUpDesc1;
        private string _lookedUpDesc2;
        private string _lookedUpPartType;
        private string _lookedUpCustomer;

        public NewPurchaseRequestForm(PurchaseRequestRepository repository, MaxOrderRepository maxOrderRepository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _maxOrderRepository = maxOrderRepository ?? throw new ArgumentNullException(nameof(maxOrderRepository));
            InitializeComponent();
            ClassicStyle.ApplyClassicStyle(this);
            this.BackColor = ClassicStyle.ClassicScreenBackColor;

            // Defaults that depend on "now" or on an enum stay in code; fixed test defaults are in the Designer.
            txt_articleId.Text = "ART-" + DateTime.UtcNow.Ticks % 100000;
            dtp_requiredDate.Value = DateTime.Today.AddDays(14);
            cb_packaging.Items.AddRange(Enum.GetNames(typeof(PackagingRequirement)));
            cb_packaging.SelectedItem = nameof(PackagingRequirement.OriginalReel);
        }

        private async void bt_fetchFromMax_Click(object sender, EventArgs e) => await LookupFromMaxAsync();

        private async void bt_save_Click(object sender, EventArgs e) => await SaveAsync();

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
            var partId = txt_articleId.Text.Trim();
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

            if (string.IsNullOrWhiteSpace(txt_description.Text))
            {
                var descriptionParts = new List<string>();
                if (!string.IsNullOrWhiteSpace(part.Desc1)) descriptionParts.Add(part.Desc1);
                if (!string.IsNullOrWhiteSpace(part.Desc2)) descriptionParts.Add(part.Desc2);
                txt_description.Text = string.Join(" ", descriptionParts);
            }
        }

        private async System.Threading.Tasks.Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(txt_mpn.Text))
            {
                MessageBox.Show(this, "Manufacturer part number is verplicht.", "Validatie", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var request = new PurchaseRequest
            {
                ErpRequestNumber = "TEST-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
                RequestDate = DateTime.UtcNow,
                RequiredDate = dtp_requiredDate.Value,
                Warehouse = txt_warehouse.Text,
                Project = txt_project.Text,
                Priority = (int)nud_priority.Value,
                Status = PurchaseRequestStatus.Pending,
                Lines = new List<PurchaseRequestLine>
                {
                    new PurchaseRequestLine
                    {
                        ErpArticleId = txt_articleId.Text,
                        Manufacturer = txt_manufacturer.Text,
                        ManufacturerPartNumber = txt_mpn.Text,
                        Description = txt_description.Text,
                        Desc1 = _lookedUpDesc1,
                        Desc2 = _lookedUpDesc2,
                        PartType = _lookedUpPartType,
                        Customer = _lookedUpCustomer,
                        RequestedQuantity = (int)nud_quantity.Value,
                        RequiredDate = dtp_requiredDate.Value,
                        PackagingRequirement = (PackagingRequirement)Enum.Parse(typeof(PackagingRequirement), (string)cb_packaging.SelectedItem),
                        ReelRequirement = chk_reelRequired.Checked
                    }
                }
            };

            await _repository.AddAsync(request);
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
