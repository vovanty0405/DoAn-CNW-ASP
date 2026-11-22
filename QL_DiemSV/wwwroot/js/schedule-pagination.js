(() => {
    'use strict';

    document.querySelectorAll('[data-schedule-pagination]').forEach(table => {
        const rows = [...table.querySelectorAll('tbody > tr')]
            .filter(row => !row.querySelector('.empty-state'));
        let page = 1;
        let size = 10;
        const nav = document.createElement('nav');
        nav.className = 'toolbar';
        nav.setAttribute('aria-label', table.dataset.schedulePagination);
        const summary = document.createElement('span');
        summary.className = 'muted';
        summary.setAttribute('role', 'status');
        summary.setAttribute('aria-live', 'polite');
        const label = document.createElement('label');
        label.textContent = 'Số dòng mỗi trang ';
        const sizes = document.createElement('select');
        [5, 10, 20, 50].forEach(value => sizes.add(new Option(value, value, false, value === size)));
        label.append(sizes);
        const previous = document.createElement('button');
        previous.type = 'button';
        previous.className = 'secondary';
        previous.textContent = '« Trước';
        const next = document.createElement('button');
        next.type = 'button';
        next.className = 'secondary';
        next.textContent = 'Sau »';
        const position = document.createElement('span');
        nav.append(summary, label, previous, position, next);
        table.closest('.scroll').after(nav);

        function render() {
            const pages = Math.max(1, Math.ceil(rows.length / size));
            page = Math.max(1, Math.min(page, pages));
            const start = (page - 1) * size;
            rows.forEach((row, index) => { row.hidden = index < start || index >= start + size; });
            summary.textContent = `Hiển thị ${rows.length ? start + 1 : 0}–${Math.min(start + size, rows.length)} / ${rows.length}`;
            position.textContent = `Trang ${page} / ${pages}`;
            previous.disabled = page === 1;
            next.disabled = page === pages;
        }

        sizes.addEventListener('change', () => { size = Number(sizes.value); page = 1; render(); });
        previous.addEventListener('click', () => { page--; render(); });
        next.addEventListener('click', () => { page++; render(); });
        if (table.id === 'term-table') {
            const picker = document.getElementById('term-select');
            function revealTerm() {
                const index = rows.findIndex(row => row.dataset.termRow === picker?.value);
                if (index >= 0) page = Math.floor(index / size) + 1;
                render();
            }
            picker?.addEventListener('change', revealTerm);
            revealTerm();
        } else render();
    });
})();
