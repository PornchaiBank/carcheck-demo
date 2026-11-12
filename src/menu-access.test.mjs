import test from 'node:test';
import assert from 'node:assert/strict';
import {canOpenPage,pagePrograms} from './menu-access.js';
const user=(programIds=[])=>({userType:'User',programIds});
test('each missing menu has an independent permission',()=>{for(const [page,id] of [['jobs','018'],['vehiclecopy','019'],['attendance','020']]){assert.equal(canOpenPage(page,user()),false);assert.equal(canOpenPage(page,user([id])),true);assert.equal(canOpenPage(page,user(['001'])),false)}});
test('list access does not grant entry or other new menu permissions',()=>{assert.equal(canOpenPage('entry',user(['018'])),false);assert.equal(canOpenPage('vehiclecopy',user(['018'])),false);assert.equal(canOpenPage('attendance',user(['018'])),false)});
test('attendance requires permission even though it is an account menu',()=>{assert.equal(canOpenPage('attendance',user()),false);assert.equal(canOpenPage('dash',user()),true)});
test('direct page navigation and navigation menu use the same rule',()=>{assert.equal(canOpenPage('vehiclecopy',null),false);assert.equal(canOpenPage('vehiclecopy',user([' 019 '])),true);assert.equal(canOpenPage('public',null),true)});
for(const userType of ['Admin','User'])test(`${userType} must have the assigned permission for every protected menu`,()=>{
 for(const [page,program] of Object.entries(pagePrograms)){
  assert.equal(canOpenPage(page,{userType,programIds:[]}),false,`${page} denied without assignment`);
  assert.equal(canOpenPage(page,{userType,programIds:[program]}),true,`${page} granted when assigned`);
  assert.equal(canOpenPage(page,{userType,programIds:['unknown']}),false,`${page} denied with unrelated assignment`);
 }
 assert.equal(canOpenPage('dash',{userType,programIds:[]}),true);
});
