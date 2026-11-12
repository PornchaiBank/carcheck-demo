export const formatDateInput=value=>/^\d{4}-\d{2}-\d{2}$/.test(value||'')?value.split('-').reverse().join('/'):'';
export function parseDateInput(value){
 const text=String(value||'').trim();if(!text)return '';
 const match=/^(\d{1,2})\/(\d{1,2})\/(\d{4})$/.exec(text);if(!match)return null;
 const [,day,month,year]=match;if(Number(year)<1)return null;const iso=`${year}-${month.padStart(2,'0')}-${day.padStart(2,'0')}`,date=new Date(`${iso}T00:00:00Z`);
 return !Number.isNaN(date.valueOf())&&date.toISOString().slice(0,10)===iso?iso:null;
}
const escape=value=>String(value??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
export function dateInputMarkup(name,value='',key='',disabled=false){return `<span class="date-input-wrap date-standard"><input type="date" lang="en-GB" tabindex="-1" aria-label="ปฏิทิน ${escape(name)}" value="${escape(value)}" autocomplete="off" data-label="${escape(name)}"${key?` data-field="${escape(key)}"`:''}${disabled?' disabled':''}><input type="text" class="date-text" data-date-text inputmode="numeric" placeholder="วว/ดด/ปปปป" aria-label="${escape(name)} (วัน/เดือน/ปี)" autocomplete="off" value="${formatDateInput(value)}"${disabled?' disabled':''}><button type="button" class="date-calendar-button" data-action="open-date-picker" aria-label="เลือก${escape(name)}"${disabled?' disabled':''}><svg viewBox="0 0 24 24" aria-hidden="true"><rect x="3" y="5" width="18" height="16" rx="2"/><path d="M16 3v4M8 3v4M3 11h18"/></svg></button>${disabled?'':`<button type="button" class="clear-date" data-action="clear-date" title="ล้าง${escape(name)}" aria-label="ล้าง${escape(name)}"><svg viewBox="0 0 20 20" aria-hidden="true"><path d="M6 6l8 8M14 6l-8 8"/></svg></button>`}</span>`}

export function refreshDateFields(root=document){
 root.querySelectorAll('.date-standard').forEach(wrap=>{
  const native=wrap.querySelector('input[type=date]'),text=wrap.querySelector('[data-date-text]');if(!native||!text)return;
  if(document.activeElement!==text&&!text.validity.customError){text.value=formatDateInput(native.value);text.setCustomValidity('')}
  if(text.disabled!==native.disabled)text.disabled=native.disabled;
  const calendar=wrap.querySelector('[data-action=open-date-picker]');if(calendar&&calendar.disabled!==native.disabled)calendar.disabled=native.disabled;
 });
}
export function installDateFields(root=document){
 let frame;
 const schedule=()=>{cancelAnimationFrame(frame);frame=requestAnimationFrame(()=>refreshDateFields(root))};
 const commit=text=>{
  const native=text.closest('.date-standard')?.querySelector('input[type=date]');if(!native||native.disabled)return;
  const iso=parseDateInput(text.value);if(iso===null){text.setCustomValidity('กรุณาระบุวันที่เป็น วัน/เดือน/ปี เช่น 31/12/2026');text.reportValidity();return false}
  text.setCustomValidity('');text.value=formatDateInput(iso);
  if(native.value!==iso){Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(native,iso);native.dispatchEvent(new Event('input',{bubbles:true}));native.dispatchEvent(new Event('change',{bubbles:true}))}
  return true;
 };
 const click=event=>{const button=event.target.closest?.('button');if(button?.matches('.action,[data-action=report-preview]')){const invalid=[...root.querySelectorAll('[data-date-text]')].find(input=>!input.disabled&&parseDateInput(input.value)===null);if(invalid){event.preventDefault();event.stopPropagation();commit(invalid);return}}if(button?.matches('[data-action=open-date-picker]')){event.preventDefault();event.stopPropagation();const native=button.closest('.date-standard')?.querySelector('input[type=date]');if(native&&!native.disabled){if(native.showPicker)native.showPicker();else{native.focus();native.click()}}}schedule()};
 const change=event=>{if(event.target.matches?.('[data-date-text]'))commit(event.target);else if(event.target.matches?.('input[type=date]')){const text=event.target.closest('.date-standard')?.querySelector('[data-date-text]');if(text){text.value=formatDateInput(event.target.value);text.setCustomValidity('')}}schedule()};
 const key=event=>{if(event.key==='Enter'&&event.target.matches?.('[data-date-text]')){event.preventDefault();commit(event.target)}schedule()};
 root.addEventListener('click',click,true);root.addEventListener('change',change,true);root.addEventListener('focusout',change,true);root.addEventListener('keydown',key,true);
 const observer=new MutationObserver(schedule);observer.observe(root.documentElement||root,{childList:true,subtree:true});refreshDateFields(root);
 return()=>{cancelAnimationFrame(frame);observer.disconnect();root.removeEventListener('click',click,true);root.removeEventListener('change',change,true);root.removeEventListener('focusout',change,true);root.removeEventListener('keydown',key,true)};
}
