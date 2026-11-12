import {promptPayPayload} from './promptpay.js';
export function receiptPaymentAmount(container){
 const fields=['inspectionFee','act1','act2','act3','act12','otherFee','annualTax'];
 const satang=fields.reduce((sum,key)=>{const value=Number(String(container?.querySelector(`[data-field=${key}]`)?.value||'').replaceAll(',',''));return sum+(Number.isFinite(value)?Math.round(value*100):0)},0);
 return satang/100;
}
export async function receiptPaymentQr(center,{amount,fetchImage,generate}){
 if(center?.paymentQrMode==='none')return '';
 if(center?.paymentQrMode==='image')return await fetchImage(center.id);
 if(center?.promptPayType&&center?.promptPayNumber&&Number.isFinite(Number(amount))&&Number(amount)>0)return await generate(promptPayPayload(center.promptPayType,center.promptPayNumber,amount));
 return '';
}
