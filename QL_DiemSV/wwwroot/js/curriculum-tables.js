(() => {
    'use strict';
    function normalize(root) {
        root.querySelectorAll('table,colgroup,col,tr,td,th').forEach(el=>{
            el.removeAttribute('width');el.removeAttribute('nowrap');
            el.style.removeProperty('width');el.style.removeProperty('min-width');el.style.removeProperty('max-width');
            el.style.whiteSpace='normal';
            if(el.getAttribute('align')?.toLowerCase()==='right')el.removeAttribute('align');
            if(el.matches('td,th')) {
                el.removeAttribute('align');el.style.textAlign='left';el.style.verticalAlign='middle';
                el.style.border='1px solid var(--curriculum-table-border, #e2e8f0)';el.style.padding='10px 14px';
                el.querySelectorAll('[align],[style]').forEach(child=>{
                    child.removeAttribute('align');child.style.removeProperty('width');child.style.removeProperty('min-width');
                    child.style.textAlign='left';child.style.whiteSpace='normal';
                });
            }
            if(el.matches('table')){el.style.borderCollapse='collapse';el.style.tableLayout='auto';}
        });
        root.querySelectorAll('table').forEach(table=>{
            [...table.rows].forEach(row=>{
                const label=row.cells[0]?.textContent.trim()??'';
                row.classList.toggle('curriculum-total-row',/^(tổng cộng|tổng số|tổng)\s*[:：]?$/iu.test(label));
            });
        });
        return root;
    }
    window.CurriculumTables={normalize};
    document.querySelectorAll('.curriculum-section-content').forEach(normalize);
})();
