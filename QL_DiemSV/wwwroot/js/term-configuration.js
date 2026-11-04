(() => {
    'use strict';
    const form=document.getElementById('term-form');
    const payload=document.getElementById('term-config-data');
    if(!form||!payload)return;
    const terms=new Map(JSON.parse(payload.textContent).map(term=>[term.code,term]));
    const field=name=>form.elements.namedItem(name);
    const cancel=document.getElementById('term-cancel');
    const save=document.getElementById('term-save');
    const message=document.getElementById('term-edit-message');
    const hint='Chọn Sửa để cập nhật thông tin học kỳ hoặc Xóa để gỡ bỏ.';
    const inputs=['ngayBatDauTuan1','soTuan','batDauDangKy','ketThucDangKy','donGiaTinChi'];
    function clearErrors(){inputs.forEach(name=>field(name).setCustomValidity(''));}
    function select(code,scroll=false){
        clearErrors();
        const term=terms.get(code);
        field('maHK').value=term?.code??'';
        const values=term? [term.first??'',term.weeks,term.opens??'',term.closes??'',term.price]:['',20,'','','635000'];
        inputs.forEach((name,i)=>field(name).value=values[i]);
        save.disabled=!term;cancel.hidden=!term;
        message.textContent=term?'Đang cấu hình: '+term.label+'. '+hint:hint;
        document.querySelectorAll('[data-term-row]').forEach(row=>row.classList.toggle('term-selected-row',row.dataset.termRow===term?.code));
        document.querySelectorAll('input[name="configHK"]').forEach(input=>input.value=term?.code??'');
        if(scroll){form.scrollIntoView({behavior:'smooth',block:'center'});field('ngayBatDauTuan1').focus({preventScroll:true});}
    }
    field('maHK').addEventListener('change',()=>select(field('maHK').value));
    document.querySelectorAll('.term-edit').forEach(button=>button.addEventListener('click',()=>select(button.dataset.term,true)));
    cancel.addEventListener('click',()=>select(''));
    inputs.forEach(name=>field(name).addEventListener('input',clearErrors));
    form.addEventListener('submit',event=>{
        clearErrors();
        const first=field('ngayBatDauTuan1');
        if(first.value&&new Date(first.value+'T12:00:00').getDay()!==1)first.setCustomValidity('Ngày bắt đầu tuần 1 phải là Thứ Hai.');
        const opens=field('batDauDangKy'),closes=field('ketThucDangKy');
        if(opens.value&&closes.value&&closes.value<=opens.value)closes.setCustomValidity('Kết thúc đăng ký phải sau bắt đầu đăng ký.');
        if(!form.reportValidity()){event.preventDefault();return;}
        save.disabled=true;save.textContent='Đang lưu…';
    });
    document.querySelectorAll('.term-delete').forEach(deleteForm=>deleteForm.addEventListener('submit',event=>{
        const term=terms.get(deleteForm.elements.namedItem('maHK').value);
        if(!confirm('Xóa '+(term?.label??'học kỳ này')+'? Học kỳ đã có lớp học phần sẽ không được xóa.'))event.preventDefault();
    }));
    // Native reset restores validation/UI state after returning through browser history.
    window.addEventListener('pageshow',()=>{save.disabled=!terms.has(field('maHK').value);save.textContent='Lưu cấu hình học kỳ';});
})();
