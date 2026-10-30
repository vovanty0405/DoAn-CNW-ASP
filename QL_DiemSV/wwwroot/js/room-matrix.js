(() => {
    'use strict';
    const form = document.getElementById('matrix-filter');
    if (!form) return;
    // Tabs have full server URLs as a no-JS fallback; reload switches both controls and table atomically.
    document.querySelectorAll('.matrix-tab').forEach(tab => tab.addEventListener('click', event => {
        if (event.ctrlKey || event.metaKey || event.shiftKey || event.altKey || event.button !== 0) return;
        event.preventDefault();
        const url = new URL(tab.href, window.location.origin);
        url.searchParams.set('viewMode', tab.dataset.mode);
        window.location.assign(url.toString());
    }));
    form.querySelectorAll('[data-calendar]').forEach(select => select.addEventListener('change', () => {
        const date = form.elements.namedItem('date');
        if (date) date.value = '';
        if (select.dataset.calendar === 'term') {
            // Let the server resolve today's week within the newly selected term.
            form.elements.namedItem('week').disabled = true;
        }
        form.requestSubmit();
    }));
    document.getElementById('matrix-date')?.addEventListener('change', () => form.requestSubmit());
})();
