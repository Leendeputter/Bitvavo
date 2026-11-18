/* Purchase Orders (web), read-only, as the desktop's OrdersForm. */
(function (P) {
    'use strict';
    function url(action) { return P.appRoot + 'Orders/' + action; }
    var status = new P.Status(document.getElementById('status'), [document.getElementById('bt_refresh')]);
    var includeDeleted = document.getElementById('chk_includeDeleted');
    var includeCompleted = document.getElementById('chk_includeCompleted');

    var linesGrid = new P.Grid(document.getElementById('grid_lines'), {
        id: 'orderlines',
        emptyText: 'Geen regels.',
        rowClass: function (row) { return row.Deviates ? 'deviates' : ''; },
        columns: [
            { key: 'Id', hidden: true },
            { key: 'ErpRequestNumber', header: 'ErpRequestNumber', width: 95 },
            { key: 'PartID', header: 'PartID', width: 110 },
            { key: 'SupplierPartNumber', header: 'SupplierPartNumber', width: 160 },
            { key: 'MaxStatus', header: 'Status', width: 60 },
            { key: 'Confirmed', header: 'Confirmed', width: 75, type: 'bool' },
            { key: 'Quantity', header: 'Quantity', width: 90, type: 'quantity' },
            { key: 'ConfirmedQuantity', header: 'ConfirmedQuantity', width: 110, type: 'quantity' },
            { key: 'QuantityMismatch', header: 'Qty Mismatch?', width: 95, type: 'bool', nullable: true },
            { key: 'UnitPrice', header: 'UnitPrice', width: 95, type: 'money' },
            { key: 'ConfirmedUnitPrice', header: 'ConfirmedUnitPrice', width: 115, type: 'money' },
            { key: 'PriceMismatch', header: 'Price Mismatch?', width: 100, type: 'bool', nullable: true },
            { key: 'RequiredDate', header: 'RequiredDate', width: 90, type: 'date' },
            { key: 'ConfirmedShipDate', header: 'ConfirmedShipDate', width: 110, type: 'date' },
            { key: 'DeliveryIsLate', header: 'Late?', width: 55, type: 'bool', nullable: true },
            { key: 'LineTotal', header: 'LineTotal', width: 105, type: 'money' }
        ]
    });

    var ordersGrid = new P.Grid(document.getElementById('grid_orders'), {
        id: 'orders',
        emptyText: 'Nog geen orders.',
        onCurrentChanged: function () { loadLines(); },
        columns: [
            { key: 'Id', hidden: true },
            { key: 'ErpPoNumber', header: 'ErpPoNumber', width: 95 },
            { key: 'Supplier', header: 'Supplier', width: 95 },
            { key: 'SupplierOrderNumber', header: 'SupplierOrderNumber', width: 125 },
            { key: 'Status', header: 'Status', width: 110 },
            { key: 'CreatedAt', header: 'CreatedAt', width: 145, type: 'dateTime' },
            { key: 'SubmittedAt', header: 'SubmittedAt', width: 145, type: 'dateTime' },
            { key: 'ConfirmedAt', header: 'ConfirmedAt', width: 145, type: 'dateTime' },
            { key: 'OrderTotal', header: 'OrderTotal', width: 110, type: 'money' },
            { key: 'Currency', header: 'Currency', width: 65 },
            { key: 'Lines', header: 'Lines', width: 60, type: 'integer' }
        ]
    });

    function loadOrders() {
        return P.request('GET', url('List') + '?includeDeleted=' + includeDeleted.checked).then(function (result) {
            ordersGrid.setRows(result.rows);
            status.set(result.status);
            return loadLines();
        });
    }

    function loadLines() {
        var order = ordersGrid.currentRow();
        if (!order) { linesGrid.setRows([]); return Promise.resolve(); }
        return P.request('GET', url('Lines') + '?orderId=' + order.Id + '&includeCompleted=' + includeCompleted.checked)
            .then(function (result) { linesGrid.setRows(result.rows); status.set(result.status); })
            .catch(function (error) { status.set('Fout bij laden van regels: ' + error.message, 'error'); });
    }

    document.getElementById('bt_refresh').addEventListener('click', function () { status.run('Orders laden...', loadOrders); });
    includeDeleted.addEventListener('change', function () { status.run('Orders laden...', loadOrders); });
    includeCompleted.addEventListener('change', function () { status.run('Regels laden...', loadLines); });
    status.run('Orders laden...', loadOrders);
})(window.Procurement);
