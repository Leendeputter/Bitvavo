/*
  Settings (web), as the desktop's SettingsForm: one tab per table, each saved with its own "Save".
  The tables are small (tens of rows), so they are plain HTML tables with an input per cell instead
  of the virtual grid. Numbers are read from <input type="number">, whose value always uses a dot,
  whatever the browser's language, so "0,5" on screen is sent as 0.5.
*/
(function (P) {
    'use strict';
    function url(action) { return P.appRoot + 'Settings/' + action; }
    var status = new P.Status(document.getElementById('status'),
        Array.prototype.slice.call(document.querySelectorAll('.tab-page button')));
    var data = null;

    /* ---------- Tabs ---------- */
    var tabButtons = document.querySelectorAll('#tabs .tab');
    Array.prototype.forEach.call(tabButtons, function (button) {
        button.addEventListener('click', function () {
            Array.prototype.forEach.call(tabButtons, function (b) { b.classList.toggle('active', b === button); });
            Array.prototype.forEach.call(document.querySelectorAll('.tab-page'), function (page) {
                page.hidden = page.getAttribute('data-page') !== button.getAttribute('data-tab');
            });
        });
    });

    /* ---------- Editable table ---------- */
    // columns: key, header, width, type: 'text' | 'int' | 'decimal' | 'bool' | 'readonly' | 'dateTime', nullable (decimal).
    function EditTable(holder, columns, onSelect) {
        this.holder = holder;
        this.columns = columns;
        this.onSelect = onSelect || function () { };
        this.rows = [];
        this.selectedIndex = -1;
    }
    EditTable.prototype.setRows = function (rows) {
        this.rows = rows.map(function (r) { return Object.assign({}, r); });
        this.selectedIndex = this.rows.length ? 0 : -1;
        this.render();
        this.onSelect(this.selected());
    };
    EditTable.prototype.selected = function () { return this.selectedIndex >= 0 ? this.rows[this.selectedIndex] : null; };
    EditTable.prototype.add = function (row) {
        this.rows.push(row);
        this.selectedIndex = this.rows.length - 1;
        this.render();
        this.onSelect(this.selected());
        var inputs = this.holder.querySelectorAll('tbody tr:last-child input:not([type=checkbox])');
        if (inputs.length) inputs[0].focus();
    };
    EditTable.prototype.removeSelected = function () {
        if (this.selectedIndex < 0) return;
        this.rows.splice(this.selectedIndex, 1);
        this.selectedIndex = Math.min(this.selectedIndex, this.rows.length - 1);
        this.render();
        this.onSelect(this.selected());
    };
    EditTable.prototype.render = function () {
        var self = this;
        var table = document.createElement('table');
        table.className = 'edit-table';
        var head = table.createTHead().insertRow();
        var corner = document.createElement('th');
        corner.className = 'rownum';
        head.appendChild(corner);
        this.columns.forEach(function (c) {
            var th = document.createElement('th');
            th.textContent = c.header;
            th.style.width = c.width + 'px';
            head.appendChild(th);
        });
        var body = table.createTBody();
        this.rows.forEach(function (row, index) {
            var tr = body.insertRow();
            if (index === self.selectedIndex) tr.className = 'selected';
            var num = tr.insertCell();
            num.className = 'rownum';
            num.textContent = index + 1;
            self.columns.forEach(function (c) { tr.appendChild(self.cell(row, c)); });
            tr.addEventListener('focusin', function () { self.select(index); });
            tr.addEventListener('mousedown', function () { self.select(index); });
        });
        this.holder.innerHTML = '';
        this.holder.appendChild(table);
    };
    EditTable.prototype.select = function (index) {
        if (index === this.selectedIndex) return;
        this.selectedIndex = index;
        Array.prototype.forEach.call(this.holder.querySelectorAll('tbody tr'), function (tr, i) {
            tr.classList.toggle('selected', i === index);
        });
        this.onSelect(this.selected());
    };
    EditTable.prototype.cell = function (row, c) {
        var td = document.createElement('td');
        var value = row[c.key];
        if (c.type === 'readonly' || c.type === 'dateTime') {
            td.textContent = c.type === 'dateTime' ? P.format.dateTime(value) : P.format.text(value);
            td.className = 'readonly';
            return td;
        }
        var input = document.createElement('input');
        if (c.type === 'bool') {
            input.type = 'checkbox';
            input.checked = !!value;
            input.addEventListener('change', function () { row[c.key] = input.checked; });
            td.className = 'center';
        } else if (c.type === 'int' || c.type === 'decimal') {
            input.type = 'number';
            input.step = c.type === 'int' ? '1' : 'any';
            input.value = value === null || value === undefined ? '' : value;
            input.addEventListener('change', function () {
                if (input.value === '') { row[c.key] = c.nullable ? null : 0; if (!c.nullable) input.value = 0; return; }
                row[c.key] = c.type === 'int' ? parseInt(input.value, 10) : parseFloat(input.value);
            });
            td.className = 'num';
        } else {
            input.type = 'text';
            input.value = value === null || value === undefined ? '' : value;
            input.addEventListener('input', function () { row[c.key] = input.value; });
        }
        // Invalid number in the box: the browser reports an empty value; mark it so it isn't saved as 0 unnoticed.
        input.addEventListener('blur', function () { input.classList.toggle('invalid', input.validity && !input.validity.valid); });
        td.appendChild(input);
        return td;
    };
    EditTable.prototype.hasInvalid = function () { return this.holder.querySelector('input:invalid') !== null; };

    function save(action, table, label) {
        if (table.hasInvalid()) { status.set('Er staat een ongeldig getal in de tabel (rood omrand). Er is niets opgeslagen.', 'error'); return; }
        status.run(label + ' opslaan...', function () {
            return P.request('POST', url(action), { rows: table.rows }).then(function (result) {
                return load().then(function () { status.set(result.status); });
            });
        });
    }

    /* ---------- Suppliers ---------- */
    var suppliers = new EditTable(document.getElementById('tbl_suppliers'), [
        { key: 'Id', header: 'Id', width: 45, type: 'readonly' },
        { key: 'SupplierCode', header: 'SupplierCode', width: 110, type: 'readonly' },
        { key: 'Name', header: 'Name', width: 220, type: 'readonly' },
        { key: 'VendorId', header: 'VendorId', width: 90, type: 'text' },
        { key: 'IsSandbox', header: 'IsSandbox', width: 75, type: 'bool' },
        { key: 'UseMockData', header: 'UseMockData', width: 90, type: 'bool' }
    ]);
    document.getElementById('bt_saveSuppliers').addEventListener('click', function () { save('SaveSuppliers', suppliers, 'Suppliers'); });

    // "Edit Shipping Details...", as ShippingDetailsForm: pre-filled (nothing here is secret), always overwrites.
    var shippingFields = [
        ['AccountId', 'Account ID'], ['ContactName', 'Contact name'], ['ContactEmail', 'Contact e-mail'],
        ['ContactTelephone', 'Contact telephone'], ['AddressLine1', 'Address line 1'], ['AddressLine2', 'Address line 2'],
        ['City', 'City'], ['Province', 'Province'], ['PostalCode', 'Postal code'], ['CountryCode', 'Country code']
    ];
    document.getElementById('bt_editShipping').addEventListener('click', function () {
        var row = suppliers.selected();
        if (!row) { status.set('Selecteer eerst een supplier.', 'error'); return; }
        P.request('GET', url('Shipping') + '?supplierCode=' + encodeURIComponent(row.SupplierCode)).then(function (details) {
            if (!details) return;
            var form = document.createElement('div');
            form.className = 'dialog-form';
            var inputs = {};
            shippingFields.forEach(function (f) {
                var label = document.createElement('label');
                var span = document.createElement('span');
                span.textContent = f[1] + ':';
                var input = document.createElement('input');
                input.type = 'text';
                input.value = details[f[0]] || '';
                inputs[f[0]] = input;
                label.appendChild(span);
                label.appendChild(input);
                form.appendChild(label);
            });
            return P.dialog({ title: 'Shipping Details — ' + row.SupplierCode, content: form, okText: 'Save' }).then(function (ok) {
                if (!ok) return;
                shippingFields.forEach(function (f) { details[f[0]] = inputs[f[0]].value; });
                return status.run('Verzendgegevens opslaan...', function () {
                    return P.request('POST', url('SaveShipping'), { details: details }).then(function (r) { status.set(r.status); });
                });
            });
        }).catch(function (error) { status.set(error.message, 'error'); });
    });

    // "Authorize Ordering...": DigiKey's one-time consent. DigiKey sends the browser to
    // https://localhost/?code=..., which doesn't open; the address bar then holds the code to paste here.
    document.getElementById('bt_authorizeOrdering').addEventListener('click', function () {
        var row = suppliers.selected();
        if (!row) { status.set('Selecteer eerst een supplier.', 'error'); return; }
        if (String(row.SupplierCode).toUpperCase() !== 'DIGIKEY') {
            status.set('"Authorize Ordering" is alleen van toepassing op DigiKey.', 'error');
            return;
        }
        // Opened right away (still inside the click), otherwise the browser blocks the new tab.
        var consentTab = window.open('about:blank', '_blank');
        P.request('POST', url('DigiKeyStart'), {}).then(function (result) {
            if (consentTab) consentTab.location.href = result.url; else window.open(result.url, '_blank');
            var content = document.createElement('div');
            var text = document.createElement('p');
            text.textContent = 'Log in bij DigiKey in het nieuwe tabblad en geef toestemming. Het tabblad gaat daarna naar ' +
                '"https://localhost/?code=..." en laat een foutpagina zien; dat is goed. Kopieer het volledige adres uit de adresbalk en plak het hieronder.';
            var input = document.createElement('input');
            input.type = 'text';
            input.className = 'dialog-wide-input';
            input.placeholder = 'https://localhost/?code=...&state=...';
            content.appendChild(text);
            content.appendChild(input);
            return P.dialog({
                title: 'Authorize DigiKey Ordering',
                content: content,
                canOk: function () { return input.value.trim() !== ''; },
                onShown: function () { input.focus(); }
            }).then(function (ok) {
                if (!ok) { status.set('Autorisatie geannuleerd.'); return; }
                return status.run('DigiKey autoriseren...', function () {
                    return P.request('POST', url('DigiKeyFinish'), { pastedUrl: input.value.trim() }).then(function (r) { status.set(r.status); });
                });
            });
        }).catch(function (error) {
            if (consentTab) consentTab.close();
            status.set(error.message, 'error');
        });
    });

    /* ---------- Supplier preferences ---------- */
    var preferences = new EditTable(document.getElementById('tbl_preferences'), [
        { key: 'Id', header: 'Id', width: 45, type: 'readonly' },
        { key: 'SupplierCode', header: 'SupplierCode', width: 120, type: 'text' },
        { key: 'Priority', header: 'Priority', width: 80, type: 'int' },
        { key: 'Active', header: 'Active', width: 60, type: 'bool' },
        { key: 'AllowedForAutoOrder', header: 'AllowedForAutoOrder', width: 130, type: 'bool' }
    ]);
    document.getElementById('bt_savePreferences').addEventListener('click', function () { save('SavePreferences', preferences, 'Supplier preferences'); });
    document.getElementById('bt_addPreference').addEventListener('click', function () {
        preferences.add({ Id: 0, SupplierCode: '', Priority: preferences.rows.length + 1, Active: true, AllowedForAutoOrder: false });
    });

    /* ---------- Packaging policy ---------- */
    var packagingBox = document.getElementById('gb_packaging');
    var packagingLabel = document.getElementById('lbl_packaging');
    var allowedList = document.getElementById('lst_allowedPackaging');
    var exceptionList = document.getElementById('lst_exceptionPackaging');

    function fillChecks(list, row, key) {
        list.innerHTML = '';
        (data ? data.PackagingTypes : []).forEach(function (type) {
            var label = document.createElement('label');
            label.className = 'check';
            var box = document.createElement('input');
            box.type = 'checkbox';
            box.checked = !!row && row[key].indexOf(type) >= 0;
            box.addEventListener('change', function () {
                if (!row) return;
                var types = row[key].filter(function (t) { return t !== type; });
                if (box.checked) types.push(type);
                row[key] = types;
            });
            label.appendChild(box);
            label.appendChild(document.createTextNode(' ' + type));
            list.appendChild(label);
        });
    }

    var packaging = new EditTable(document.getElementById('tbl_packaging'), [
        { key: 'Id', header: 'Id', width: 45, type: 'readonly' },
        { key: 'ComponentCategory', header: 'Category / soort', width: 210, type: 'text' },
        { key: 'ExceptionPriceThreshold', header: 'Prijsdrempel (€)', width: 110, type: 'decimal', nullable: true },
        { key: 'SurplusPenaltyFactor', header: 'Factor overschot', width: 110, type: 'decimal' },
        { key: 'OriginalReelRequired', header: 'Originele reel verplicht', width: 150, type: 'bool' }
    ], function (row) {
        packagingBox.disabled = !row;
        packagingLabel.textContent = row ? 'Verpakkingen: ' + (row.ComponentCategory || '(nieuw)') : 'Verpakkingen van geselecteerde regel';
        fillChecks(allowedList, row, 'AllowedPackaging');
        fillChecks(exceptionList, row, 'ExceptionPackaging');
    });
    document.getElementById('bt_savePackaging').addEventListener('click', function () { save('SavePackaging', packaging, 'Packaging policies'); });
    document.getElementById('bt_addPackaging').addEventListener('click', function () {
        packaging.add({ Id: 0, ComponentCategory: '', ExceptionPriceThreshold: null, SurplusPenaltyFactor: 0.5, OriginalReelRequired: false, AllowedPackaging: [], ExceptionPackaging: [] });
    });

    /* ---------- Extra voor uitval ---------- */
    var attrition = new EditTable(document.getElementById('tbl_attrition'), [
        { key: 'AppliesTo', header: 'Geldt voor (Category of SMT/THT)', width: 230, type: 'text' },
        { key: 'MinUnitPrice', header: 'Prijs vanaf (€)', width: 105, type: 'decimal', nullable: true },
        { key: 'MaxUnitPrice', header: 'Prijs tot (€)', width: 105, type: 'decimal', nullable: true },
        { key: 'Percentage', header: 'Percentage', width: 90, type: 'decimal' },
        { key: 'MinimumExtra', header: 'Minimum extra (st)', width: 120, type: 'int' }
    ]);
    document.getElementById('bt_saveAttrition').addEventListener('click', function () { save('SaveAttrition', attrition, 'Extra voor uitval'); });
    document.getElementById('bt_addAttrition').addEventListener('click', function () {
        attrition.add({ Id: 0, AppliesTo: '', MinUnitPrice: null, MaxUnitPrice: null, Percentage: 0, MinimumExtra: 0 });
    });
    document.getElementById('bt_deleteAttrition').addEventListener('click', function () {
        var row = attrition.selected();
        if (!row) return;
        // Only removed from the table here; "Save" deletes it for real, as on the desktop.
        attrition.removeSelected();
        status.set('Regel weggehaald. Klik op Save om de wijziging op te slaan.');
    });

    /* ---------- Approval policy ---------- */
    var approvalForm = document.getElementById('frm_approval');
    function fillApproval(policy) {
        approvalForm.MaxOrderValueForAutoApproval.value = policy.MaxOrderValueForAutoApproval;
        approvalForm.MaxPriceVariancePercentage.value = policy.MaxPriceVariancePercentage;
        approvalForm.AllowExternalSupplier.checked = policy.AllowExternalSupplier;
        approvalForm.AllowNonOriginalPackaging.checked = policy.AllowNonOriginalPackaging;
        approvalForm.AllowAlternativePart.checked = policy.AllowAlternativePart;
    }
    approvalForm.addEventListener('submit', function (e) {
        e.preventDefault();
        if (!approvalForm.checkValidity() || approvalForm.MaxOrderValueForAutoApproval.value === '' || approvalForm.MaxPriceVariancePercentage.value === '') {
            status.set('Vul beide bedragen in met een geldig getal. Er is niets opgeslagen.', 'error');
            return;
        }
        var policy = {
            MaxOrderValueForAutoApproval: parseFloat(approvalForm.MaxOrderValueForAutoApproval.value),
            MaxPriceVariancePercentage: parseFloat(approvalForm.MaxPriceVariancePercentage.value),
            AllowExternalSupplier: approvalForm.AllowExternalSupplier.checked,
            AllowNonOriginalPackaging: approvalForm.AllowNonOriginalPackaging.checked,
            AllowAlternativePart: approvalForm.AllowAlternativePart.checked
        };
        status.run('Approval policy opslaan...', function () {
            return P.request('POST', url('SaveApproval'), { policy: policy }).then(function (r) { status.set(r.status); });
        });
    });

    /* ---------- Capabilities (read-only) ---------- */
    var capabilities = new EditTable(document.getElementById('tbl_capabilities'), [
        { key: 'Supplier', header: 'Supplier', width: 110, type: 'readonly' },
        { key: 'Capability', header: 'Capability', width: 140, type: 'readonly' },
        { key: 'Status', header: 'Status', width: 140, type: 'readonly' }
    ]);

    function load() {
        return P.request('GET', url('Data')).then(function (result) {
            data = result;
            suppliers.setRows(result.Suppliers);
            preferences.setRows(result.SupplierPreferences);
            packaging.setRows(result.PackagingPolicies);
            attrition.setRows(result.AttritionRules);
            fillApproval(result.ApprovalPolicy);
            capabilities.setRows(result.Capabilities);
        });
    }

    status.run('Laden...', function () { return load().then(function () { status.set(''); }); });
})(window.Procurement);
