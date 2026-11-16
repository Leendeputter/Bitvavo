using System.Windows.Forms;
using Procurement.UI.Support;

namespace Procurement.UI.Forms
{
    /// <summary>
    /// Settings > Suppliers > "Authorize Ordering..." for DigiKey. DigiKey rejects any "http://"
    /// callback URL, so the app uses "https://localhost": nothing listens there, the browser fails to
    /// load "https://localhost/?code=...&amp;state=...", and the user copies that URL from the address
    /// bar into this dialog. Built with the Visual Studio Designer (user request, 16 nov 2026).
    /// </summary>
    public partial class DigiKeyAuthorizeForm : Form
    {
        public DigiKeyAuthorizeForm()
        {
            InitializeComponent();
            ClassicStyle.ApplyClassicStyle(this);
            this.BackColor = ClassicStyle.ClassicScreenBackColor;
        }

        /// <summary>The URL the user pasted.</summary>
        public string RedirectUrl => txt_redirectUrl.Text.Trim();
    }
}
