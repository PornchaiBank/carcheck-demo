import test from 'node:test';
import assert from 'node:assert/strict';
import {bangkokDay,duplicate,validCard,addRecord} from './attendance-state.mjs';
test('Thai day boundary follows UTC+7',()=>{assert.equal(bangkokDay('2026-10-24T16:59:59Z'),'2026-10-24');assert.equal(bangkokDay('2026-10-24T17:00:00Z'),'2026-10-25');});
test('idempotent response and same-person same-day do not create another row',()=>{const a={id:'a',personKey:'p',day:'2026-10-25'};const rows=addRecord([],a);assert.equal(addRecord(rows,a),rows);assert.equal(addRecord(rows,{...a,id:'b'}),rows);assert.equal(duplicate(rows,'p','2026-10-25'),true);});
test('different person and next day can be recorded',()=>{const rows=[{id:'a',personKey:'p',day:'2026-10-25'}];assert.equal(addRecord(rows,{id:'b',personKey:'q',day:'2026-10-25'}).length,2);assert.equal(addRecord(rows,{id:'c',personKey:'p',day:'2026-10-26'}).length,2);});
test('empty/removal/malformed snapshots cannot be used as a card',()=>{assert.equal(validCard({present:false}),false);assert.equal(validCard({present:true,name:'x'}),false);assert.equal(validCard({present:true,readId:'r',personKey:'k',name:'TEST',maskedId:'X'}),true);});
