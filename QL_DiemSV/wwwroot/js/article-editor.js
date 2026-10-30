(() => {
'use strict';
const form=document.getElementById('article-form'),editor=document.getElementById('rich-editor'),html=document.getElementById('article-html');
if(!form||!editor||!html)return;
html.hidden=true;
document.querySelectorAll('[data-command]').forEach(button=>{
 button.addEventListener('mousedown',e=>e.preventDefault());
 button.addEventListener('click',()=>{editor.focus();document.execCommand(button.dataset.command,false,null);});
});
document.getElementById('editor-link').addEventListener('mousedown',e=>e.preventDefault());
document.getElementById('editor-link').addEventListener('click',()=>{const url=prompt('Nhập liên kết https:// hoặc đường dẫn /...');if(!url)return;if(!/^https?:\/\//i.test(url)&&!/^\/(?![\/\\])/.test(url)){alert('Liên kết không hợp lệ.');return;}editor.focus();document.execCommand('createLink',false,url);});
editor.addEventListener('paste',event=>{event.preventDefault();document.execCommand('insertText',false,event.clipboardData.getData('text/plain'));});
form.addEventListener('submit',event=>{html.value=editor.innerHTML;if(!editor.textContent.trim()){event.preventDefault();editor.focus();alert('Vui lòng nhập nội dung bài viết.');}});
})();
