(() => {
'use strict';
const scheduleForm=document.getElementById('schedule-form');
if(scheduleForm){
 document.getElementById('add-schedule').addEventListener('click',()=>document.getElementById('schedule-rows').append(document.getElementById('schedule-template').content.cloneNode(true)));
 scheduleForm.addEventListener('submit',event=>{
  const rows=[...document.querySelectorAll('#schedule-rows tr')];
  for(const row of rows){
   const start=Number(row.querySelector('[data-field="TietBatDau"]').value), count=Number(row.querySelector('[data-field="SoTiet"]').value);
   if(start+count>13){event.preventDefault();row.querySelector('[data-field="SoTiet"]').focus();alert('Buổi học không được kéo dài quá tiết 12.');return;}
  }
  rows.forEach((row,i)=>row.querySelectorAll('[data-field]').forEach(input=>input.name=`schedules[${i}].${input.dataset.field}`));
 });
}
const roomForm=document.getElementById('room-form'), cancel=document.getElementById('room-cancel');
const field=name=>roomForm.elements.namedItem(name);
function reset(){roomForm.reset();field('originalMaPhong').value='';document.getElementById('room-save').textContent='Thêm phòng';cancel.hidden=true;document.getElementById('room-edit-message').textContent='Chọn Sửa để cập nhật hoặc đổi mã phòng.';}
cancel.addEventListener('click',reset);
document.querySelectorAll('.room-edit').forEach(button=>button.addEventListener('click',()=>{
 field('originalMaPhong').value=button.dataset.code;field('maPhong').value=button.dataset.code;field('sucChua').value=button.dataset.capacity;field('toaNha').value=button.dataset.building;field('trangThai').value=button.dataset.status;
 document.getElementById('room-save').textContent='Lưu thay đổi';cancel.hidden=false;document.getElementById('room-edit-message').textContent='Đang sửa phòng '+button.dataset.code;roomForm.scrollIntoView({behavior:'smooth',block:'center'});field('maPhong').focus({preventScroll:true});
}));
document.querySelectorAll('.room-delete').forEach(form=>form.addEventListener('submit',event=>{if(!confirm('Xóa phòng '+form.elements.namedItem('maPhong').value+'? Chỉ phòng chưa được sử dụng mới được xóa.'))event.preventDefault();}));
})();
