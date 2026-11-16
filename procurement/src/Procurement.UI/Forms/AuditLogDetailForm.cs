using System;
using System.Windows.Forms;
using Procurement.UI.Support;

namespace Procurement.UI.Forms
{
    /// <summary>
    /// Full, untruncated text of one Audit Log row, opened by double-clicking it (AuditLogForm).
    /// Error/RequestPayload/ResponsePayload often hold a whole HTTP error body. Built with the
    /// Visual Studio Designer (user request, 16 nov 2026).
    /// </summary>
    public partial class AuditLogDetailForm : Form
    {
        public AuditLogDetailForm(string detailText)
        {
            InitializeComponent();
            ClassicStyle.ApplyClassicStyle(this);
            this.BackColor = ClassicStyle.ClassicScreenBackColor;
            txt_detail.Text = detailText ?? string.Empty;
        }

        private void bt_copy_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(txt_detail.Text))
                Clipboard.SetText(txt_detail.Text);
        }
    }
}
