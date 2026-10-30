(() => {
'use strict';
const data = JSON.parse(document.getElementById('registration-data').textContent);
const chosen = new Set(data.filter(x => x.saved).map(x => x.id));
const original = new Set(chosen);
const money = n => Number(n).toLocaleString('en-US',{maximumFractionDigits:2});
const message = (text,error=false) => {const el=document.getElementById('registration-message');el.textContent=text;el.className='notice '+(error?'error':'success');};
let dirty=false, busy=false;
function render(){
 const body=document.getElementById('selected-courses');body.replaceChildren();
 const totals=[0,0,0,0,0];
 data.filter(x=>chosen.has(x.id)).forEach((x,i)=>{
  const row=document.createElement('tr');const due=x.fee-x.discount;
  [i+1,x.code,x.name,x.group,x.practice||'—',x.credits,x.feeCredits,money(x.fee)+' đ',money(x.discount)+' đ',money(due)+' đ'].forEach(v=>{const td=document.createElement('td');td.textContent=v;row.append(td);});
  const status=document.createElement('td');status.textContent=x.saved?'Đã lưu vào CSDL':'Chưa lưu';status.className=x.saved?'success':'pending';row.append(status);
  const td=document.createElement('td'), checkbox=document.createElement('input');checkbox.type='checkbox';checkbox.dataset.remove=x.id;checkbox.setAttribute('aria-label','Xóa '+x.code);checkbox.disabled=!window.registrationOpen||busy;td.append(checkbox);row.append(td);body.append(row);
  [x.credits,x.feeCredits,x.fee,x.discount,due].forEach((v,j)=>totals[j]+=v);
 });
 const footer=document.getElementById('selected-totals');footer.replaceChildren();const tr=document.createElement('tr'),label=document.createElement('td');label.colSpan=5;label.textContent='Tổng cộng';tr.append(label);
 totals.forEach((v,i)=>{const td=document.createElement('td');td.textContent=money(v)+(i>1?' đ':'');tr.append(td);});const tail=document.createElement('td');tail.colSpan=2;tr.append(tail);footer.append(tr);
 document.querySelectorAll('.section-choice').forEach(c=>{c.checked=chosen.has(c.dataset.id);const x=data.find(x=>x.id===c.dataset.id);c.disabled=busy||!window.registrationOpen||(original.has(x.id)&&chosen.has(x.id))||(c.closest('tr').querySelector('.error')!==null);});
}
document.querySelectorAll('.section-choice').forEach(c=>c.addEventListener('change',()=>{
 const x=data.find(x=>x.id===c.dataset.id);
 if(c.checked){
  for(const y of data.filter(y=>chosen.has(y.id))){
   if(x.code===y.code){c.checked=false;message('Không thể chọn hai nhóm của môn '+x.code+'.',true);return;}
   for(const a of x.schedules)for(const b of y.schedules){
    const weeks=a.weeks.filter(w=>b.weeks.includes(w));
    if(a.day===b.day&&a.start<b.start+b.count&&b.start<a.start+a.count&&weeks.length){c.checked=false;message(x.code+' trùng '+y.code+' vào '+(a.day===8?'Chủ nhật':'thứ '+a.day)+', tiết '+Math.max(a.start,b.start)+', tuần '+weeks.join(', ')+'.',true);return;}
   }
  }
  chosen.add(x.id);
 }else chosen.delete(x.id);
 dirty=true;message('Lựa chọn chưa được lưu.');render();
}));
document.getElementById('remove-registration').addEventListener('click',()=>{if(busy)return;document.querySelectorAll('[data-remove]:checked').forEach(c=>chosen.delete(c.dataset.remove));dirty=true;message('Đã xóa khỏi danh sách chọn. Bấm Lưu Đăng Ký để cập nhật CSDL.');render();});
document.getElementById('save-registration').addEventListener('click',async()=>{
 if(busy)return;busy=true;const btn=document.getElementById('save-registration');btn.disabled=true;render();
 try{
  const response=await fetch('/dang-ky-hoc-phan/luu',{method:'POST',headers:{'Content-Type':'application/json','RequestVerificationToken':document.querySelector('#token input').value},body:JSON.stringify({MaHK:window.registrationTerm,SectionIds:[...chosen]})});
  const result=await response.json();if(!response.ok)throw new Error(result.message||'Không thể lưu đăng ký.');dirty=false;location.reload();
 }catch(e){message(e.message,true);}finally{busy=false;btn.disabled=!window.registrationOpen;render();}
});
window.addEventListener('beforeunload',e=>{if(dirty){e.preventDefault();e.returnValue='';}});
render();
})();
