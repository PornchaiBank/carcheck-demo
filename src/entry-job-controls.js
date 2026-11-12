export function entryJobControls(row,busy=false){
 const deleted=!!row?.deleted;
 return {
  saveDisabled:deleted||busy,
  deleteDisabled:deleted||busy||!row?.id,
  printDisabled:deleted||busy,
  deletionMessage:deleted?`รายการนี้ถูกลบแล้ว · สาเหตุ: ${row.deleteReason||'ไม่ระบุสาเหตุ (ข้อมูลเดิม)'}`:''
 };
}
