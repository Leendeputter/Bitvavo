/* Approvals (web), as the desktop's ApprovalForm. */
(function (P) {
    'use strict';
    function url(action) { return P.appRoot + 'Approvals/' + action; }
    var status = new P.Status(document.getElementById('status'),
        [document.getElementById('bt_approve'), document.getElementById('bt_reject')]);
    var comment = document.getElementById('txt_comment');

    var grid = new P.Grid(document.getElementById('grid_approvals'), {
        id: 'approvals',
        multiSelect: true, // Ctrl/Shift-click: decide several lines at once (19 nov 2026)
        emptyText: 'Er wacht niets op goedkeuring.',
        columns: [
            { key: 'Id', hidden: true },
            { key: 'ErpRequestNumber', header: 'ErpRequestNumber', width: 95 },
            { key: 'PartID', header: 'PartID', width: 110 },
            { key: 'Part', header: 'Part', width: 140 },
            { key: 'Supplier', header: 'Supplier', width: 95 },
            { key: 'OfferedQty', header: 'Offered Qty', width: 95, type: 'quantity' },
            { key: 'Price', header: 'Price', width: 95, type: 'money' },
            { key: 'Reason', header: 'Reason', width: 400 },
            { key: 'CreatedAt', header: 'CreatedAt', width: 145, type: 'dateTime' }
        ]
    });

    function load() {
        return P.request('GET', url('List')).then(function (result) {
            grid.setRows(result.rows);
            status.set(result.status);
        });
    }

    function decide(approve) {
        var ids = grid.selectedIds();
        if (!ids.length) { status.set('Selecteer eerst een of meer regels.', 'error'); return; }
        var ask = ids.length > 1
            ? P.confirm(approve ? 'Approve' : 'Reject', ids.length + ' regels ' + (approve ? 'goedkeuren' : 'afwijzen') + '?', approve ? 'Approve' : 'Reject')
            : Promise.resolve(true);
        ask.then(function (ok) { if (ok) decideConfirmed(ids, approve); });
    }

    function decideConfirmed(ids, approve) {
        status.run(approve ? 'Goedkeuren...' : 'Afwijzen...', function () {
            return P.request('POST', url('Decide'), { approvalIds: ids, approve: approve, comment: comment.value })
                .then(function (result) { comment.value = ''; return load().then(function () { status.set(result.status); }); });
        });
    }

    document.getElementById('bt_approve').addEventListener('click', function () { decide(true); });
    document.getElementById('bt_reject').addEventListener('click', function () { decide(false); });
    status.run('Laden...', load);
})(window.Procurement);
