const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict'),path=require('node:path');
class Element {
 constructor(value=''){this._value=String(value);this.events={};this.dataset={};this.classes=new Set();this.classList={toggle:(key,enabled)=>enabled?this.classes.add(key):this.classes.delete(key)};this.error='';}
 get value(){return this._value;}set value(value){this._value=String(value);}
 addEventListener(name,handler){(this.events[name]??=[]).push(handler);}
 fire(name,event={}){for(const handler of this.events[name]??[])handler(event);}
 setCustomValidity(message){this.error=message;}
 focus(){}scrollIntoView(){}
}
const fields=Object.fromEntries(['maHK','ngayBatDauTuan1','soTuan','batDauDangKy','ketThucDangKy','donGiaTinChi'].map(name=>[name,new Element()]));
const form=new Element();form.elements={namedItem:name=>fields[name]};form.reportValidity=()=>Object.values(fields).every(field=>!field.error);
const terms=[{code:'A',label:'Học kỳ A',first:'2026-08-10',weeks:20,opens:'2026-08-01T07:00',closes:'2026-08-09T12:00',price:'635000'},{code:'B',label:'Học kỳ B',first:'2027-01-04',weeks:18,opens:'2026-12-01T07:00',closes:'2026-12-20T12:00',price:'700000'}];
const payload={textContent:JSON.stringify(terms)},cancel=new Element(),save=new Element(),message=new Element(),contextInput=new Element();
const rows=terms.map(term=>{const e=new Element();e.dataset.termRow=term.code;return e;});
const edits=terms.map(term=>{const e=new Element();e.dataset.term=term.code;return e;});
const deletion=new Element();deletion.elements={namedItem:()=>({value:'B'})};
const ids={'term-form':form,'term-config-data':payload,'term-cancel':cancel,'term-save':save,'term-edit-message':message};
const pageEvents={};
vm.runInNewContext(fs.readFileSync(path.join(__dirname,'../../QL_DiemSV/wwwroot/js/term-configuration.js'),'utf8'),{
 document:{getElementById:id=>ids[id],querySelectorAll:selector=>selector==='[data-term-row]'?rows:selector==='.term-edit'?edits:selector==='.term-delete'?[deletion]:selector==='input[name="configHK"]'?[contextInput]:[]},
 window:{addEventListener:(name,handler)=>pageEvents[name]=handler},confirm:()=>false,Date,Map
});
edits[1].fire('click');assert.equal(fields.maHK.value,'B');assert.equal(fields.soTuan.value,'18');assert.equal(fields.donGiaTinChi.value,'700000');assert.equal(fields.batDauDangKy.value,'2026-12-01T07:00');assert(rows[1].classes.has('term-selected-row'));assert.equal(contextInput.value,'B');
console.log('PASS edit fills every field and highlights selected semester without reload');
cancel.fire('click');assert.equal(fields.maHK.value,'');assert.equal(fields.ngayBatDauTuan1.value,'');assert.equal(fields.batDauDangKy.value,'');assert(save.disabled&&cancel.hidden);assert(rows.every(row=>!row.classes.has('term-selected-row')));
console.log('PASS cancel clears selection, dates, highlight and save state');
fields.maHK.value='A';fields.maHK.fire('change');assert.equal(fields.ngayBatDauTuan1.value,'2026-08-10');assert.equal(fields.ketThucDangKy.value,'2026-08-09T12:00');
console.log('PASS dropdown instantly loads semester data');
let prevented=false;fields.ngayBatDauTuan1.value='2026-08-11';form.fire('submit',{preventDefault(){prevented=true;}});assert(prevented&&fields.ngayBatDauTuan1.error);
console.log('PASS client rejects non-Monday week one');
fields.ngayBatDauTuan1.value='2026-08-10';fields.ngayBatDauTuan1.fire('input');fields.ketThucDangKy.value='2026-07-01T12:00';prevented=false;form.fire('submit',{preventDefault(){prevented=true;}});assert(prevented&&fields.ketThucDangKy.error);
console.log('PASS client rejects reversed registration window');
fields.ketThucDangKy.value='2026-08-09T12:00';fields.ketThucDangKy.fire('input');prevented=false;form.fire('submit',{preventDefault(){prevented=true;}});assert(!prevented&&save.disabled);
pageEvents.pageshow();assert.equal(save.disabled,false);
console.log('PASS valid submit and back navigation restore button state');
prevented=false;deletion.fire('submit',{preventDefault(){prevented=true;}});assert(prevented);
console.log('PASS rejecting delete confirmation prevents the request');
