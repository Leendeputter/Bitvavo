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

    function escapeHtml(text) {
        return String(text).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

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
        this.colEls = {};
        this.rowEls = {};
        var table = document.createElement('table');
        var colgroup = document.createElement('colgroup');
        var rowNumberWidth = 40;
        var colNum = document.createElement('col');
        colNum.style.width = rowNumberWidth + 'px';
        colgroup.appendChild(colNum);
        visible.forEach(function (c) {
            var col = document.createElement('col');
            col.style.width = (c.width || 100) + 'px';
            self.colEls[c.key] = col;
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

        // Only the rows in view are in the page (plus a margin); the rest is an empty spacer of the
        // same height, so the scroll bar is right. With thousands of rows a full table took seconds
        // to draw and made every click slow (19 nov 2026). One listener per event type on the body.
        var tbody = document.createElement('tbody');
        this.tbody = tbody;
        this.visible = visible;
        this.frozenLeft = frozenLeft;
        this.windowStart = -1;

        function rowOf(element) {
            var tr = element.closest('tr');
            return tr && tr.dataset.index !== undefined ? self.rows[+tr.dataset.index] : null;
        }
        // Shift/Ctrl-click selects rows, not the text in the cells (as a DataGridView): the browser
        // starts a text selection on mousedown, so that's where it's stopped.
        tbody.addEventListener('mousedown', function (e) {
            if ((e.shiftKey || e.ctrlKey || e.metaKey) && e.target.tagName !== 'INPUT') {
                e.preventDefault();
                var selection = window.getSelection && window.getSelection();
                if (selection) selection.removeAllRanges();
            }
        });
        tbody.addEventListener('click', function (e) {
            if (e.target.tagName === 'INPUT') return; // a checkbox or the quantity editor, not a row click
            var row = rowOf(e.target);
            if (row) self.rowClicked(row[key], e);
        });
        tbody.addEventListener('change', function (e) {
            if (e.target.type !== 'checkbox' || !e.target.dataset.key) return;
            var row = rowOf(e.target);
            if (row) self.onCheck(row, e.target.checked);
        });
        tbody.addEventListener('dblclick', function (e) {
            var td = e.target.closest('td[data-edit]');
            var row = td && rowOf(td);
            if (!row) return;
            var column = visible.filter(function (c) { return c.key === td.dataset.edit; })[0];
            self.startEdit(td, row, column);
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
        this.renderBody(true);

        if (!this.scrollHooked) {
            this.scrollHooked = true;
            var pending = false;
            this.el.addEventListener('scroll', function () {
                if (pending) return;
                pending = true;
                window.requestAnimationFrame(function () { pending = false; self.renderBody(false); });
            });
        }
    };

    var RowHeightGuess = 23; // first guess; the real height is measured after the first draw
    var RowMargin = 40;     // rows drawn above and below the visible part

    Grid.prototype.renderBody = function (force) {
        var self = this, key = this.keyField, visible = this.visible, frozenLeft = this.frozenLeft;
        var RowHeight = this.rowHeight || RowHeightGuess;
        var viewRows = Math.ceil((this.el.clientHeight || 600) / RowHeight);
        var top = Math.floor(this.el.scrollTop / RowHeight);
        var first = Math.max(0, top - RowMargin);
        // Redraw only when what's on screen comes within half a margin of the drawn block's edge.
        // Compared on the unclamped top row (19 nov 2026): comparing the clamped "first" skipped the
        // redraw when scrolling back up near the start, so the first rows stayed blank.
        if (!force && this.windowStart >= 0) {
            var nearTop = this.windowStart > 0 && top - this.windowStart < RowMargin / 2;
            var nearBottom = this.windowEnd < this.rows.length && this.windowEnd - (top + viewRows) < RowMargin / 2;
            if (!nearTop && !nearBottom) return;
        }
        var start = first;
        var end = Math.min(this.rows.length, first + viewRows + 2 * RowMargin);
        this.windowStart = start;
        this.windowEnd = end;
        var columnCount = visible.length + 1;

        var html = [];
        if (start > 0) html.push('<tr class="spacer" style="height:', start * RowHeight, 'px"><td colspan="', columnCount, '"></td></tr>');
        for (var index = start; index < end; index++) {
            var row = this.rows[index];
            var id = row[key];
            var classes = [];
            var extra = this.rowClass(row);
            if (extra) classes.push(extra);
            if (this.selected[id]) classes.push('selected');
            html.push('<tr data-index="', index, '" class="', classes.join(' '), '"><td class="rownum">', index + 1, '</td>');
            for (var ci = 0; ci < visible.length; ci++) {
                var c = visible[ci];
                var value = row[c.key];
                var style = c.frozen ? ' style="left:' + frozenLeft[c.key] + 'px"' : '';
                var frozen = c.frozen ? ' frozen' : '';
                if (c.type === 'check' || c.type === 'bool') {
                    // Nullable yes/no (e.g. "Late?" before a confirmation): blank, not "no", as on the desktop.
                    var blank = (value === null || value === undefined) && c.type === 'bool' && c.nullable === true;
                    html.push('<td class="center', frozen, '"', style, '><input type="checkbox" data-key="', c.key, '"',
                        value ? ' checked' : '', c.type === 'bool' ? ' disabled' : '', blank ? ' data-blank="1"' : '', ' /></td>');
                } else {
                    var text = escapeHtml(this.cellText(c, value));
                    var cls = (['quantity', 'integer', 'money'].indexOf(c.type) >= 0 ? 'num' : '') + (c.editable ? ' editable' : '') + frozen;
                    html.push('<td class="', cls, '"', style, ' title="', text, '"', c.editable ? ' data-edit="' + c.key + '"' : '', '>', text, '</td>');
                }
            }
            html.push('</tr>');
        }
        if (end < this.rows.length) html.push('<tr class="spacer" style="height:', (this.rows.length - end) * RowHeight, 'px"><td colspan="', columnCount, '"></td></tr>');

        this.tbody.innerHTML = html.join('');
        Array.prototype.forEach.call(this.tbody.querySelectorAll('input[data-blank]'), function (box) { box.indeterminate = true; });
        this.rowEls = {};
        Array.prototype.forEach.call(this.tbody.rows, function (tr) {
            if (tr.dataset.index !== undefined) self.rowEls[self.rows[+tr.dataset.index][key]] = tr;
        });

        // Spacer heights must match the real row height (font and zoom differ per pc).
        var sample = this.tbody.querySelector('tr[data-index]');
        var measured = sample ? sample.getBoundingClientRect().height : 0;
        if (measured > 0 && Math.abs(measured - RowHeight) > 0.1) {
            this.rowHeight = measured;
            this.renderBody(true);
        }
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
        // Only the row colours change; redrawing the whole table made clicks slow with many rows.
        var selected = this.selected;
        Array.prototype.forEach.call(this.el.querySelectorAll('tbody tr.selected'), function (tr) { tr.classList.remove('selected'); });
        Object.keys(selected).forEach(function (rowId) {
            if (self.rowEls[rowId]) self.rowEls[rowId].classList.add('selected');
        });
        if (changed) this.onCurrentChanged(this.currentRow());
    };

    Grid.prototype.startResize = function (e, column) {
        e.preventDefault();
        e.stopPropagation();
        var self = this, startX = e.clientX, startWidth = column.width || 100;
        function move(ev) {
            column.width = Math.max(30, startWidth + ev.clientX - startX);
            var col = self.colEls && self.colEls[column.key];
            if (col) col.style.width = column.width + 'px';
        }
        function up() {
            document.removeEventListener('mousemove', move);
            document.removeEventListener('mouseup', up);
            self.render(); // frozen column offsets follow the new width
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

    /* ---------- Modal dialog (as the desktop's ShowDialog) ---------- */

    // options: title, content (a DOM node), okText, cancelText (null hides Cancel), wide.
    // Resolves true for OK, false for Cancel/Escape. "ok" can be refused by options.canOk().
    function dialog(options) {
        return new Promise(function (resolve) {
            var dlg = document.createElement('dialog');
            dlg.className = 'dialog' + (options.wide ? ' wide' : '');
            var title = document.createElement('div');
            title.className = 'dialog-title';
            title.textContent = options.title || '';
            var body = document.createElement('div');
            body.className = 'dialog-body';
            if (options.content) body.appendChild(options.content);
            var buttons = document.createElement('div');
            buttons.className = 'dialog-buttons';
            var done = false;
            function close(result) {
                if (done) return;
                done = true;
                dlg.close();
                dlg.parentNode.removeChild(dlg);
                resolve(result);
            }
            var ok = document.createElement('button');
            ok.type = 'button';
            ok.textContent = options.okText || 'OK';
            ok.addEventListener('click', function () { if (!options.canOk || options.canOk()) close(true); });
            buttons.appendChild(ok);
            if (options.cancelText !== null) {
                var cancel = document.createElement('button');
                cancel.type = 'button';
                cancel.textContent = options.cancelText || 'Cancel';
                cancel.addEventListener('click', function () { close(false); });
                buttons.appendChild(cancel);
            }
            dlg.addEventListener('cancel', function (ev) { ev.preventDefault(); close(false); });
            dlg.appendChild(title);
            dlg.appendChild(body);
            dlg.appendChild(buttons);
            document.body.appendChild(dlg);
            dlg.showModal();
            if (options.onShown) options.onShown(dlg, function () { ok.click(); });
        });
    }

    window.Procurement = { appRoot: appRoot, request: request, format: format, Grid: Grid, Status: Status, dialog: dialog };
})(window, document);
