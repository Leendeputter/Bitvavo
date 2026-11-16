using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Procurement.Suppliers.Farnell;
using Procurement.UI.Support;

namespace Procurement.UI.Forms
{
    /// <summary>
    /// Shown by MainForm before placing an order that touches Farnell (user request, 21 okt 2026:
    /// "Misschien dan toch bij iedere order laten vragen om het shipping address, waarbij de
    /// default wel al ingevuld staat... dan hoeven we de database ook niet aan te passen" — picked
    /// fresh from FarnellAdapter.GetAvailableShipToAddressesAsync's live list every time, nothing
    /// stored). The first address in that list is pre-selected; the user can pick any other before
    /// confirming. Only ever shown when there's more than one address to choose from — MainForm
    /// skips this dialog entirely otherwise.
    /// Built with the Visual Studio Designer (user request, 16 nov 2026); the address list is filled here.
    /// </summary>
    public partial class FarnellShipToAddressPickerForm : Form
    {
        private readonly List<FarnellShipToAddress> _addresses;

        public string SelectedAddressId { get; private set; }

        public FarnellShipToAddressPickerForm(IReadOnlyList<FarnellShipToAddress> addresses)
        {
            _addresses = addresses?.ToList() ?? new List<FarnellShipToAddress>();
            InitializeComponent();
            ClassicStyle.ApplyClassicStyle(this);
            this.BackColor = ClassicStyle.ClassicScreenBackColor;

            foreach (var address in _addresses)
                lst_addresses.Items.Add(FormatAddress(address));
            if (lst_addresses.Items.Count > 0)
                lst_addresses.SelectedIndex = 0;
        }

        private void bt_ok_Click(object sender, EventArgs e) => Confirm();

        private void lst_addresses_DoubleClick(object sender, EventArgs e) => Confirm();

        private void Confirm()
        {
            SelectedAddressId = lst_addresses.SelectedIndex >= 0 ? _addresses[lst_addresses.SelectedIndex].AddressId : null;
            DialogResult = DialogResult.OK;
        }

        private static string FormatAddress(FarnellShipToAddress address)
        {
            var parts = new[]
            {
                address.NickName,
                address.OrganizationName,
                address.AddressLine1,
                address.City,
                address.PostalCode,
                address.Country
            }.Where(p => !string.IsNullOrWhiteSpace(p));
            return string.Join(", ", parts);
        }
    }
}
