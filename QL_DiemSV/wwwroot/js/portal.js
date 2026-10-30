(() => {
'use strict';
const button=document.getElementById('portal-theme');
const update=()=>{const dark=document.documentElement.classList.contains('dark');button.textContent=dark?'☀':'☾';button.setAttribute('aria-label',dark?'Chuyển sang giao diện Sáng':'Chuyển sang giao diện Tối');};
button?.addEventListener('click',()=>{const dark=!document.documentElement.classList.contains('dark');document.documentElement.classList.toggle('dark',dark);document.documentElement.classList.toggle('light',!dark);localStorage.setItem('app-theme',dark?'dark':'light');update();});
if(button)update();
document.getElementById('portal-logout')?.addEventListener('click',async()=>{const response=await fetch('/api/auth/logout',{method:'POST'});if(response.ok)location.assign('/portal');});
})();
