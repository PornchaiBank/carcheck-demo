export const vehicleCopyDuplicateMessage='ทะเบียนรถนี้มีคู่มือ/สำเนารถอยู่แล้วในศูนย์บริการนี้ ไม่สามารถบันทึกซ้ำได้';
export function readVehicleCopyIdentity(form,centerId,excludeId=null){
 const value=name=>form?.elements?.[name]?.value?.trim()||'';
 return {centerId,letterCategory:value('letterCategory'),registrationNumber:value('registrationNumber'),province:value('province'),excludeId};
}
export const hasVehicleCopyIdentity=value=>!!value&&['centerId','letterCategory','registrationNumber','province'].every(key=>String(value[key]||'').trim());
