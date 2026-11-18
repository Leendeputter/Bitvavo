/* Approvals (web), as the desktop's ApprovalForm. */
(function (P) {
    'use strict';
    function url(action) { return P.appRoot + 'Approvals/' + action; }
    var status = new P.Status(document.getElementById('status'),
        [document.getElementById('bt_approve'), document.getElementById('bt_reject')]);
    var comment = document.getElementById('txt_comment');

    var grid = new P.Grid(document.getElementById('grid_approvals'), {
        id: 'approvals',
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
        var row = grid.currentRow();
        if (!row) { status.set('Selecteer eerst een regel.', 'error'); return; }
        status.run(approve ? 'Goedkeuren...' : 'Afwijzen...', function () {
            return P.request('POST', url('Decide'), { approvalId: row.Id, approve: approve, comment: comment.value })
                .then(function () { comment.value = ''; return load(); });
        });
    }

    document.getElementById('bt_approve').addEventListener('click', function () { decide(true); });
    document.getElementById('bt_reject').addEventListener('click', function () { decide(false); });
    status.run('Laden...', load);
})(window.Procurement);
