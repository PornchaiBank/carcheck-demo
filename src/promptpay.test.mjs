import test from 'node:test';
import assert from 'node:assert/strict';
import {promptPayPayload} from './promptpay.js';

test('PromptPay phone payload includes recipient, amount and CRC',()=>{
 assert.equal(promptPayPayload('phone','081-234-5678',100),'00020101021229370016A000000677010111011300668123456785802TH53037645406100.006304BB8A');
});

test('PromptPay rejects an invalid recipient',()=>{
 assert.throws(()=>promptPayPayload('phone','123',100),/10 หลัก/);
});
