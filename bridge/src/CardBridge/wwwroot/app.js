'use strict';
const token=location.hash.slice(1);
history.replaceState(null,'',location.pathname);
const el=id=>document.getElementById(id);
let timer;
function clear(){el('name').textContent='—';el('cid').textContent='—';clearTimeout(timer);}
async function api(path,body){
 if(!token) throw new Error('กรุณาปิดหน้านี้ แล้วเปิดจากลิงก์ในหน้าต่างโปรแกรมอีกครั้ง');
 const response=await fetch(path,{method:body?'POST':'GET',headers:{'X-Bridge-Token':token,...(body?{'Content-Type':'application/json'}:{})},body:body?JSON.stringify(body):undefined,cache:'no-store',signal:AbortSignal.timeout(15000)});
 if(response.status===401) throw new Error('การเชื่อมต่อหมดอายุ กรุณาเปิดลิงก์จากโปรแกรมอีกครั้ง');
 const data=await response.json();if(!response.ok)throw new Error(data.error||'เชื่อมต่อไม่สำเร็จ');return data;
}
async function readers(){clear();el('read').disabled=true;el('refresh').disabled=true;el('readers').replaceChildren();try{const data=await api('/api/readers');for(const value of data.readers){const option=document.createElement('option');option.value=value;option.textContent=value;el('readers').append(option);}el('status').textContent=data.readers.length?'พบเครื่องอ่านแล้ว เสียบบัตรและกด “อ่านบัตรประชาชน”':'ไม่พบเครื่องอ่าน ตรวจ USB และ Windows Smart Card service';el('read').disabled=!data.readers.length;}catch(e){el('status').textContent=e.message;}finally{el('refresh').disabled=false;}}
el('read').onclick=async()=>{clear();el('read').disabled=true;el('refresh').disabled=true;el('status').textContent='กำลังอ่านบัตร…';try{const data=await api('/api/read',{reader:el('readers').value});if(document.hidden)return;el('name').textContent=data.thaiName;el('cid').textContent=data.citizenId;el('status').textContent='อ่านสำเร็จ • ไม่มีการบันทึกข้อมูล';timer=setTimeout(()=>{clear();el('status').textContent='ล้างข้อมูลหลังครบ 60 วินาทีแล้ว';},60000);}catch(e){el('status').textContent=e.name==='TimeoutError'?'เครื่องอ่านไม่ตอบกลับใน 15 วินาที ลองปิดโปรแกรมที่ใช้งานบัตรอื่น หรือเปิด Bridge ใหม่':e.message;}finally{el('read').disabled=false;el('refresh').disabled=false;}};
el('refresh').onclick=readers;el('clear').onclick=clear;el('readers').onchange=clear;document.addEventListener('visibilitychange',()=>{if(document.hidden)clear();});window.addEventListener('pagehide',clear);readers();

el('pair').onclick=async()=>{try{await navigator.clipboard.writeText(token);el('status').textContent='คัดลอกรหัสแล้ว นำไปวางในช่องรหัสเชื่อมต่อของเว็บ CARCHECK';}catch{el('status').textContent='คัดลอกไม่ได้ ใช้รหัสหลัง # จากหน้าต่างโปรแกรม ห้ามส่งรหัสในแชต';}};
