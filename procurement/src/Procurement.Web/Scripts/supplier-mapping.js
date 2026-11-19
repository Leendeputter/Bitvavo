/* Supplier Mapping (web), as the desktop's SupplierMappingForm: filter on match confidence, mark as Verified. */
(function (P) {
    'use strict';
    function url(action) { return P.appRoot + 'SupplierMapping/' + action; }
    var filter = document.getElementById('cb_filter');
    var status = new P.Status(document.getElementById('status'), [document.getElementById('bt_verify')]);

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
    filter.addEventListener('change', function () { status.run('Laden...', load); });
    status.run('Laden...', load);
})(window.Procurement);
