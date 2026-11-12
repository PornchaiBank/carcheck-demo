import test from 'node:test';
import assert from 'node:assert/strict';
import {entryJobControls} from './entry-job-controls.js';
test('new bill can be saved but cannot be deleted before saving',()=>{const c=entryJobControls(null);assert.equal(c.saveDisabled,false);assert.equal(c.deleteDisabled,true);assert.equal(c.deletionMessage,'')});
test('existing active bill can be saved, deleted and printed',()=>{const c=entryJobControls({id:'bill-1',deleted:false});assert.equal(c.saveDisabled,false);assert.equal(c.deleteDisabled,false);assert.equal(c.printDisabled,false)});
test('deleted bill displays reason and disables save delete and print',()=>{const c=entryJobControls({id:'bill-1',deleted:true,deleteReason:'ลูกค้ายกเลิก'});assert.equal(c.saveDisabled,true);assert.equal(c.deleteDisabled,true);assert.equal(c.printDisabled,true);assert.match(c.deletionMessage,/ลูกค้ายกเลิก/)});
test('legacy deletion without a reason remains locked',()=>{const c=entryJobControls({id:'old',deleted:true});assert.equal(c.saveDisabled,true);assert.match(c.deletionMessage,/ข้อมูลเดิม/)});
test('pending request locks all bill actions',()=>{const c=entryJobControls({id:'bill-1'},true);assert.equal(c.saveDisabled,true);assert.equal(c.deleteDisabled,true);assert.equal(c.printDisabled,true)});
