/*
  Shared helpers for the Procurement web pages (plan of 18 nov 2026): calls to the server with the
  anti-forgery token, number/date formatting as in the desktop (GridFormatting.cs), and a small
  grid that behaves like the desktop DataGridView: fixed column widths that you can drag and that
  are remembered (like GridColumnSettingsHelper), sort on header click, row numbers, frozen columns,
  Ctrl/Shift multi-select, a checkbox column and an editable quantity cell.
  No external libraries, so it also works on a server without internet access.
*/
(function (window, document) {
    'use strict';

    var token = (document.querySelector('meta[name="request-verification-token"]') || {}).content || '';
    // Site root, also when IIS hosts the app in a sub folder (e.g. /Procurement/).
    var appRoot = (document.querySelector('meta[name="app-root"]') || {}).content || '/';

    function request(method, url, data) {
        var options = {
            method: method,
            credentials: 'same-origin',
            headers: { 'X-Requested-With': 'XMLHttpRequest', 'RequestVerificationToken': token }
        };
        if (data !== undefined) {
            options.headers['Content-Type'] = 'application/json';
            options.body = JSON.stringify(data);
        }
        return fetch(url, options).then(function (response) {
            if (response.status === 401) {
                window.location.reload(); // session expired: back to the company choice
                throw new Error('Sessie verlopen, log opnieuw in.');
            }
            return response.text().then(function (text) {
                var body = null;
                try { body = text ? JSON.parse(text) : null; } catch (e) { body = null; }
                if (!response.ok) throw new Error((body && body.error) || ('Fout ' + response.status + ' van de server.'));
                return body;
            });
        });
    }

    var nl = 'nl-NL';
    var quantityFormat = new Intl.NumberFormat(nl, { minimumFractionDigits: 4, maximumFractionDigits: 4 });
    var integerFormat = new Intl.NumberFormat(nl, { maximumFractionDigits: 0 });
    var moneyFormat = new Intl.NumberFormat(nl, { minimumFractionDigits: 4, maximumFractionDigits: 4 });

    function pad(n) { return n < 10 ? '0' + n : '' + n; }

    var format = {
        // d-MM-yyyy, as in the desktop.
        dateTime: function (value) {
            if (!value) return '';
            var d = new Date(value);
            if (isNaN(d.getTime())) return value;
            return format.date(value) + ' ' + pad(d.getHours()) + ':' + pad(d.getMinutes()) + ':' + pad(d.getSeconds());
        },
        date: function (value) {
            if (!value) return '';
            var d = new Date(value);
            if (isNaN(d.getTime())) return value;
            return d.getDate() + '-' + pad(d.getMonth() + 1) + '-' + d.getFullYear();
        },
        quantity: function (value) { return value === null || value === undefined ? '' : quantityFormat.format(value); },
        integer: function (value) { return value === null || value === undefined ? '' : integerFormat.format(value); },
        money: function (value) { return value === null || value === undefined ? '' : '€ ' + moneyFormat.format(value); },
        number: function (value, decimals) {
            if (value === null || value === undefined) return '';
            return new Intl.NumberFormat(nl, { minimumFractionDigits: decimals || 0, maximumFractionDigits: decimals || 0 }).format(value);
        },
        text: function (value) { return value === null || value === undefined ? '' : String(value); }
    };

    /* ---------- Grid ---------- */

    // columns: [{ key, header, width (px), type: text|quantity|integer|money|date|bool|check, hidden, frozen, editable }]
    function Grid(element, options) {
        this.el = element;
        this.id = options.id;
        this.columns = options.columns;
        this.keyField = options.keyField || 'Id';
        this.multiSelect = !!options.multiSelect;
        this.onCurrentChanged = options.onCurrentChanged || function () { };
        this.onCheck = options.onCheck || function () { };
        this.onEdit = options.onEdit || function () { };
        this.emptyText = options.emptyText || '';
        this.rowClass = options.rowClass || function () { return ''; };
        this.rows = [];
        this.selected = {};
        this.current = null;
        this.anchor = null;
        this.sort = this.load('sort') || null;
        var widths = this.load('widths') || {};
        this.columns.forEach(function (c) { if (widths[c.key]) c.width = widths[c.key]; });
        this.render();
    }

    Grid.prototype.storageKey = function (name) { return 'procurement.grid.' + this.id + '.' + name; };
    Grid.prototype.load = function (name) {
        try { return JSON.parse(window.localStorage.getItem(this.storageKey(name))); } catch (e) { return null; }
    };
    Grid.prototype.save = function (name, value) {
        try { window.localStorage.setItem(this.storageKey(name), JSON.stringify(value)); } catch (e) { /* private mode */ }
    };

    Grid.prototype.setRows = function (rows) {
        var key = this.keyField;
        var previousCurrent = this.current;
        this.rows = (rows || []).slice();
        var stillThere = {};
        this.rows.forEach(function (r) { stillThere[r[key]] = true; });
        var selected = {};
        Object.keys(this.selected).forEach(function (id) { if (stillThere[id]) selected[id] = true; });
        this.selected = selected;
        this.current = previousCurrent !== null && stillThere[previousCurrent] ? previousCurrent : (this.rows.length ? this.rows[0][key] : null);
        if (this.current !== null && Object.keys(this.selected).length === 0) this.selected[this.current] = true;
        this.applySort();
        this.render();
        if (this.current !== previousCurrent) this.onCurrentChanged(this.currentRow());
    };

    Grid.prototype.currentRow = function () {
        var key = this.keyField, current = this.current;
        for (var i = 0; i < this.rows.length; i++) if (this.rows[i][key] === current) return this.rows[i];
        return null;
    };
    Grid.prototype.selectedIds = function () {
        var key = this.keyField, selected = this.selected;
        return this.rows.filter(function (r) { return selected[r[key]]; }).map(function (r) { return r[key]; });
    };
    Grid.prototype.allIds = function () {
        var key = this.keyField;
        return this.rows.map(function (r) { return r[key]; });
    };

    Grid.prototype.applySort = function () {
        if (!this.sort) return;
        var key = this.sort.key, dir = this.sort.dir === 'desc' ? -1 : 1;
        this.rows.sort(function (a, b) {
            var x = a[key], y = b[key];
            if (x === y) return 0;
            if (x === null || x === undefined) return 1;
            if (y === null || y === undefined) return -1;
            if (typeof x === 'string' && typeof y === 'string') return x.localeCompare(y, nl, { numeric: true }) * dir;
            return (x < y ? -1 : 1) * dir;
        });
    };

    Grid.prototype.cellText = function (column, value) {
        switch (column.type) {
            case 'date': return format.date(value);
            case 'dateTime': return format.dateTime(value);
            case 'quantity': return format.quantity(value);
            case 'integer': return format.integer(value);
            case 'money': return format.money(value);
            default: return format.text(value);
        }
    };

    Grid.prototype.render = function () {
        var self = this, key = this.keyField;
        var visible = this.columns.filter(function (c) { return !c.hidden; });
        var table = document.createElement('table');
        var colgroup = document.createElement('colgroup');
        var rowNumberWidth = 40;
        var colNum = document.createElement('col');
        colNum.style.width = rowNumberWidth + 'px';
        colgroup.appendChild(colNum);
        visible.forEach(function (c) {
            var col = document.createElement('col');
            col.style.width = (c.width || 100) + 'px';
            colgroup.appendChild(col);
        });
        table.appendChild(colgroup);

        // Frozen columns (as the desktop's ApplyFrozenColumns): sticky, offset by the columns before them.
        var left = rowNumberWidth, frozenLeft = {};
        visible.forEach(function (c) {
            if (!c.frozen) return;
            frozenLeft[c.key] = left;
            left += c.width || 100;
        });

        var thead = document.createElement('thead');
        var headRow = document.createElement('tr');
        var corner = document.createElement('th');
        corner.className = 'rownum';
        headRow.appendChild(corner);
        visible.forEach(function (c) {
            var th = document.createElement('th');
            th.textContent = c.header || c.key;
            th.title = c.header || c.key;
            if (self.sort && self.sort.key === c.key) th.className = 'sorted-' + self.sort.dir;
            if (c.frozen) { th.classList.add('frozen'); th.style.left = frozenLeft[c.key] + 'px'; th.style.zIndex = 3; }
            th.addEventListener('click', function (e) {
                if (e.target.className === 'resize') return;
                var dir = self.sort && self.sort.key === c.key && self.sort.dir === 'asc' ? 'desc' : 'asc';
                self.sort = { key: c.key, dir: dir };
                self.save('sort', self.sort);
                self.applySort();
                self.render();
            });
            var grip = document.createElement('span');
            grip.className = 'resize';
            grip.addEventListener('mousedown', function (e) { self.startResize(e, c); });
            th.appendChild(grip);
            headRow.appendChild(th);
        });
        thead.appendChild(headRow);
        table.appendChild(thead);

        var tbody = document.createElement('tbody');
        this.rows.forEach(function (row, index) {
            var tr = document.createElement('tr');
            var id = row[key];
            var extra = self.rowClass(row);
            if (extra) tr.classList.add(extra);
            if (self.selected[id]) tr.classList.add('selected');
            var num = document.createElement('td');
            num.className = 'rownum';
            num.textContent = index + 1;
            tr.appendChild(num);
            visible.forEach(function (c) {
                var td = document.createElement('td');
                var value = row[c.key];
                if (c.type === 'check' || c.type === 'bool') {
                    td.className = 'center';
                    var box = document.createElement('input');
                    box.type = 'checkbox';
                    box.checked = !!value;
                    // Nullable yes/no (e.g. "Late?" before a confirmation): blank, not "no", as on the desktop.
                    if (value === null || value === undefined) box.indeterminate = c.type === 'bool' && c.nullable === true;
                    box.disabled = c.type === 'bool';
                    if (c.type === 'check') {
                        box.addEventListener('click', function (e) { e.stopPropagation(); });
                        box.addEventListener('change', function () { self.onCheck(row, box.checked); });
                    }
                    td.appendChild(box);
                } else {
                    td.textContent = self.cellText(c, value);
                    td.title = td.textContent;
                    if (['quantity', 'integer', 'money'].indexOf(c.type) >= 0) td.classList.add('num');
                    if (c.editable) {
                        td.classList.add('editable');
                        td.addEventListener('dblclick', function (e) { e.stopPropagation(); self.startEdit(td, row, c); });
                    }
                }
                if (c.frozen) { td.classList.add('frozen'); td.style.left = frozenLeft[c.key] + 'px'; }
                tr.appendChild(td);
            });
            tr.addEventListener('click', function (e) { self.rowClicked(id, e); });
            tbody.appendChild(tr);
        });
        table.appendChild(tbody);

        var scrollTop = this.el.scrollTop, scrollLeft = this.el.scrollLeft;
        this.el.innerHTML = '';
        this.el.appendChild(table);
        if (!this.rows.length && this.emptyText) {
            var empty = document.createElement('div');
            empty.className = 'empty';
            empty.textContent = this.emptyText;
            this.el.appendChild(empty);
        }
        this.el.scrollTop = scrollTop;
        this.el.scrollLeft = scrollLeft;
    };

    Grid.prototype.rowClicked = function (id, e) {
        var self = this, key = this.keyField;
        if (this.multiSelect && e.shiftKey && this.anchor !== null) {
            var ids = this.allIds(), a = ids.indexOf(this.anchor), b = ids.indexOf(id);
            this.selected = {};
            ids.slice(Math.min(a, b), Math.max(a, b) + 1).forEach(function (x) { self.selected[x] = true; });
        } else if (this.multiSelect && (e.ctrlKey || e.metaKey)) {
            if (this.selected[id]) delete this.selected[id]; else this.selected[id] = true;
            this.anchor = id;
        } else {
            this.selected = {};
            this.selected[id] = true;
            this.anchor = id;
        }
        var changed = this.current !== id;
        this.current = id;
        this.render();
        if (changed) this.onCurrentChanged(this.currentRow());
    };

    Grid.prototype.startResize = function (e, column) {
        e.preventDefault();
        e.stopPropagation();
        var self = this, startX = e.clientX, startWidth = column.width || 100;
        function move(ev) {
            column.width = Math.max(30, startWidth + ev.clientX - startX);
            self.render();
        }
        function up() {
            document.removeEventListener('mousemove', move);
            document.removeEventListener('mouseup', up);
            var widths = self.load('widths') || {};
            widths[column.key] = column.width;
            self.save('widths', widths);
        }
        document.addEventListener('mousemove', move);
        document.addEventListener('mouseup', up);
    };

    Grid.prototype.startEdit = function (td, row, column) {
        var self = this;
        var input = document.createElement('input');
        input.type = 'number';
        input.min = '1';
        input.step = '1';
        input.value = row[column.key];
        td.textContent = '';
        td.appendChild(input);
        input.focus();
        input.select();
        var done = false;
        function finish(commit) {
            if (done) return;
            done = true;
            var value = parseInt(input.value, 10);
            self.render();
            if (commit && value > 0 && value !== row[column.key]) self.onEdit(row, column.key, value);
        }
        input.addEventListener('keydown', function (ev) {
            if (ev.key === 'Enter') finish(true);
            if (ev.key === 'Escape') finish(false);
        });
        input.addEventListener('blur', function () { finish(false); });
    };

    /* ---------- Status line and busy state (as the desktop's SetStatus/SetBusy) ---------- */

    function Status(element, buttons) {
        this.el = element;
        this.buttons = buttons || [];
        this.busy = false;
    }
    Status.prototype.set = function (text, kind) {
        this.el.textContent = text;
        this.el.className = 'status-line' + (kind ? ' ' + kind : '');
    };
    Status.prototype.run = function (text, work) {
        var self = this;
        if (this.busy) return Promise.resolve();
        this.busy = true;
        this.set(text, 'busy');
        document.body.style.cursor = 'progress';
        this.buttons.forEach(function (b) { b.dataset.wasDisabled = b.disabled ? '1' : ''; b.disabled = true; });
        return Promise.resolve()
            .then(work)
            .catch(function (error) { self.set(error.message || String(error), 'error'); })
            .then(function () {
                self.busy = false;
                document.body.style.cursor = '';
                self.buttons.forEach(function (b) { b.disabled = b.dataset.wasDisabled === '1'; });
            });
    };

    window.Procurement = { appRoot: appRoot, request: request, format: format, Grid: Grid, Status: Status };
})(window, document);
