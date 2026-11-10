window.academicExcel = {
    async importFile(input, { title, map, save, refresh, key, existingKeys }) {
        const file = input.files[0];
        input.value = '';
        if (!file) return;
        try {
            if (!/\.xlsx?$/i.test(file.name) || file.size > 5 * 1024 * 1024)
                throw new Error('Chọn file Excel .xlsx hoặc .xls tối đa 5 MB.');
            const book = XLSX.read(await file.arrayBuffer(), { sheetRows: 1002 });
            const sheet = book.Sheets[book.SheetNames[0]];
            if (!sheet?.['!ref']) throw new Error('Trang tính đầu tiên đang trống.');
            const range = XLSX.utils.decode_range(sheet['!fullref'] || sheet['!ref']);
            if (range.e.r - range.s.r > 1000) throw new Error('Tối đa 1.000 dòng dữ liệu mỗi file.');
            const rows = XLSX.utils.sheet_to_json(sheet, { defval: '', raw: true });
            if (!rows.length) throw new Error('Không có dòng dữ liệu để nhập.');
            const entries = rows.map(row => {
                try {
                    const value = map(row);
                    return { row: row.__rowNum__ + 1, value, error: value ? '' : 'Thiếu mã hoặc tên bắt buộc.' };
                } catch (err) { return { row: row.__rowNum__ + 1, error: err.message }; }
            });
            if (key) {
                const normalize = value => String(value).trim().toUpperCase();
                const valid = entries.filter(e => !e.error);
                const keys = [...new Set(valid.map(e => normalize(e.value[key])))];
                const existing = new Set(existingKeys && keys.length ? (await existingKeys(keys)).map(normalize) : []);
                const seen = new Set();
                for (const entry of valid) {
                    const code = normalize(entry.value[key]);
                    if (seen.has(code) || existing.has(code)) {
                        entry.duplicate = true;
                        entry.error = seen.has(code) ? 'Trùng trong tệp' : 'Đã có trong hệ thống';
                    }
                    seen.add(code);
                }
            }
            const accepted = entries.filter(e => !e.error).map(e => e.value);
            const saved = await this.preview(title, entries, accepted.length, async () => {
                const result = await save(accepted);
                showToast(result?.message || 'Đã xác nhận danh sách.');
            });
            if (saved && refresh) await refresh();
        } catch (err) { showToast(err.message, 'error'); }
    },

    preview(title, entries, count, save) {
        return new Promise(resolve => {
            const dialog = document.createElement('dialog');
            dialog.className = 'academic-excel-dialog';
            const heading = document.createElement('h2');
            heading.textContent = title;
            const table = document.createElement('table');
            const fields = [...new Set(entries.flatMap(e => Object.keys(e.value || {})))];
            const addRow = (parent, values, tag) => {
                const row = parent.insertRow();
                values.forEach(value => {
                    const cell = document.createElement(tag);
                    cell.textContent = value ?? '';
                    row.append(cell);
                });
            };
            addRow(table.createTHead(), ['Dòng', ...fields, 'Kiểm tra'], 'th');
            const body = table.createTBody();
            entries.forEach(e => addRow(body, [e.row, ...fields.map(k => e.value?.[k]), e.error || 'Sẵn sàng gửi'], 'td'));
            const grid = document.createElement('div');
            grid.className = 'academic-excel-grid';
            grid.append(table);
            const summary = document.createElement('p');
            const duplicates = entries.filter(e => e.duplicate).length;
            summary.textContent = `Đã đọc ${entries.length} dòng · Dự kiến nhập ${count} · Trùng ${duplicates} · Lỗi ${entries.length - count - duplicates}`;
            const error = document.createElement('p');
            error.setAttribute('role', 'alert');
            const cancel = document.createElement('button');
            cancel.textContent = 'Hủy';
            const confirm = document.createElement('button');
            confirm.textContent = 'Xác nhận nhập';
            confirm.disabled = count === 0;
            let saved = false;
            cancel.onclick = () => dialog.close();
            dialog.addEventListener('cancel', event => { if (cancel.disabled) event.preventDefault(); });
            dialog.addEventListener('close', () => { dialog.remove(); resolve(saved); }, { once: true });
            confirm.onclick = async () => {
                confirm.disabled = cancel.disabled = true;
                error.textContent = '';
                try { await save(); saved = true; dialog.close(); }
                catch (err) { error.textContent = err.message; confirm.disabled = cancel.disabled = false; }
            };
            const actions = document.createElement('footer');
            actions.append(cancel, confirm);
            dialog.append(heading, grid, summary, error, actions);
            document.body.append(dialog);
            dialog.showModal();
        });
    },

    exportFile(rows, name, fields) {
        const sheet = XLSX.utils.json_to_sheet(rows.map(row =>
            Object.fromEntries(fields.map(field => [field, row[field] ?? '']))), { header: fields });
        const book = XLSX.utils.book_new();
        XLSX.utils.book_append_sheet(book, sheet, 'DanhSach');
        XLSX.writeFile(book, name + '.xlsx');
    },

    bulk(input, title, endpoint, fields, required, numbers, refresh, individual = false) {
        return this.importFile(input, {
            title,
            key: required[0],
            map(row) {
                const value = {};
                for (const key of fields) {
                    const cell = String(row[key] ?? '').trim();
                    if (required.includes(key) && !cell) throw new Error(`Thiếu cột hoặc giá trị ${key}.`);
                    if (!cell) continue;
                    if (numbers.includes(key) && (!Number.isSafeInteger(Number(cell)) || Number(cell) < 0))
                        throw new Error(`${key} phải là số nguyên không âm.`);
                    value[key] = numbers.includes(key) ? Number(cell) : cell;
                }
                return value;
            },
            async save(rows) {
                if (!individual) return apiFetch(endpoint, { method: 'POST', body: JSON.stringify(rows) });
                let added = 0;
                const errors = [];
                for (const row of rows) {
                    try {
                        await apiFetch(endpoint, { method: 'POST', body: JSON.stringify(row) });
                        added++;
                    } catch (err) { errors.push(`${row[required[0]]}: ${err.message}`); }
                }
                const detail = errors.slice(0, 3).join(' · ');
                return { message: `Đã thêm ${added}/${rows.length} dòng. Lỗi ${errors.length}.${detail ? ' ' + detail : ''}` };
            },
            refresh
        });
    }
};
