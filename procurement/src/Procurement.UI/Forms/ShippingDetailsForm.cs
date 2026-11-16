using System.Windows.Forms;
using Procurement.Core.Entities;
using Procurement.UI.Support;

namespace Procurement.UI.Forms
{
    /// <summary>
    /// Settings > Suppliers > "Edit Shipping Details...": the account and ship-to contact a real
    /// Ordering API call needs (see Supplier.cs). Not secret, so the current values are pre-filled.
    /// Built with the Visual Studio Designer (user request, 16 nov 2026); SettingsForm reads the
    /// values back from the properties below after OK and does the save.
    /// </summary>
    public partial class ShippingDetailsForm : Form
    {
        public ShippingDetailsForm(Supplier supplier)
        {
            InitializeComponent();
            ClassicStyle.ApplyClassicStyle(this);
            this.BackColor = ClassicStyle.ClassicScreenBackColor;

            if (supplier == null) return;
            this.Text += " – " + supplier.SupplierCode;
            txt_accountId.Text = supplier.AccountId;
            txt_contactName.Text = supplier.ContactName;
            txt_contactEmail.Text = supplier.ContactEmail;
            txt_contactTelephone.Text = supplier.ContactTelephone;
            txt_addressLine1.Text = supplier.AddressLine1;
            txt_addressLine2.Text = supplier.AddressLine2;
            txt_city.Text = supplier.City;
            txt_province.Text = supplier.Province;
            txt_postalCode.Text = supplier.PostalCode;
            txt_countryCode.Text = supplier.CountryCode;
        }

        public string AccountId => txt_accountId.Text;
        public string ContactName => txt_contactName.Text;
        public string ContactEmail => txt_contactEmail.Text;
        public string ContactTelephone => txt_contactTelephone.Text;
        public string AddressLine1 => txt_addressLine1.Text;
        public string AddressLine2 => txt_addressLine2.Text;
        public string City => txt_city.Text;
        public string Province => txt_province.Text;
        public string PostalCode => txt_postalCode.Text;
        public string CountryCode => txt_countryCode.Text;
    }
}
