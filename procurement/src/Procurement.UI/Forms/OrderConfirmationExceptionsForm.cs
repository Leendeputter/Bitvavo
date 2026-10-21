using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Procurement.Core.Models;
using static Procurement.UI.Support.GridFormatting;

namespace Procurement.UI.Forms
{
    /// <summary>
    /// Read-only result of "Orderbevestiging verwerken" (MainForm) — every checked PO already had
    /// its confirmation applied automatically (local tracking, and MAX's Confirming/Reference/
    /// duedate fields in real mode) by the time this dialog opens; this only lists the lines that
    /// need a human look (quantity/price deviation, or a delivery date confirmed later than
    /// requested), not a report of everything that went through cleanly.
    /// </summary>
    public class OrderConfirmationExceptionsForm : Form
    {
        private DataGridView _grid;

        public OrderConfirmationExceptionsForm(IReadOnlyList<OrderConfirmationException> exceptions)
        {
            InitializeComponent();
            _grid.DataSource = exceptions.Select(e => new
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

            ApplyQuantityColumns(_grid, "OrderedQuantity", "ConfirmedQuantity");
            ApplyCurrencyColumns(_grid, "OrderedUnitPrice", "ConfirmedUnitPrice");
            ApplyDateColumns(_grid, "RequiredDate", "ConfirmedShipDate");
            // Reason is the one genuinely free-text column here, left in Fill with a floor.
            SetCharacterBasedColumnWidth(_grid, "ErpPoNumber", 12);
            SetCharacterBasedColumnWidth(_grid, "SupplierCode", 12);
            SetMinimumWidth(_grid, "SupplierPartNumber", 140);
            SetCharacterBasedColumnWidth(_grid, "OrderedQuantity", 12);
            SetCharacterBasedColumnWidth(_grid, "ConfirmedQuantity", 14);
            SetCharacterBasedColumnWidth(_grid, "OrderedUnitPrice", 14);
            SetCharacterBasedColumnWidth(_grid, "ConfirmedUnitPrice", 16);
            SetCharacterBasedColumnWidth(_grid, "RequiredDate", 12);
            SetCharacterBasedColumnWidth(_grid, "ConfirmedShipDate", 14);
            SetMinimumWidth(_grid, "Reason", 160);
        }

        private void InitializeComponent()
        {
            Text = "Order Confirmation – Deviations";
            Width = 1000;
            Height = 500;
            StartPosition = FormStartPosition.CenterParent;
            // "Lichtgrijze formulierachtergrond" (user request, 20 okt 2026) — explicit so every
            // screen matches on purpose, not by coincidence of an unset property.
            BackColor = System.Drawing.SystemColors.Control;

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.RowHeaderSelect,
                MultiSelect = false
            };
            EnableDoubleBuffering(_grid);
            EnableRowNumberHeaders(_grid);
            ApplyClassicHeaderStyle(_grid);

            var closeButton = new Button { Text = "Close", AutoSize = true, Dock = DockStyle.Bottom };
            closeButton.Click += (s, e) => Close();

            Controls.Add(_grid);
            Controls.Add(closeButton);
        }

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
