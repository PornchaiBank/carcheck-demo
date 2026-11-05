export const bangkokDay=iso=>new Intl.DateTimeFormat('en-CA',{timeZone:'Asia/Bangkok',year:'numeric',month:'2-digit',day:'2-digit'}).format(new Date(iso));
export const duplicate=(records,personKey,day)=>records.some(r=>r.personKey===personKey&&r.day===day);
export function addRecord(records,row){return records.some(r=>r.id===row.id)||duplicate(records,row.personKey,row.day)?records:[row,...records];}
export function validCard(x){return x?.present===true&&typeof x.readId==='string'&&typeof x.personKey==='string'&&typeof x.name==='string'&&typeof x.maskedId==='string';}
