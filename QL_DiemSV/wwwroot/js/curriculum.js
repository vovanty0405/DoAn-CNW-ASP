(() => {
'use strict';
const allowed=new Set('p div span br hr strong b em i u s sub sup font h1 h2 h3 h4 h5 h6 ul ol li blockquote pre code a img table caption colgroup col thead tbody tfoot tr th td'.split(' '));
const styles=new Set('color background-color text-align font-size font-family font-weight font-style text-decoration line-height border border-top border-bottom border-left border-right border-collapse border-color padding padding-top padding-bottom padding-left padding-right margin margin-left margin-right width max-width min-width vertical-align list-style-type'.split(' '));
function safeUrl(url,image=false){if(url.includes(String.fromCharCode(92)))return false;if(url.startsWith('/')&&!url.startsWith('//'))return !image||url.startsWith('/images/')||url.startsWith('/uploads/');try{return (image?['https:']:['https:','http:']).includes(new URL(url).protocol);}catch{return false;}}
function clean(html){
 const doc=new DOMParser().parseFromString(html,'text/html');
 doc.querySelectorAll('script,style,iframe,object,embed,form,meta,link,svg,math,template').forEach(el=>el.remove());
 [...doc.body.querySelectorAll('*')].reverse().forEach(el=>{
  if(!allowed.has(el.localName)){el.replaceWith(...el.childNodes);return;}
  const attrs={};for(const name of ['rowspan','colspan','span']){const n=Number(el.getAttribute(name));if(Number.isInteger(n)&&n>0&&n<=1000)attrs[name]=String(n);}
  if(el.localName==='a'&&safeUrl(el.getAttribute('href')||''))attrs.href=el.getAttribute('href');
  if(el.localName==='img'){if(!safeUrl(el.getAttribute('src')||'',true)){el.remove();return;}attrs.src=el.getAttribute('src');attrs.alt=el.getAttribute('alt')||'';}
  if(el.localName==='font')for(const name of ['face','size','color'])if(el.hasAttribute(name))attrs[name]=el.getAttribute(name);
  const style=document.createElement('span').style;for(const prop of el.style){const value=el.style.getPropertyValue(prop);if(styles.has(prop)&&!/(url\s*\(|expression|javascript|[<>])/i.test(value)&&!value.includes(String.fromCharCode(92)))style.setProperty(prop,value);}
  if(['td','th'].includes(el.localName)){style.border=style.border||'1px solid #94a3b8';style.padding=style.padding||'8px';}
  if(el.localName==='table'){style.borderCollapse='collapse';style.width=style.width||'100%';}
  const align=el.getAttribute('align');if(['left','center','right','justify'].includes(align))style.textAlign=align;
  [...el.attributes].forEach(a=>el.removeAttribute(a.name));for(const [key,value]of Object.entries(attrs))el.setAttribute(key,value);
  if(style.cssText)el.setAttribute('style',style.cssText);
 });
 window.CurriculumTables?.normalize(doc.body);
 return doc.body.innerHTML;
}
function xml(html){const doc=new DOMParser().parseFromString(clean(html),'text/html');return [...doc.body.childNodes].map(n=>new XMLSerializer().serializeToString(n)).join('').replace(/ xmlns="http:\/\/www.w3.org\/1999\/xhtml"/g,'');}
const button=(name,label)=>`<button type="button" data-action="${name}" title="${label}">${label}</button>`;
const select=(action,values)=>`<select data-action="${action}" aria-label="${action}">${values.map(([value,label])=>`<option value="${value}">${label}</option>`).join('')}</select>`;
const group=(name,html)=>`<div class="ribbon-group"><div>${html}</div><small>${name}</small></div>`;
document.querySelectorAll('.curriculum-popover').forEach(pop=>pop.addEventListener('toggle',()=>{if(pop.open)document.querySelectorAll('.curriculum-popover').forEach(other=>{if(other!==pop)other.open=false;});}));
document.addEventListener('click',e=>document.querySelectorAll('.curriculum-popover[open]').forEach(p=>{if(!p.contains(e.target))p.open=false;}));
document.addEventListener('keydown',e=>{if(e.key==='Escape')document.querySelectorAll('.curriculum-popover[open]').forEach(p=>p.open=false);});
const form=document.getElementById('curriculum-editor');if(!form)return;
form.querySelectorAll('.curriculum-section').forEach((section,index)=>{
 const rich=section.querySelector('.curriculum-rich'),source=section.querySelector('.curriculum-source'),ribbon=section.querySelector('.word-ribbon');
 window.CurriculumTables?.normalize(rich);
 const notice=document.createElement('p');notice.className='editor-notice';notice.setAttribute('role','status');ribbon.after(notice);
 ribbon.innerHTML=group('Clipboard & Lịch sử',['undo','redo','copy','paste','removeFormat'].map((n,i)=>button(n,['↶ Undo','↷ Redo','Copy','Dán định dạng','Xóa định dạng'][i])).join(''))+
 group('Font',select('fontName',['Inter','Arial','Times New Roman','Aptos'].map(x=>[x,x]))+select('points',[8,9,10,11,12,14,16,18,20,24,28,32,36].map(x=>[x,x+'pt']))+button('bigger','A↑')+button('smaller','A↓')+select('formatBlock',[['p','Normal'],['h1','Heading 1'],['h2','Heading 2'],['h3','Heading 3']]))+
 group('Định dạng',['bold','italic','underline','strikeThrough','subscript','superscript'].map((n,i)=>button(n,['B','I','U','ab̶','X₂','X²'][i])).join('')+'<label>Màu chữ<input type="color" data-action="foreColor" value="#173d70"></label><label>Tô sáng<input type="color" data-action="hiliteColor" value="#fff099"></label>')+
 group('Đoạn văn',['insertUnorderedList','insertOrderedList','outdent','indent','justifyLeft','justifyCenter','justifyRight','justifyFull'].map((n,i)=>button(n,['• List','1. List','⇤','⇥','Trái','Giữa','Phải','Đều'][i])).join('')+select('lineHeight',[['1','Dòng 1'],['1.15','Dòng 1.15'],['1.5','Dòng 1.5'],['2','Dòng 2']]))+
 group('Bảng & Chèn','<details class="table-picker"><summary>Kẻ bảng ▦</summary><div class="table-picker-grid">'+Array.from({length:64},(_,n)=>`<button type="button" data-rows="${Math.floor(n/8)+1}" data-cols="${n%8+1}" title="${Math.floor(n/8)+1} dòng × ${n%8+1} cột">·</button>`).join('')+'</div></details>'+select('table',[['','Sửa bảng…'],['rowBefore','Dòng phía trên'],['rowAfter','Dòng phía dưới'],['colBefore','Cột trái'],['colAfter','Cột phải'],['deleteRow','Xóa dòng'],['deleteCol','Xóa cột'],['merge','Gộp ô đã chọn'],['split','Tách ô'],['deleteTable','Xóa bảng'],['borders','Kẻ viền'],['noBorders','Bỏ viền']])+'<label>Nền ô<input type="color" data-action="shading" value="#dceaff"></label>'+button('template','📊 Bảng mẫu Khối kiến thức')+button('image','Ảnh')+button('insertHorizontalRule','Đường kẻ')+button('link','Liên kết')+button('html','</> HTML'));
 ribbon.querySelector('[data-action="points"]').value='12';
 let range=null,activeCell=null,selected=new Set(),history=[rich.innerHTML],cursor=0;
 const say=text=>notice.textContent=text;
 const remember=()=>{const sel=getSelection();if(sel.rangeCount&&rich.contains(sel.anchorNode))range=sel.getRangeAt(0).cloneRange();};
 const restore=()=>{rich.focus();if(range&&rich.contains(range.startContainer)){const sel=getSelection();sel.removeAllRanges();sel.addRange(range);}};
 const sync=(record=true)=>{rich.querySelectorAll('.editor-cell-selected').forEach(c=>c.classList.remove('editor-cell-selected'));source.value=xml(rich.innerHTML);if(record&&history[cursor]!==rich.innerHTML){history=history.slice(0,cursor+1);history.push(rich.innerHTML);if(history.length>40)history.shift();cursor=history.length-1;}selected.forEach(c=>{if(c.isConnected)c.classList.add('editor-cell-selected');});};
 const command=(name,value=null)=>{restore();document.execCommand(name,false,value);remember();sync();};
 const insert=html=>command('insertHTML',clean(html));
 function matrix(table){const grid=[];[...table.rows].forEach((row,r)=>{grid[r]??=[];let col=0;[...row.cells].forEach(cell=>{while(grid[r][col])col++;for(let y=0;y<cell.rowSpan;y++){grid[r+y]??=[];for(let x=0;x<cell.colSpan;x++)grid[r+y][col+x]=cell;}col+=cell.colSpan;});});return grid;}
 function rebuild(table,grid){const body=document.createElement('tbody'),seen=new Set();grid.forEach((row,r)=>{const tr=document.createElement('tr');row.forEach((cell,c)=>{if(seen.has(cell))return;seen.add(cell);const clone=cell.cloneNode(true);clone.classList.remove('editor-cell-selected');let w=1,h=1;while(row[c+w]===cell)w++;while(grid[r+h]?.[c]===cell)h++;clone.colSpan=w;clone.rowSpan=h;tr.append(clone);});body.append(tr);});table.replaceChildren(body);selected.clear();activeCell=null;sync();}
 const fresh=()=>{const td=document.createElement('td');td.innerHTML='<br>';td.style.cssText='border:1px solid #94a3b8;padding:8px';return td;};
 function tableAction(action){
  const table=activeCell?.closest('table');if(!table||!rich.contains(table)){say('Đặt con trỏ trong bảng trước. Ctrl/⌘ + click để chọn nhiều ô cần gộp.');return;}
  let grid=matrix(table),r=grid.findIndex(row=>row.includes(activeCell)),c=grid[r].indexOf(activeCell);
  if(action==='deleteTable'){table.remove();selected.clear();activeCell=null;sync();return;}
  if(action==='borders'||action==='noBorders'){table.style.borderCollapse='collapse';table.querySelectorAll('td,th').forEach(cell=>cell.style.border=action==='borders'?'1px solid #94a3b8':'none');sync();return;}
  if(action==='merge'){
   const cells=selected.size?selected:new Set([activeCell]);if([...cells].some(cell=>cell.closest('table')!==table)){say('Chỉ chọn các ô trong cùng một bảng.');return;}const points=[];grid.forEach((row,y)=>row.forEach((cell,x)=>{if(cells.has(cell))points.push([y,x]);}));
   const top=Math.min(...points.map(p=>p[0])),bottom=Math.max(...points.map(p=>p[0])),left=Math.min(...points.map(p=>p[1])),right=Math.max(...points.map(p=>p[1]));
   for(let y=top;y<=bottom;y++)for(let x=left;x<=right;x++)if(!cells.has(grid[y][x])){say('Chọn đủ các ô trong một vùng hình chữ nhật để gộp.');return;}
   const first=grid[top][left];first.innerHTML=[...new Set(grid.flat().filter(cell=>cells.has(cell)))].map(cell=>cell.innerHTML).join('<br>');for(let y=top;y<=bottom;y++)for(let x=left;x<=right;x++)grid[y][x]=first;
  }else if(action==='split'){
   const target=activeCell;grid=grid.map(row=>row.map(cell=>{if(cell!==target)return cell;const td=fresh();return td;}));grid[r][c].innerHTML=target.innerHTML;
  }else if(action==='deleteRow'){grid.splice(r,1);}
  else if(action==='deleteCol'){grid.forEach(row=>row.splice(c,1));}
  else if(action.startsWith('row')){const at=action==='rowBefore'?r:r+activeCell.rowSpan;grid.splice(at,0,Array.from({length:grid[0].length},(_,x)=>grid[at-1]?.[x]&&grid[at-1][x]===grid[at]?.[x]?grid[at][x]:fresh()));}
  else if(action.startsWith('col')){const at=action==='colBefore'?c:c+activeCell.colSpan;grid.forEach(row=>row.splice(at,0,row[at-1]&&row[at-1]===row[at]?row[at]:fresh()));}
  if(!grid.length||!grid[0].length){table.remove();sync();}else rebuild(table,grid);
 }
 function points(size){restore();document.execCommand('fontSize',false,'7');rich.querySelectorAll('font[size="7"]').forEach(el=>{el.removeAttribute('size');el.style.fontSize=size+'pt';});sync();}
 async function action(name,value){
  if(name==='undo'||name==='redo'){cursor=Math.max(0,Math.min(history.length-1,cursor+(name==='undo'?-1:1)));rich.innerHTML=history[cursor];range=null;selected.clear();sync(false);return;}
  if(name==='table'){tableAction(value);return;}
  if(name==='points'){points(Number(value));return;}if(name==='bigger'||name==='smaller'){const sizes=[8,9,10,11,12,14,16,18,20,24,28,32,36];const current=Number(ribbon.querySelector('[data-action="points"]').value)||12;const next=sizes[Math.max(0,Math.min(sizes.length-1,sizes.indexOf(current)+(name==='bigger'?1:-1)))];ribbon.querySelector('[data-action="points"]').value=String(next);points(next);return;}
  if(name==='lineHeight'){restore();let node=getSelection().anchorNode?.parentElement?.closest('p,div,li,h1,h2,h3');if(node===rich){command('formatBlock','p');node=getSelection().anchorNode?.parentElement?.closest('p');}if(node&&rich.contains(node))node.style.lineHeight=value;sync();return;}
  if(name==='shading'){(selected.size?selected:new Set([activeCell])).forEach(c=>{if(c)c.style.backgroundColor=value;});sync();return;}
  if(name==='html'){source.closest('label').hidden=!source.closest('label').hidden;if(!source.closest('label').hidden)source.focus();return;}
  if(name==='copy'){restore();if(!document.execCommand('copy'))say('Dùng Ctrl+C để sao chép vùng đang chọn.');return;}
  if(name==='paste'){try{restore();const items=await navigator.clipboard.read();for(const item of items){if(item.types.includes('text/html')){insert(await(await item.getType('text/html')).text());return;}if(item.types.includes('text/plain')){command('insertText',await(await item.getType('text/plain')).text());return;}}}catch{say('Trình duyệt không cấp quyền đọc Clipboard. Đặt con trỏ trong nội dung và nhấn Ctrl+V để dán giữ bảng.');}return;}
  if(name==='link'||name==='image'){const url=prompt(name==='image'?'URL ảnh https://...':'URL liên kết https://...');if(!url)return;if(!safeUrl(url,name==='image')){say('Liên kết không hợp lệ.');return;}command(name==='image'?'insertImage':'createLink',url);return;}
  if(name==='template'){insert('<table><thead><tr><th rowspan="2">Khối kiến thức</th><th colspan="2">Khối lượng</th></tr><tr><th>Tổng số tín chỉ</th><th>Tỷ lệ (%)</th></tr></thead><tbody><tr><td>Giáo dục đại cương</td><td>0</td><td>0</td></tr><tr><td>Giáo dục chuyên nghiệp</td><td>0</td><td>0</td></tr><tr><td>Tốt nghiệp</td><td>0</td><td>0</td></tr></tbody></table><p><br></p>');return;}
  command(name,value);
 }
 ribbon.querySelectorAll('button').forEach(button=>{button.addEventListener('mousedown',e=>e.preventDefault());button.addEventListener('click',()=>{if(button.dataset.rows){const rows=Number(button.dataset.rows),cols=Number(button.dataset.cols);insert('<table><tbody>'+Array.from({length:rows},()=>'<tr>'+('<td><br></td>'.repeat(cols))+'</tr>').join('')+'</tbody></table><p><br></p>');button.closest('details').open=false;}else action(button.dataset.action,null);});});
 ribbon.querySelectorAll('select,input').forEach(control=>control.addEventListener('change',()=>{action(control.dataset.action,control.value);if(control.dataset.action==='table')control.value='';}));
 rich.addEventListener('mouseup',remember);rich.addEventListener('keyup',remember);rich.addEventListener('input',()=>{remember();sync();});
 rich.addEventListener('click',e=>{const cell=e.target.closest('td,th');if(!cell)return;activeCell=cell;if(!e.ctrlKey&&!e.metaKey){selected.forEach(c=>c.classList.remove('editor-cell-selected'));selected.clear();}if(selected.has(cell)){selected.delete(cell);cell.classList.remove('editor-cell-selected');}else{selected.add(cell);cell.classList.add('editor-cell-selected');}say('Ctrl/⌘ + click các ô liền nhau, sau đó chọn Gộp ô.');});
 rich.addEventListener('keydown',e=>{if((e.ctrlKey||e.metaKey)&&['z','y'].includes(e.key.toLowerCase())){e.preventDefault();action(e.shiftKey||e.key.toLowerCase()==='y'?'redo':'undo');}});
 rich.addEventListener('paste',e=>{e.preventDefault();remember();const html=e.clipboardData.getData('text/html');if(html)insert(html);else command('insertText',e.clipboardData.getData('text/plain'));});
 source.addEventListener('input',()=>{rich.innerHTML=clean(source.value);range=null;selected.clear();if(history[cursor]!==rich.innerHTML){history=history.slice(0,cursor+1);history.push(rich.innerHTML);if(history.length>40)history.shift();cursor=history.length-1;}});
 form.addEventListener('submit',()=>{selected.forEach(c=>c.classList.remove('editor-cell-selected'));source.value=xml(rich.innerHTML);});
 sync(false);
});
})();
