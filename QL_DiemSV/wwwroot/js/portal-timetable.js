(() => {
    'use strict';
    const form=document.getElementById('timetable-filter');
    if(!form)return;
    form.querySelectorAll('[data-tkb]').forEach(select=>select.addEventListener('change',()=>{
        const week=form.elements.namedItem('week');
        // Semester selection recalculates the current week rather than carrying an old semester's week.
        if(select.dataset.tkb==='term' && week)week.disabled=true;
        if(select.dataset.tkb==='mode' && select.value==='all' && week)week.disabled=true;
        form.requestSubmit();
    }));
    document.getElementById('print-timetable')?.addEventListener('click',()=>window.print());
})();
