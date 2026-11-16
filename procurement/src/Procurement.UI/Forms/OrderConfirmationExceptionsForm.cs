using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Procurement.Core.Models;
using Procurement.UI.Support;
using static Procurement.UI.Support.GridFormatting;

namespace Procurement.UI.Forms
{
    /// <summary>
    /// Read-only result of "Orderbevestiging verwerken" (MainForm) — every checked PO already had
    /// its confirmation applied automatically (local tracking, and MAX's Confirming/Reference/
    /// duedate fields in real mode) by the time this dialog opens; this only lists the lines that
    /// need a human look (quantity/price deviation, or a delivery date confirmed later than
    /// requested), not a report of everything that went through cleanly.
    /// Built with the Visual Studio Designer (user request, 16 nov 2026); grid columns come from the
    /// DataSource, so their widths are set here. No Fill mode (user rule, 16 nov 2026).
    /// </summary>
    public partial class OrderConfirmationExceptionsForm : Form
    {
        public OrderConfirmationExceptionsForm(IReadOnlyList<OrderConfirmationException> exceptions)
        {
            InitializeComponent();
            ClassicStyle.ApplyClassicStyle(this);
            this.BackColor = ClassicStyle.ClassicScreenBackColor;
            EnableRowNumberHeaders(dgv_exceptions);
            GridColumnSettingsHelper.Attach(this); // remembers column widths per user, like UniPro

            dgv_exceptions.DataSource = (exceptions ?? Array.Empty<OrderConfirmationException>()).Select(e => new
            {
                e.ErpPoNumber,
                e.SupplierCode,
                e.SupplierPartNumber,
                e.OrderedQuantity,
                e.ConfirmedQuantity,
                e.OrderedUnitPrice,
                e.ConfirmedUnitPrice,
                e.RequiredDate,
                e.ConfirmedShipDate,
                Reason = DescribeReason(e)
            }).ToList();

            ApplyQuantityColumns(dgv_exceptions, "OrderedQuantity", "ConfirmedQuantity");
            ApplyCurrencyColumns(dgv_exceptions, "OrderedUnitPrice", "ConfirmedUnitPrice");
            ApplyDateColumns(dgv_exceptions, "RequiredDate", "ConfirmedShipDate");
            SetCharacterBasedColumnWidth(dgv_exceptions, "ErpPoNumber", 12);
            SetCharacterBasedColumnWidth(dgv_exceptions, "SupplierCode", 12);
            SetCharacterBasedColumnWidth(dgv_exceptions, "SupplierPartNumber", 20);
            SetCharacterBasedColumnWidth(dgv_exceptions, "OrderedQuantity", 12);
            SetCharacterBasedColumnWidth(dgv_exceptions, "ConfirmedQuantity", 14);
            SetCharacterBasedColumnWidth(dgv_exceptions, "OrderedUnitPrice", 14);
            SetCharacterBasedColumnWidth(dgv_exceptions, "ConfirmedUnitPrice", 16);
            SetCharacterBasedColumnWidth(dgv_exceptions, "RequiredDate", 12);
            SetCharacterBasedColumnWidth(dgv_exceptions, "ConfirmedShipDate", 14);
            SetCharacterBasedColumnWidth(dgv_exceptions, "Reason", 36);
        }

        private void bt_close_Click(object sender, EventArgs e) => Close();

        private static string DescribeReason(OrderConfirmationException e)
        {
            var reasons = new List<string>();
            if (e.QuantityMismatch) reasons.Add("quantity mismatch");
            if (e.PriceMismatch) reasons.Add("price mismatch");
            if (e.DeliveryIsLate) reasons.Add("delivery late");
            return string.Join(", ", reasons);
        }
    }
}
