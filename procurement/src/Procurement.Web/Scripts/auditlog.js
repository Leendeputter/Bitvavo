/* Audit Log (web), as the desktop's AuditLogForm: filter, newest first, double-click for the full text. */
(function (P) {
    'use strict';
    function url(action) { return P.appRoot + 'AuditLog/' + action; }
    var form = document.getElementById('filters');
    var status = new P.Status(document.getElementById('status'), [document.getElementById('bt_search')]);
    var detailFields = ['Timestamp', 'EntityType', 'EntityId', 'EventType', 'SupplierCode', 'Status',
        'UserOrSystem', 'RequestPayload', 'ResponsePayload', 'Error'];

    var grid = new P.Grid(document.getElementById('grid_auditlog'), {
        id: 'auditlog',
        emptyText: 'Geen gebeurtenissen gevonden.',
        columns: [
            { key: 'Id', hidden: true },
            { key: 'Timestamp', header: 'Timestamp', width: 140, type: 'dateTime' },
            { key: 'EntityType', header: 'EntityType', width: 115 },
            { key: 'EntityId', header: 'EntityId', width: 100 },
            { key: 'EventType', header: 'EventType', width: 190 },
            { key: 'SupplierCode', header: 'SupplierCode', width: 90 },
            { key: 'Status', header: 'Status', width: 75 },
            { key: 'UserOrSystem', header: 'UserOrSystem', width: 90 },
            { key: 'RequestPayload', header: 'RequestPayload', width: 220 },
            { key: 'ResponsePayload', header: 'ResponsePayload', width: 220 },
            { key: 'Error', header: 'Error', width: 290 }
        ]
    });

    // Detail window, as AuditLogDetailForm: the full, untruncated text, with "Copy to Clipboard".
    function showDetail(row) {
        var text = detailFields.map(function (f) {
            return f + ':\n' + (f === 'Timestamp' ? P.format.dateTime(row[f]) : P.format.text(row[f]));
        }).join('\n\n');
        var box = document.createElement('textarea');
        box.className = 'dialog-text';
        box.readOnly = true;
        box.value = text;
        P.dialog({
            title: 'Audit Log Detail',
            content: box,
            okText: 'Close',
            cancelText: 'Copy to Clipboard',
            wide: true,
            onShown: function (dlg) {
                box.scrollTop = 0;
                // "Copy" must not close the window, so it replaces the default Cancel handler.
                var copy = dlg.querySelectorAll('.dialog-buttons button')[1];
                var clone = copy.cloneNode(true);
                copy.parentNode.replaceChild(clone, copy);
                clone.addEventListener('click', function () {
                    box.select();
                    if (navigator.clipboard) navigator.clipboard.writeText(text); else document.execCommand('copy');
                });
            }
        });
    }
    grid.el.addEventListener('dblclick', function (ev) {
        if (!ev.target.closest('tbody tr')) return;
        var row = grid.currentRow();
        if (row) showDetail(row);
    });

    function query() {
        var params = ['entityType', 'eventType', 'supplier'].map(function (n) {
            return n + '=' + encodeURIComponent(form[n].value);
        });
        if (document.getElementById('chk_from').checked && form.from.value) params.push('from=' + form.from.value);
        // "To" is a whole day, as on the desktop (its picker defaults to tomorrow, meaning "up to and including today").
        if (document.getElementById('chk_to').checked && form.to.value) params.push('to=' + form.to.value + 'T23:59:59');
        return P.request('GET', url('List') + '?' + params.join('&')).then(function (result) {
            grid.setRows(result.rows);
            status.set(result.status);
        });
    }

    function iso(d) { return d.getFullYear() + '-' + ('0' + (d.getMonth() + 1)).slice(-2) + '-' + ('0' + d.getDate()).slice(-2); }
    var weekAgo = new Date(); weekAgo.setDate(weekAgo.getDate() - 7);
    form.from.value = iso(weekAgo);
    form.to.value = iso(new Date());
    document.getElementById('chk_from').addEventListener('change', function () { form.from.disabled = !this.checked; });
    document.getElementById('chk_to').addEventListener('change', function () { form.to.disabled = !this.checked; });
    form.addEventListener('submit', function (e) { e.preventDefault(); status.run('Zoeken...', query); });
    status.run('Laden...', query);
})(window.Procurement);
