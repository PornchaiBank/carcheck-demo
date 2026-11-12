export function readEntryCustomer(form){
 const field=name=>form?.querySelector(`[data-field=${name}]`),value=name=>field(name)?.value?.trim()||'';
 const selected=name=>value(name)?field(name)?.selectedOptions?.[0]?.textContent.trim()||'':'';
 return {name:value('customerName'),citizenId:value('citizenId'),phone:value('phone'),address:value('address'),province:selected('customerProvince'),amphur:selected('customerAmphur'),district:selected('customerDistrict'),zipcode:value('postcode'),amphurCode:Number(value('customerAmphur'))||null,districtCode:Number(value('customerDistrict'))||null};
}

export async function saveEntryCustomer({id,data,previous,create,update}){
 if(!data.name)throw Error('กรุณาระบุชื่อ–นามสกุลลูกค้า');
 if(!id){const customer=await create(data);if(!customer?.id)throw Error('ระบบไม่ได้รับรหัสลูกค้าใหม่');return {id:customer.id,data:{...data}}}
 const unchanged=previous?.id===id&&Object.keys(data).every(key=>data[key]===previous.data[key]);
 const storedData=previous?.id===id&&previous.storedData?Object.fromEntries(Object.keys(data).map(key=>[key,data[key]===previous.data[key]?previous.storedData[key]??data[key]:data[key]])):data;
 if(!unchanged)await update(id,storedData);
 return {id,data:{...data},storedData:{...storedData}};
}
