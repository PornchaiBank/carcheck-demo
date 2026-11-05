const escapeHtml=value=>String(value??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const formatDate=value=>/^\d{4}-\d{2}-\d{2}$/.test(value)?value.split('-').reverse().join('/'):value;
export const reportNames={entry:'รายละเอียดการรับบริการ',daily:'รายงานประจำวัน',insurance:'รายงานประกันใกล้หมดอายุ',tax:'รายงานภาษีใกล้หมดอายุ',ry:'รายงานสรุปตามประเภท รย.'};
export function buildReport(kind,container){
 const title=reportNames[kind];if(!title)throw Error('ไม่พบรูปแบบรายงาน');
 const fields=[...container.querySelectorAll('input:not([type=checkbox]),textarea')].map(e=>({name:e.dataset.label||e.closest('label')?.firstChild?.textContent||'ข้อมูล',value:e.value.trim(),type:e.type}));
 const dates=fields.filter(f=>f.type==='date'&&f.value);
 if(kind!=='entry'&&dates.length===2&&dates[0].value>dates[1].value)throw Error('วันที่เริ่มต้นต้องไม่เกินวันที่สิ้นสุด');
 const e=escapeHtml;const fieldRow=f=>`<tr><th scope="row">${e(f.name)}</th><td>${e(formatDate(f.value)||'—')}</td></tr>`;
 let body='',summary='';
 if(kind==='entry'){
  const groups=[['ข้อมูลลูกค้า',fields.slice(0,10)],['ข้อมูลรถและความคุ้มครอง',fields.slice(10,17)],['รายละเอียดค่าใช้จ่ายและหมายเหตุ',fields.slice(17)]];
  body=groups.map(([heading,items])=>`<section><h2>${e(heading)}</h2><table class="details"><tbody>${items.map(fieldRow).join('')}</tbody></table></section>`).join('');
  summary='จำนวนเงินแสดงตามที่กรอก · ยังไม่คำนวณยอดสุทธิจนกว่าจะยืนยันสูตรเดิม';
 }else{
  const table=container.querySelector('table');const headings=[...table.querySelectorAll('thead th')].map(x=>x.textContent.trim());const rows=[...table.querySelectorAll('tbody tr')].filter(r=>!r.hidden).map(r=>[...r.cells].map(c=>c.textContent.trim()));
  body=`<div class="criteria">${fields.map(f=>`<span><b>${e(f.name)}:</b> ${e(formatDate(f.value)||'ไม่ระบุ')}</span>`).join('')}</div><table class="report-table"><thead><tr>${headings.map(h=>`<th scope="col">${e(h)}</th>`).join('')}</tr></thead><tbody>${rows.length?rows.map(r=>`<tr>${r.map((c,i)=>`<td class="${kind==='ry'&&i>0?'number':''}">${e(c)}</td>`).join('')}</tr>`).join(''):`<tr><td colspan="${headings.length}" class="empty">ไม่พบรายการ</td></tr>`}</tbody></table>`;
  summary=`รวม ${rows.length} รายการ`;
  if(kind==='ry'){
   const quantity=rows.reduce((a,r)=>a+Number(r[1].replaceAll(',','')),0);const amount=rows.reduce((a,r)=>a+Number(r[2].replaceAll(',','')),0);
   summary=`รวม ${quantity.toLocaleString('th-TH')} รายการ · ยอดเงิน ${amount.toLocaleString('th-TH',{minimumFractionDigits:2,maximumFractionDigits:2})} บาท`;
  }
 }
 const orientation=kind==='entry'?'portrait':'landscape';
 return {title,orientation,html:`<!doctype html><html lang="th"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>${e(title)}</title><style>
 @font-face{font-family:Sarabun;src:url('./Sarabun.ttf')}*{box-sizing:border-box}body{margin:0;background:#e7edf3;color:#111;font:14px/1.5 Sarabun,Tahoma,sans-serif}.paper{margin:24px auto;padding:12mm;background:#fff;max-width:${orientation==='portrait'?'210':'297'}mm;min-height:${orientation==='portrait'?'297':'210'}mm;box-shadow:0 4px 24px #18364c20}.report-header{text-align:center;border-bottom:2px solid #222;padding-bottom:14px;margin-bottom:20px}.report-header h1{font-size:23px;margin:0 0 6px}.report-header p{margin:3px 0}.sample{display:inline-block;border:1px solid #777;padding:3px 10px;font-size:12px;color:#444}.criteria{display:grid;grid-template-columns:1fr 1fr;gap:8px 20px;margin-bottom:18px}table{border-collapse:collapse;width:100%;table-layout:fixed}th,td{border:1px solid #777;padding:8px 9px;text-align:left;vertical-align:top;overflow-wrap:anywhere}thead{display:table-header-group}thead th{background:#eee;text-align:center}tr{break-inside:avoid}h2{font-size:16px;margin:18px 0 8px;break-after:avoid}.details th{width:42%;font-weight:400;background:#f6f6f6}.details td{white-space:pre-wrap}.details th,.details td{padding:4px 8px}.number{text-align:right;font-variant-numeric:tabular-nums}.empty{text-align:center}.summary{text-align:right;font-weight:bold;border-top:2px solid #222;margin-top:16px;padding-top:10px;break-inside:avoid}.report-footer{margin-top:24px;font-size:11px;color:#555;border-top:1px solid #aaa;padding-top:8px}.report-footer p{margin:3px 0}.print-time{text-align:right;font-size:11px;color:#555;margin-bottom:8px}@page{size:A4 ${orientation};margin:12mm}@media print{html,body{background:white;margin:0;padding:0}.paper{max-width:none;min-height:0;margin:0;padding:0;box-shadow:none}body{font-size:11px}.report-header{margin-bottom:12px}.report-header h1{font-size:20px}th,td{padding:6px 7px}section{break-inside:auto}h2{margin-top:12px}.criteria{font-size:11px}a{color:inherit}}@media screen and (max-width:600px){.paper{margin:0;padding:18px;min-width:${orientation==='landscape'?'720':'340'}px}}
 </style></head><body><article class="paper"><header class="report-header"><h1>${e(title)}</h1><p>CARCHECK · ระบบบริหารงาน ตรอ.</p><span class="sample">ข้อมูลตัวอย่าง — แบบจัดวางรอเทียบรายงานเดิม</span></header><div class="print-time">วันที่แสดงรายงาน ${e(new Date().toLocaleDateString('th-TH'))}</div>${body}<div class="summary">${e(summary)}</div><footer class="report-footer"><p>รายงานนี้จัดทำจากข้อมูลใน Web Demo ไม่ใช่ข้อมูลจากระบบจริง</p><p>โครงสร้างและขนาดกระดาษเป็นแบบร่าง ยังไม่ได้ยืนยันกับต้นฉบับรายงานเดิม</p></footer></article></body></html>`};
}
