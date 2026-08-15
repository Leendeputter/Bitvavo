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
                Reden = DescribeReason(e)
            }).ToList();

            ApplyQuantityColumns(_grid, "OrderedQuantity", "ConfirmedQuantity");
            ApplyCurrencyColumns(_grid, "OrderedUnitPrice", "ConfirmedUnitPrice");
            ApplyDateColumns(_grid, "RequiredDate", "ConfirmedShipDate");
        }

        private void InitializeComponent()
        {
            Text = "Orderbevestiging – afwijkingen";
            Width = 1000;
            Height = 500;
            StartPosition = FormStartPosition.CenterParent;

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false
            };
            EnableDoubleBuffering(_grid);

            var closeButton = new Button { Text = "Sluiten", AutoSize = true, Dock = DockStyle.Bottom };
            closeButton.Click += (s, e) => Close();

            Controls.Add(_grid);
            Controls.Add(closeButton);
        }

        private static string DescribeReason(OrderConfirmationException e)
        {
            var reasons = new List<string>();
            if (e.QuantityMismatch) reasons.Add("aantal wijkt af");
            if (e.PriceMismatch) reasons.Add("prijs wijkt af");
            if (e.DeliveryIsLate) reasons.Add("levering te laat");
            return string.Join(", ", reasons);
        }
    }
}
