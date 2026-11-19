/*
  Purchase Requests screen (web), same behaviour as the desktop's MainForm: "Query" fetches from MAX,
  the Display checkboxes only filter what's already synced, the offers grid follows the selected
  request, "Select" chooses or un-chooses an offer, and a different Quantity re-sources the bundle.
  All rules are on the server (PurchaseRequestScreenService); this only shows them.
*/
(function (P) {
    'use strict';

    function url(action) { return P.appRoot + 'PurchaseRequests/' + action; }

    var form = document.getElementById('filters');
    var status = new P.Status(document.getElementById('status'), Array.prototype.slice.call(document.querySelectorAll('.toolbar button:not([disabled]), #bt_query')));
    var currentLineId = null;

    var requestColumns = [
        { key: 'Id', hidden: true },
        { key: 'Order', header: 'Order', width: 80, frozen: true },
        { key: 'Workflow', header: 'Workflow', width: 112, frozen: true },
        { key: 'Status', header: 'Status', width: 50 },
        { key: 'Firm', header: 'Firm', width: 40, type: 'bool' },
        { key: 'Type', header: 'Type', width: 45 },
        { key: 'PartID', header: 'PartID', width: 100, frozen: true },
        { key: 'Rev', header: 'Rev', width: 45 },
        { key: 'Desc1', header: 'Desc1', width: 200, frozen: true },
        { key: 'Desc2', header: 'Desc2', width: 160 },
        { key: 'Quantity', header: 'Quantity', width: 90, type: 'quantity', frozen: true },
        { key: 'Cost', header: 'Cost', width: 90, type: 'money', frozen: true },
        { key: 'Cnv', header: 'Cnv', width: 50, type: 'integer' },
        { key: 'ExtCost', header: 'Ext. Cost', width: 100, type: 'money' },
        { key: 'DueDate', header: 'DueDate', width: 85, type: 'date', frozen: true },
        { key: 'PoDueDate', header: 'PO DueDate', width: 85, type: 'date', frozen: true },
        { key: 'Reference', header: 'Reference', width: 200 },
        { key: 'ManufacturingPart', header: 'Manufacturing Part', width: 155 },
        { key: 'Customer', header: 'Customer', width: 100 },
        { key: 'StockID', header: 'StockID', width: 100 },
        { key: 'RecVendor', header: 'Rec. Vendor', width: 75 },
        { key: 'SelectedSupplier', header: 'Selected Supplier', width: 100 },
        { key: 'SelectedPrice', header: 'Selected Price', width: 90, type: 'money' },
        { key: 'SelectedQuantity', header: 'Selected Qty', width: 90, type: 'quantity' },
        { key: 'SelectedDueDate', header: 'Selected DueDate', width: 90, type: 'date' }
    ];

    var offerColumns = [
        { key: 'Id', hidden: true },
        { key: 'Select', header: 'Select', width: 50, type: 'check' },
        { key: 'SupplierCode', header: 'Supplier', width: 75 },
        { key: 'SupplierPartNumber', header: 'Supplier Part Number', width: 225 },
        { key: 'MatchConfidence', header: 'Match', width: 70 },
        { key: 'OfferedQuantity', header: 'Quantity', width: 85, type: 'integer', editable: true },
        { key: 'AttritionQuantity', header: 'Extra uitval', width: 85, type: 'integer' },
        { key: 'SurplusQuantity', header: 'Overschot', width: 75, type: 'integer' },
        { key: 'Bundle', header: 'Bundel', width: 155 },
        { key: 'UnitPrice', header: 'Unit Price', width: 90, type: 'money' },
        { key: 'Currency', header: 'Currency', width: 60 },
        { key: 'Packaging', header: 'Packaging', width: 210 },
        { key: 'Reel', header: 'Reel', width: 75 },
        { key: 'ReelingFee', header: 'Reeling Fee', width: 90, type: 'money' },
        { key: 'LandedCost', header: 'Landed Cost', width: 90, type: 'money' },
        { key: 'ComparisonCost', header: 'Vergelijkingskosten', width: 125, type: 'money' },
        { key: 'LeadTimeDays', header: 'Lead Time (Working Days)', width: 155, type: 'integer' },
        { key: 'AvailableQuantity', header: 'Available Qty', width: 90, type: 'integer' },
        { key: 'DueDate', header: 'Due Date', width: 85, type: 'date' }
    ];

    var offersGrid = new P.Grid(document.getElementById('grid_offers'), {
        id: 'offers',
        columns: offerColumns,
        emptyText: 'Geen offers voor deze aanvraag.',
        onCheck: function (row, checked) {
            if (currentLineId === null) return;
            status.run(checked ? 'Offer kiezen...' : 'Keuze ongedaan maken...', function () {
                return P.request('POST', url('Select'), { lineId: currentLineId, offerId: row.Id, selected: checked })
                    .then(function () { return loadLocal(); });
            }).then(function () { if (status.el.className.indexOf('error') >= 0) loadOffers(); });
        },
        onEdit: function (row, key, value) {
            if (currentLineId === null) return;
            var ok = window.confirm('Opnieuw sourcen voor een totaal van ' + P.format.integer(value) + ' stuks?\n\n' +
                'Alle offers van deze regel (en van de andere regels in de bundel) worden opnieuw opgehaald voor dat aantal; ' +
                'een bestaande keuze of goedkeuring vervalt.');
            if (!ok) return;
            status.run('Opnieuw sourcen voor ' + P.format.integer(value) + ' stuks...', function () {
                return P.request('POST', url('Quantity'), { lineId: currentLineId, quantity: value })
                    .then(function (result) { return loadLocal().then(function () { status.set(result.status); }); });
            });
        }
    });

    var requestsGrid = new P.Grid(document.getElementById('grid_requests'), {
        id: 'requests',
        columns: requestColumns,
        multiSelect: true,
        emptyText: 'Klik op "Query" om orders uit MAX op te halen.',
        onCurrentChanged: function () { loadOffers(); }
    });

    function filter() {
        var data = {
            Statuses: [],
            IncludePlanned: form.IncludePlanned.checked,
            IncludeApproved: form.IncludeApproved.checked,
            DueDateEnabled: form.DueDateEnabled.checked,
            DueDateStart: form.DueDateStart.value || null,
            DueDateEnd: form.DueDateEnd.value || null,
            RangeField: form.RangeField.value,
            RangeStart: form.RangeStart.value,
            RangeEnd: form.RangeEnd.value,
            ShowTestRequests: form.ShowTestRequests.checked
        };
        Array.prototype.forEach.call(form.querySelectorAll('input[name=Statuses]:checked'), function (box) { data.Statuses.push(box.value); });
        return data;
    }

    function showRequests(result) {
        requestsGrid.setRows(result.rows);
        status.set(result.status);
        return loadOffers();
    }

    function loadLocal() {
        return P.request('POST', url('Load'), { filter: filter() }).then(showRequests);
    }

    function loadOffers() {
        var row = requestsGrid.currentRow();
        if (!row) {
            currentLineId = null;
            offersGrid.setRows([]);
            return Promise.resolve();
        }
        return P.request('GET', url('Offers') + '?requestId=' + encodeURIComponent(row.Id)).then(function (result) {
            currentLineId = result.lineId;
            offersGrid.setRows(result.rows);
        }).catch(function (error) { status.set('Fout bij laden van offers: ' + error.message, 'error'); });
    }

    function source(ids) {
        if (!ids.length) { status.set('Selecteer eerst een of meer aanvragen.', 'error'); return; }
        if (ids.length > 1 && !window.confirm('Sourcing starten voor ' + ids.length + ' aanvragen?')) return;
        var done = null;
        status.run('Sourcing van ' + ids.length + ' aanvraag/aanvragen... (dit kan even duren; leveranciers worden live bevraagd)', function () {
            return P.request('POST', url('Source'), { requestIds: ids }).then(function (result) {
                done = result.status;
                return loadLocal().then(function () { status.set(result.status); });
            });
        }).then(function () {
            // A clear "finished" (19 nov 2026, Leen: "Misschien ook goed om een melding in te bouwen van
            // Sourcing completed"), shown after the busy state ends.
            if (!done) return;
            var text = document.createElement('p');
            text.textContent = done;
            return P.dialog({ title: 'Sourcing completed', content: text, cancelText: null });
        });
    }

    document.getElementById('bt_query').addEventListener('click', function () {
        status.run('Orders opvragen uit MAX...', function () {
            return P.request('POST', url('Query'), { filter: filter() }).then(showRequests);
        });
    });
    document.getElementById('bt_startSourcing').addEventListener('click', function () { source(requestsGrid.selectedIds()); });
    document.getElementById('bt_sourceAll').addEventListener('click', function () { source(requestsGrid.allIds()); });

    /* ---------- Place Order (as MainForm.PlaceOrdersAsync) ---------- */

    // Farnell's ship-to picker, as FarnellShipToAddressPickerForm. Resolves the chosen id, or null on Cancel.
    function pickShipToAddress(addresses) {
        var content = document.createElement('div');
        var text = document.createElement('p');
        text.textContent = 'Deze orders gaan (deels) naar Farnell. Kies het afleveradres:';
        var list = document.createElement('select');
        list.className = 'dialog-list';
        list.size = Math.min(Math.max(addresses.length, 4), 12);
        addresses.forEach(function (a, i) {
            var option = document.createElement('option');
            option.value = a.AddressId;
            option.textContent = a.Text;
            if (i === 0) option.selected = true;
            list.appendChild(option);
        });
        content.appendChild(text);
        content.appendChild(list);
        return P.dialog({
            title: 'Farnell ship-to address',
            content: content,
            canOk: function () { return list.selectedIndex >= 0; },
            onShown: function (dlg, ok) { list.focus(); list.addEventListener('dblclick', ok); }
        }).then(function (confirmed) { return confirmed ? list.value : null; });
    }

    function placeOrders() {
        var ids = requestsGrid.selectedIds();
        if (!ids.length) { status.set('Selecteer eerst een of meer aanvragen.', 'error'); return; }
        if (!window.confirm('Order(s) plaatsen voor ' + ids.length + ' aanvraag/aanvragen? Regels worden per leverancier gebundeld tot één PO per leverancier.')) return;

        status.run('Orders voorbereiden...', function () {
            return P.request('POST', url('ShipToChoices'), { requestIds: ids }).then(function (result) {
                if (!result.addresses.length) return '';
                return pickShipToAddress(result.addresses);
            }).then(function (shipToAddressId) {
                if (shipToAddressId === null) { status.set('Geannuleerd — geen orders geplaatst.'); return; }
                status.set('Orders plaatsen...', 'busy');
                // Refreshed whether it worked or not: a partial batch (some suppliers placed before
                // one failed) should still show what did go through, as on the desktop.
                return P.request('POST', url('PlaceOrders'), { requestIds: ids, shipToAddressId: shipToAddressId || null }).then(
                    function (placed) { return loadLocal().then(function () { status.set(placed.status); }); },
                    function (error) { return loadLocal().then(function () { throw error; }, function () { throw error; }); });
            });
        });
    }

    /* ---------- Process Order Confirmations (as MainForm.ProcessOrderConfirmationsAsync) ---------- */

    // Deviations window, as OrderConfirmationExceptionsForm.
    function showConfirmationExceptions(rows) {
        var content = document.createElement('div');
        var text = document.createElement('p');
        text.textContent = 'Deze bevestigde regels wijken af van de bestelling en vragen om een beoordeling:';
        var holder = document.createElement('div');
        holder.className = 'grid';
        content.appendChild(text);
        content.appendChild(holder);
        return P.dialog({
            title: 'Order Confirmation Exceptions',
            content: content,
            okText: 'Close',
            cancelText: null,
            wide: true,
            onShown: function () {
                var grid = new P.Grid(holder, {
                    id: 'confirmationExceptions',
                    keyField: 'Key',
                    columns: [
                        { key: 'Key', hidden: true },
                        { key: 'ErpPoNumber', header: 'PO', width: 90 },
                        { key: 'SupplierCode', header: 'Supplier', width: 85 },
                        { key: 'SupplierPartNumber', header: 'Supplier Part Number', width: 150 },
                        { key: 'OrderedQuantity', header: 'Ordered Qty', width: 85, type: 'quantity' },
                        { key: 'ConfirmedQuantity', header: 'Confirmed Qty', width: 95, type: 'quantity' },
                        { key: 'OrderedUnitPrice', header: 'Ordered Price', width: 95, type: 'money' },
                        { key: 'ConfirmedUnitPrice', header: 'Confirmed Price', width: 105, type: 'money' },
                        { key: 'RequiredDate', header: 'Required Date', width: 90, type: 'date' },
                        { key: 'ConfirmedShipDate', header: 'Confirmed Ship Date', width: 120, type: 'date' },
                        { key: 'Reason', header: 'Reason', width: 230 }
                    ]
                });
                grid.setRows(rows.map(function (r, i) { r.Key = i + 1; return r; }));
            }
        });
    }

    function processConfirmations() {
        var result = null;
        status.run('Orderbevestigingen verwerken...', function () {
            return P.request('POST', url('ProcessConfirmations'), {}).then(function (r) {
                result = r;
                return loadLocal().then(function () { status.set(r.status); });
            });
        }).then(function () {
            // Shown after the busy state ends, as on the desktop (a dialog over a wait cursor looked stuck).
            if (!result) return;
            if (result.exceptions.length) return showConfirmationExceptions(result.exceptions);
            if (result.message) window.alert(result.message);
        });
    }

    document.getElementById('bt_processConfirmations').addEventListener('click', processConfirmations);
    var placeButton = document.getElementById('bt_placeOrders');
    if (!placeButton.disabled) placeButton.addEventListener('click', placeOrders);

    document.getElementById('chk_dueDateEnable').addEventListener('change', function () {
        form.DueDateStart.disabled = form.DueDateEnd.disabled = !this.checked;
    });
    // Display checkboxes: re-show what's already synced, never touches MAX (as on the desktop).
    Array.prototype.forEach.call(form.querySelectorAll('input[name=Statuses], input[name=ShowTestRequests]'), function (box) {
        box.addEventListener('change', function () { status.run('Aanvragen laden...', loadLocal); });
    });
    form.addEventListener('submit', function (e) { e.preventDefault(); });

    // Date pickers start at today, like the desktop's; they only count once "Enable" is ticked.
    var today = new Date();
    var iso = today.getFullYear() + '-' + ('0' + (today.getMonth() + 1)).slice(-2) + '-' + ('0' + today.getDate()).slice(-2);
    form.DueDateStart.value = iso;
    form.DueDateEnd.value = iso;
})(window.Procurement);
