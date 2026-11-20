/*
  Beheer > App Secrets (web), as the desktop's AppSecretsForm. Secret values only go from this page
  to the server; the server only ever answers "ingesteld" or "leeg". The boxes are emptied right
  after saving, so a value doesn't stay on screen.
*/
(function (P) {
    'use strict';
    function url(action) { return P.appRoot + 'AppSecrets/' + action; }
    var status = new P.Status(document.getElementById('status'),
        Array.prototype.slice.call(document.querySelectorAll('button')).filter(function (b) { return !b.closest('.menu'); }));
    var form = document.getElementById('frm_secrets');
    var rowsHolder = document.getElementById('secret_rows');
    var accessHolder = document.getElementById('tbl_access');
    var access = [];
    var selectedAccess = -1;   // the current row (anchor for Shift-click)
    var selectedSet = {};      // all selected rows by index (Ctrl/Shift-click, as in the other grids)

    function renderSecrets(secrets) {
        rowsHolder.innerHTML = '';
        secrets.forEach(function (s) {
            var row = document.createElement('div');
            row.className = 'secret-row';
            var label = document.createElement('span');
            label.textContent = s.Label;
            var state = document.createElement('span');
            state.className = 'secret-state ' + (s.IsSet ? 'set' : 'empty');
            state.textContent = s.IsSet ? 'ingesteld' : 'leeg';
            row.appendChild(label);
            row.appendChild(state);
            if (s.Editable) {
                var input = document.createElement('input');
                input.type = 'password';
                input.name = s.Name;
                input.autocomplete = 'new-password';
                row.appendChild(input);
            } else {
                var note = document.createElement('span');
                note.className = 'muted';
                note.textContent = 'set via Settings > Authorize Ordering...';
                row.appendChild(note);
            }
            rowsHolder.appendChild(row);
        });
    }

    function renderAccess() {
        var table = document.createElement('table');
        table.className = 'edit-table';
        var head = table.createTHead().insertRow();
        ['', 'Secret', 'User (Windows login)', 'May use', 'Granted by', 'Date'].forEach(function (h, i) {
            var th = document.createElement('th');
            th.textContent = h;
            if (i === 0) th.className = 'rownum';
            th.style.width = [30, 160, 160, 70, 110, 120][i] + 'px';
            head.appendChild(th);
        });
        var body = table.createTBody();
        access.forEach(function (entry, index) {
            var tr = body.insertRow();
            if (selectedSet[index]) tr.className = 'selected';
            var num = tr.insertCell();
            num.className = 'rownum';
            num.textContent = index + 1;

            var secretCell = tr.insertCell();
            var select = document.createElement('select');
            window.secretNames.concat(window.secretNames.indexOf(entry.SecretName) < 0 && entry.SecretName ? [entry.SecretName] : []).forEach(function (n) {
                var option = document.createElement('option');
                option.value = option.textContent = n;
                select.appendChild(option);
            });
            select.value = entry.SecretName || window.secretNames[0];
            entry.SecretName = select.value;
            select.disabled = !entry.isNew; // an existing grant is identified by secret + user; change it by deleting and adding
            select.addEventListener('change', function () { entry.SecretName = select.value; });
            secretCell.appendChild(select);

            var userCell = tr.insertCell();
            var user = document.createElement('input');
            user.type = 'text';
            user.value = entry.UserName || '';
            user.disabled = !entry.isNew;
            user.addEventListener('input', function () { entry.UserName = user.value; });
            userCell.appendChild(user);

            var readCell = tr.insertCell();
            readCell.className = 'center';
            var canRead = document.createElement('input');
            canRead.type = 'checkbox';
            canRead.checked = !!entry.CanRead;
            canRead.addEventListener('change', function () { entry.CanRead = canRead.checked; });
            readCell.appendChild(canRead);

            tr.insertCell().textContent = entry.GrantedBy || '';
            tr.insertCell().textContent = entry.GrantedDate ? P.format.dateTime(entry.GrantedDate) : '';
            tr.addEventListener('mousedown', function (e) { select2(index, e); });
            tr.addEventListener('focusin', function () { if (!selectedSet[index]) select2(index); });
        });
        accessHolder.innerHTML = '';
        accessHolder.appendChild(table);
    }

    // Click selects one row, Ctrl-click adds or removes a row, Shift-click selects a range.
    function select2(index, e) {
        if (e && e.shiftKey && selectedAccess >= 0) {
            selectedSet = {};
            for (var i = Math.min(selectedAccess, index); i <= Math.max(selectedAccess, index); i++) selectedSet[i] = true;
            if (e.target.tagName !== 'INPUT' && e.target.tagName !== 'SELECT') e.preventDefault(); // no text selection
        } else if (e && (e.ctrlKey || e.metaKey)) {
            if (selectedSet[index]) delete selectedSet[index]; else selectedSet[index] = true;
            selectedAccess = index;
        } else {
            selectedSet = {};
            selectedSet[index] = true;
            selectedAccess = index;
        }
        Array.prototype.forEach.call(accessHolder.querySelectorAll('tbody tr'), function (tr, i) { tr.classList.toggle('selected', !!selectedSet[i]); });
    }

    function selectedIndexes() {
        return Object.keys(selectedSet).map(Number).filter(function (i) { return i < access.length; }).sort(function (a, b) { return a - b; });
    }

    function fillCopyFrom() {
        var list = document.getElementById('copy_from_users');
        list.innerHTML = '';
        var seen = {};
        access.forEach(function (e) {
            var key = (e.UserName || '').toLowerCase();
            if (!key || seen[key]) return;
            seen[key] = true;
            var option = document.createElement('option');
            option.value = e.UserName;
            list.appendChild(option);
        });
    }

    function load() {
        return P.request('GET', url('Data')).then(function (result) {
            renderSecrets(result.secrets);
            access = result.access;
            selectedAccess = access.length ? 0 : -1;
            selectedSet = access.length ? { 0: true } : {};
            renderAccess();
            fillCopyFrom();
        });
    }

    form.addEventListener('submit', function (e) {
        e.preventDefault();
        var values = Array.prototype.map.call(form.querySelectorAll('input[type=password]'), function (input) {
            return { Name: input.name, Value: input.value };
        }).filter(function (v) { return v.Value !== ''; });
        if (!values.length) { status.set('Er is niets ingevuld; er is niets opgeslagen.'); return; }
        status.run('Secrets opslaan...', function () {
            return P.request('POST', url('SaveSecrets'), { values: values }).then(function (result) {
                return load().then(function () { status.set(result.status); });
            });
        }).then(function () {
            // Emptied whether it worked or not: a typed secret shouldn't stay on screen.
            Array.prototype.forEach.call(form.querySelectorAll('input[type=password]'), function (input) { input.value = ''; });
        });
    });

    document.getElementById('bt_addAccess').addEventListener('click', function () {
        access.push({ SecretName: window.secretNames[0], UserName: '', CanRead: true, isNew: true });
        selectedAccess = access.length - 1;
        selectedSet = {};
        selectedSet[selectedAccess] = true;
        renderAccess();
        var inputs = accessHolder.querySelectorAll('tbody tr:last-child input[type=text]');
        if (inputs.length) inputs[0].focus();
    });

    document.getElementById('bt_deleteAccess').addEventListener('click', function () {
        var indexes = selectedIndexes();
        if (!indexes.length) { status.set('Selecteer eerst een of meer regels.', 'error'); return; }
        var chosen = indexes.map(function (i) { return access[i]; });
        var stored = chosen.filter(function (e) { return !e.isNew; });
        // New, unsaved rows are only taken off the screen.
        access = access.filter(function (e) { return chosen.indexOf(e) < 0 || !e.isNew; });
        if (!stored.length) {
            selectedAccess = Math.min(indexes[0], access.length - 1);
            selectedSet = {};
            if (selectedAccess >= 0) selectedSet[selectedAccess] = true;
            renderAccess();
            return;
        }
        var question = stored.length === 1
            ? 'Het recht van ' + stored[0].UserName + ' op ' + stored[0].SecretName + ' direct intrekken?'
            : stored.length + ' rechten direct intrekken?\n\n' + stored.slice(0, 15).map(function (e) { return e.UserName + ' — ' + e.SecretName; }).join('\n') +
              (stored.length > 15 ? '\n... en nog ' + (stored.length - 15) : '');
        P.confirm('Rechten intrekken', question, 'Intrekken').then(function (ok) {
            if (!ok) { renderAccess(); return; }
            status.run('Rechten intrekken...', function () {
                return P.request('POST', url('DeleteAccess'), {
                    entries: stored.map(function (e) { return { SecretName: e.SecretName, UserName: e.UserName }; })
                }).then(function (result) {
                    return load().then(function () { status.set(result.status); });
                });
            });
        });
    });

    document.getElementById('bt_saveAccess').addEventListener('click', function () {
        var entries = access.filter(function (e) { return e.SecretName && (e.UserName || '').trim(); }).map(function (e) {
            return { SecretName: e.SecretName, UserName: e.UserName.trim(), CanRead: !!e.CanRead };
        });
        status.run('Rechten opslaan...', function () {
            return P.request('POST', url('SaveAccess'), { entries: entries }).then(function (result) {
                return load().then(function () { status.set(result.status); });
            });
        });
    });

    document.getElementById('bt_copyAccess').addEventListener('click', function () {
        var from = document.getElementById('txt_copyFrom').value.trim();
        var to = document.getElementById('txt_copyTo').value.trim();
        if (!from || !to) { status.set("Vul zowel 'from' als 'to' in.", 'error'); return; }
        status.run('Rechten kopiëren...', function () {
            return P.request('POST', url('CopyAccess'), { fromUser: from, toUsers: to }).then(function (result) {
                document.getElementById('txt_copyTo').value = '';
                return load().then(function () { status.set(result.status); });
            });
        });
    });

    status.run('Laden...', function () { return load().then(function () { status.set(''); }); });
})(window.Procurement);
