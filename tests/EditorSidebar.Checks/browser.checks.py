from pathlib import Path
import subprocess, json, re, html
root=Path(__file__).resolve().parents[2]
work=root/'.verification'/'editor-browser';work.mkdir(parents=True,exist_ok=True)
editor=(root/'QL_DiemSV/wwwroot/js/curriculum.js').read_text(encoding='utf-8')
sidebar=(root/'QL_DiemSV/wwwroot/js/sidebar.js').read_text(encoding='utf-8')
tables=(root/'QL_DiemSV/wwwroot/js/curriculum-tables.js').read_text(encoding='utf-8')
css=(root/'QL_DiemSV/wwwroot/css/curriculum.css').read_text(encoding='utf-8')
fixture=''.join('<details class="curriculum-section" open><div class="word-ribbon"></div><div class="curriculum-rich curriculum-section-content" contenteditable="true"><p>Sample</p></div><label><textarea class="curriculum-source" name="Contents['+str(n)+']"></textarea></label></details>' for n in range(11))
checks=r"""
const results=[];function check(ok,name){if(!ok)throw Error(name);results.push(name);}
try {
const legacy=document.getElementById('legacy-content'),legacyTable=legacy.querySelector('table'),legacyCell=legacy.querySelector('td');
check(!legacyTable.hasAttribute('width')&&!legacyTable.style.width&&!legacyCell.hasAttribute('nowrap')&&!legacyCell.hasAttribute('align'),'legacy fixed width and nowrap removed on Portal load');
check(legacyCell.rowSpan===2,'render normalization preserves merged cells');
check(getComputedStyle(legacyCell).textAlign==='left'&&getComputedStyle(legacyCell.querySelector('p')).textAlign==='left','cells and nested Word paragraphs align left');
const box=document.querySelector('.curriculum-detail-page').getBoundingClientRect();
check(box.width<=1024&&Math.abs(box.left-(document.documentElement.clientWidth-box.width)/2)<2,'detail container centered at maximum 1024px');
check(getComputedStyle(document.getElementById('next-section')).overflow==='hidden','accordion item clips overflowing content independently');
check(getComputedStyle(legacyCell).borderLeftWidth==='1px'&&getComputedStyle(legacyCell).borderLeftColor==='rgb(226, 232, 240)','light theme table borders');
document.documentElement.className='dark';check(getComputedStyle(legacyCell).borderLeftColor==='rgb(51, 65, 85)'&&getComputedStyle(legacyCell).backgroundColor==='rgb(30, 41, 59)','dark theme border and header colors');document.documentElement.className='light';
check(legacy.querySelector('.curriculum-total-row')!==null,'total row emphasized');
check(document.querySelectorAll('.word-ribbon [data-action="fontName"]').length===11,'ribbon exists for all eleven sections');
const section=document.querySelector('.curriculum-section'),rich=section.querySelector('.curriculum-rich'),source=section.querySelector('textarea');
rich.focus();let range=document.createRange();range.selectNodeContents(rich);range.collapse(false);getSelection().removeAllRanges();getSelection().addRange(range);rich.dispatchEvent(new MouseEvent('mouseup',{bubbles:true}));
const pasted='<table style="mso-table-layout-alt:fixed"><tbody><tr><td rowspan="2" colspan="2" style="mso-bidi-font-size:12pt;color:red">A</td><td>B</td></tr><tr><td>C</td></tr></tbody></table>';
const paste=new Event('paste',{bubbles:true,cancelable:true});Object.defineProperty(paste,'clipboardData',{value:{getData:type=>type==='text/html'?pasted:''}});rich.dispatchEvent(paste);
check(rich.querySelector('td[rowspan="2"][colspan="2"]')!==null,'paste preserves merged Word/Excel table cells');check(!source.value.includes('mso-'),'paste removes Word-only CSS');check(!rich.querySelector('table').style.width&&!rich.querySelector('td').hasAttribute('width'),'pasted tables remove fixed widths in Admin');check(source.value.includes('border'),'paste normalizes borders');
source.value='<p style="font-size:18pt">HTML update</p>';source.dispatchEvent(new Event('input'));check(rich.textContent==='HTML update','HTML immediately updates visual editor');
rich.innerHTML='<table><tbody><tr><td>A</td><td>B</td></tr><tr><td>C</td><td>D</td></tr></tbody></table>';rich.dispatchEvent(new Event('input'));
let cells=[...rich.querySelectorAll('td')];cells[0].dispatchEvent(new MouseEvent('click',{bubbles:true}));cells[1].dispatchEvent(new MouseEvent('click',{bubbles:true,ctrlKey:true}));
const tableAction=section.querySelector('[data-action="table"]');tableAction.value='merge';tableAction.dispatchEvent(new Event('change'));check(rich.querySelector('td').colSpan===2,'merge rectangular selected cells');
rich.querySelector('td').dispatchEvent(new MouseEvent('click',{bubbles:true}));tableAction.value='split';tableAction.dispatchEvent(new Event('change'));check(rich.querySelectorAll('td').length===4,'split restores individual cells');
rich.querySelector('td').dispatchEvent(new MouseEvent('click',{bubbles:true}));tableAction.value='rowAfter';tableAction.dispatchEvent(new Event('change'));check(rich.querySelectorAll('tr').length===3,'insert row');
rich.querySelector('td').dispatchEvent(new MouseEvent('click',{bubbles:true}));tableAction.value='colAfter';tableAction.dispatchEvent(new Event('change'));check(rich.querySelector('tr').cells.length===3,'insert column');
const font=section.querySelector('[data-action="points"]');range=document.createRange();range.selectNodeContents(rich.querySelector('td'));getSelection().removeAllRanges();getSelection().addRange(range);rich.dispatchEvent(new MouseEvent('mouseup'));font.value='18';font.dispatchEvent(new Event('change'));check(source.value.includes('18pt')||source.value.includes('18pt'),'font size saved as points');
check(document.querySelector('[data-sidebar-group="training"] button').getAttribute('aria-expanded')==='true','active sidebar group expands despite saved closed state');
const group=document.querySelector('[data-sidebar-group="portal"]'),toggle=group.querySelector('button');toggle.click();check(toggle.getAttribute('aria-expanded')==='true','sidebar toggles open');toggle.click();check(localStorage.getItem('academic-sidebar:portal')==='closed','sidebar remembers closed state');
document.getElementById('curriculum-editor').dispatchEvent(new Event('submit',{cancelable:true}));check(!source.value.includes('editor-cell-selected'),'selection UI is not saved into content');
document.getElementById('result').textContent=JSON.stringify({ok:true,results});
}catch(error){document.getElementById('result').textContent=JSON.stringify({ok:false,error:error.message,results});}
"""
page='<html class="light"><head><style>body{margin:0}'+css+'</style></head><body><section data-sidebar-group="training" data-active="true"><button aria-expanded="true"></button><div class="sidebar-collapse"></div></section><section data-sidebar-group="portal" data-active="false"><button aria-expanded="false"></button><div class="sidebar-collapse"></div></section><form id="curriculum-editor">'+fixture+'</form><div class="curriculum-detail-page"><details class="curriculum-section" open><summary>Section 6</summary><div class="curriculum-section-content" id="legacy-content"><table width="15000" style="width:12000pt;min-width:10000px"><colgroup width="9000"><col style="width:8000pt"></colgroup><tbody><tr><td rowspan="2" width="9000" nowrap align="right" style="width:9000pt;white-space:nowrap;text-align:right"><p style="text-align:right;width:9000px">Legacy text</p></td><td>Header</td></tr><tr><td>Other</td></tr><tr><td>Tổng cộng</td><td>100</td></tr></tbody></table></div></details><details class="curriculum-section" open id="next-section"><summary>Section 8</summary><div class="curriculum-section-content">Next section</div></details></div><pre id="result"></pre><script>localStorage.setItem("academic-sidebar:training","closed");localStorage.setItem("academic-sidebar:portal","closed");</script><script>'+tables+'</script><script>'+editor+'</script><script>'+sidebar+'</script><script>'+checks+'</script></body></html>'
file=work/'fixture.html';file.write_text(page,encoding='utf-8')
run=subprocess.run([r'C:\Program Files\Google\Chrome\Application\chrome.exe','--headless','--window-size=1440,900','--disable-gpu','--no-first-run','--no-default-browser-check','--user-data-dir='+str(work/'profile'),'--dump-dom','--virtual-time-budget=2000',file.as_uri()],capture_output=True,text=True,encoding='utf-8',timeout=45,creationflags=subprocess.CREATE_NO_WINDOW)
match=re.search(r'<pre id="result">(.*?)</pre>',run.stdout,re.S)
if not match:raise RuntimeError('No browser test output: '+run.stderr[-1000:])
result=json.loads(html.unescape(match.group(1)));print(json.dumps(result,ensure_ascii=False,indent=2));assert result['ok'],result.get('error')
