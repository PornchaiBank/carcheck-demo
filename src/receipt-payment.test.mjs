import test from 'node:test';
import assert from 'node:assert/strict';
import {receiptPaymentQr,receiptPaymentAmount} from './receipt-payment.js';
import {buildReport} from './reports.js';

test('number mode generates dynamic PromptPay with the bill amount',async()=>{
 let payload;
 const result=await receiptPaymentQr({id:'02',paymentQrMode:'number',promptPayType:'phone',promptPayNumber:'0812345678'},{amount:840.5,fetchImage:()=>assert.fail(),generate:async value=>{payload=value;return 'generated-qr'}});
 assert.equal(result,'generated-qr');assert.equal(payload.slice(6,12),'010212');
 let offset=0;const tags={};while(offset<payload.length){const tag=payload.slice(offset,offset+2),length=Number(payload.slice(offset+2,offset+4));tags[tag]=payload.slice(offset+4,offset+4+length);offset+=4+length}
 assert.equal(tags['54'],'840.50');
});
test('image mode uses the uploaded QR unchanged even when a PromptPay number exists',async()=>{
 const image='data:image/png;base64,example';let requested;
 const result=await receiptPaymentQr({id:'11',paymentQrMode:'image',promptPayType:'phone',promptPayNumber:'0812345678'},{amount:840.5,fetchImage:async id=>{requested=id;return image},generate:()=>assert.fail('must not regenerate uploaded image')});
 assert.equal(requested,'11');assert.equal(result,image);
});
test('existing number configuration remains supported and unconfigured centers omit QR',async()=>{
 assert.equal(await receiptPaymentQr({promptPayType:'phone',promptPayNumber:'0812345678'},{amount:100,generate:async()=> 'qr'}),'qr');
 assert.equal(await receiptPaymentQr({},{}),'');
});
test('uploaded-image failure is reported instead of falling back to a different recipient',async()=>{
 await assert.rejects(receiptPaymentQr({id:'1',paymentQrMode:'image'},{fetchImage:async()=>{throw Error('unavailable')},generate:()=>assert.fail()}),/unavailable/);
});
test('receipt includes the same QR on both copies without an amount next to QR',()=>{
 const root={querySelector:selector=>selector==='[data-receipt-center]'?{dataset:{receiptCenter:'ศูนย์',promptPayQr:'data:image/png;base64,test'}}:null};
 const {html}=buildReport('entry',root);assert.equal((html.match(/src="data:image\/png;base64,test"/g)||[]).length,2);assert.equal(html.includes('กรุณาระบุยอดชำระในแอปธนาคาร'),true);assert.equal(html.includes('<span>ยอด '),false);
});

test('bill amount includes all fees and tax, with formatted numbers and satang rounding',()=>{
 const values={inspectionFee:'200.00',act1:'640.00',act2:'1,000.10',act3:'0.10',act12:'0.20',otherFee:'107.25',annualTax:'900.35'};
 const form={querySelector:selector=>({value:values[selector.match(/data-field=(.*)\]/)?.[1]]||''})};
 assert.equal(receiptPaymentAmount(form),2848);
});

test('number-mode receipt displays the matching total on both copies',()=>{
 const form={querySelector:selector=>selector==='[data-receipt-center]'?{dataset:{promptPayMode:'number',promptPayQr:'data:image/png;base64,test'}}:{value:selector==='[data-field=inspectionFee]'?'200.00':selector==='[data-field=act1]'?'640.50':''}};
 const {html}=buildReport('entry',form);
 assert.equal((html.match(/<span>ยอด 840\.50 บาท<\/span>/g)||[]).length,2);
 assert.equal(html.includes('กรุณาระบุยอดชำระในแอปธนาคาร'),false);
});

test('zero or invalid bill totals do not produce a number-mode QR with an unspecified amount',async()=>{
 for(const amount of [0,-1,NaN,undefined])assert.equal(await receiptPaymentQr({promptPayType:'phone',promptPayNumber:'0812345678'},{amount,generate:()=>assert.fail()}),'');
});

test('disabled QR mode ignores previously saved numbers and images',async()=>{
 const center={id:'02',paymentQrMode:'none',promptPayType:'phone',promptPayNumber:'0812345678',hasPaymentQrImage:true};
 assert.equal(await receiptPaymentQr(center,{amount:840,fetchImage:()=>assert.fail('must not load saved image'),generate:()=>assert.fail('must not generate QR')}),'');
});

test('receipt prints the entry note on both copies before payment and receiver sections',()=>{
 const form={querySelector:selector=>selector==='[data-field=note]'?{value:'หมายเหตุสำหรับทดสอบ\nบรรทัดที่สอง'}:null};
 const {html}=buildReport('entry',form);
 assert.equal((html.match(/class="receipt-note"/g)||[]).length,2);
 assert.equal((html.match(/หมายเหตุสำหรับทดสอบ\nบรรทัดที่สอง/g)||[]).length,2);
 assert.ok(html.indexOf('class="receipt-note"')<html.indexOf('class="receiver"'));
});
test('receipt escapes note markup as plain text',()=>{
 const form={querySelector:selector=>selector==='[data-field=note]'?{value:'<script>bad()</script> & "test"'}:null};
 const {html}=buildReport('entry',form);
 assert.equal(html.includes('<script>bad()'),false);
 assert.equal((html.match(/&lt;script&gt;bad\(\)&lt;\/script&gt; &amp; &quot;test&quot;/g)||[]).length,2);
});
test('receipt omits an empty note section',()=>{
 const form={querySelector:selector=>selector==='[data-field=note]'?{value:'   '}:null};
 assert.equal(buildReport('entry',form).html.includes('class="receipt-note"'),false);
});
