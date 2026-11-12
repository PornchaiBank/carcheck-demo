export function isMobileDevice(device=globalThis.navigator){
 if(!device)return false;
 return device.userAgentData?.mobile===true||device.userAgentData?.platform==='Android'||/Android|iPhone|iPad|iPod/i.test(device.userAgent||'')||(/MacIntel|Macintosh/i.test(`${device.platform||''} ${device.userAgent||''}`)&&Number(device.maxTouchPoints)>1);
}

export function cameraErrorMessage(error){
 if(['NotAllowedError','PermissionDeniedError','SecurityError'].includes(error?.name))return 'กรุณาอนุญาตให้เว็บไซต์ใช้กล้อง แล้วลองอีกครั้ง';
 if(['NotFoundError','DevicesNotFoundError'].includes(error?.name))return 'ไม่พบกล้องบนอุปกรณ์นี้';
 if(['NotReadableError','TrackStartError'].includes(error?.name))return 'เปิดกล้องไม่ได้ กรุณาปิดแอปที่ใช้กล้องแล้วลองอีกครั้ง';
 return error?.message||'ไม่สามารถเปิดกล้องได้ กรุณาลองใหม่';
}
