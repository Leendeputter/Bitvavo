/* Supplier Mapping (web), as the desktop's SupplierMappingForm: filter on match confidence, mark as Verified. */
(function (P) {
    'use strict';
    function url(action) { return P.appRoot + 'SupplierMapping/' + action; }
    var filter = document.getElementById('cb_filter');
    var status = new P.Status(document.getElementById('status'), [document.getElementById('bt_verify'), document.getElementById('bt_edit')]);

    var grid = new P.Grid(document.getElementById('grid_mappings'), {
        id: 'mappings',
        emptyText: 'Geen mappings.',
        columns: [
            { key: 'Id', header: 'Id', width: 50, type: 'integer' },
            { key: 'ErpArticleId', header: 'ErpArticleId', width: 105 },
            { key: 'SupplierCode', header: 'SupplierCode', width: 90 },
            { key: 'SupplierPartNumber', header: 'SupplierPartNumber', width: 150 },
            { key: 'Manufacturer', header: 'Manufacturer', width: 120 },
            { key: 'ManufacturerPartNumber', header: 'ManufacturerPartNumber', width: 180 },
            { key: 'MatchConfidence', header: 'MatchConfidence', width: 105 },
            { key: 'CreatedAt', header: 'CreatedAt', width: 140, type: 'dateTime' }
        ]
    });

    function load() {
        return P.request('GET', url('List') + '?confidence=' + encodeURIComponent(filter.value)).then(function (result) {
            grid.setRows(result.rows);
            status.set(result.status);
        });
    }

    document.getElementById('bt_verify').addEventListener('click', function () {
        var row = grid.currentRow();
        if (!row) { status.set('Selecteer eerst een mapping.', 'error'); return; }
        status.run('Op Verified zetten...', function () {
            return P.request('POST', url('Verify'), { mappingId: row.Id }).then(load);
        });
    });
    // Another order code (24 nov 2026, Leen: two Farnell codes in Part_Vendor, keep one of them).
    function editPartNumber() {
        var row = grid.currentRow();
        if (!row) { status.set('Selecteer eerst een mapping.', 'error'); return; }
        var form = document.createElement('div');
        form.className = 'dialog-form';
        var info = document.createElement('p');
        info.textContent = row.ErpArticleId + ' bij ' + row.SupplierCode + (row.ManufacturerPartNumber ? ' (' + row.ManufacturerPartNumber + ')' : '') +
            '. Een volgende Query overschrijft deze code niet.';
        var label = document.createElement('label');
        var caption = document.createElement('span');
        caption.textContent = 'Supplier Part Number';
        var input = document.createElement('input');
        input.type = 'text';
        input.maxLength = 100;
        input.value = row.SupplierPartNumber || '';
        label.appendChild(caption);
        label.appendChild(input);
        form.appendChild(info);
        form.appendChild(label);
        P.dialog({
            title: 'Change Supplier Part Number',
            content: form,
            okText: 'Save',
            canOk: function () { return input.value.trim().length > 0; },
            onShown: function (dlg, ok) {
                input.focus();
                input.select();
                input.addEventListener('keydown', function (ev) { if (ev.key === 'Enter') { ev.preventDefault(); ok(); } });
            }
        }).then(function (confirmed) {
            if (!confirmed || input.value.trim() === (row.SupplierPartNumber || '')) return;
            status.run('Ordercode opslaan...', function () {
                return P.request('POST', url('ChangePartNumber'), { mappingId: row.Id, supplierPartNumber: input.value.trim() }).then(load);
            });
        });
    }

    document.getElementById('bt_edit').addEventListener('click', editPartNumber);
    grid.el.addEventListener('dblclick', function (ev) { if (ev.target.closest('tbody tr')) editPartNumber(); });
    filter.addEventListener('change', function () { status.run('Laden...', load); });
    status.run('Laden...', load);
})(window.Procurement);
