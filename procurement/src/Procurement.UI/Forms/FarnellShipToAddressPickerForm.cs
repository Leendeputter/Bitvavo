using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Procurement.Suppliers.Farnell;
using static Procurement.UI.Support.GridFormatting;

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
    /// </summary>
    public class FarnellShipToAddressPickerForm : Form
    {
        private readonly List<FarnellShipToAddress> _addresses;
        private ListBox _list;

        public string SelectedAddressId { get; private set; }

        public FarnellShipToAddressPickerForm(IReadOnlyList<FarnellShipToAddress> addresses)
        {
            _addresses = addresses?.ToList() ?? new List<FarnellShipToAddress>();
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = "Choose Farnell Shipping Address";
            Width = 480;
            Height = 380;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            BackColor = ClassicScreenBackColor;

            var hint = new Label
            {
                Dock = DockStyle.Top,
                Height = 30,
                Padding = new Padding(8),
                Text = "This Farnell order needs a shipping address — pick one below:"
            };

            _list = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
            foreach (var address in _addresses)
                _list.Items.Add(FormatAddress(address));
            if (_list.Items.Count > 0)
                _list.SelectedIndex = 0;

            var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10) };
            var okButton = new Button { Text = "OK", AutoSize = true, DialogResult = DialogResult.OK };
            ApplyClassicButtonStyle(okButton);
            okButton.Click += (s, e) =>
            {
                SelectedAddressId = _list.SelectedIndex >= 0 ? _addresses[_list.SelectedIndex].AddressId : null;
            };
            var cancelButton = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
            ApplyClassicButtonStyle(cancelButton);
            buttonPanel.Controls.Add(cancelButton);
            buttonPanel.Controls.Add(okButton);

            Controls.Add(_list);
            Controls.Add(buttonPanel);
            Controls.Add(hint);
            AcceptButton = okButton;
            CancelButton = cancelButton;
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
