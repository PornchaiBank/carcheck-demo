import test from 'node:test';
import assert from 'node:assert/strict';
import {content} from './content.js';

const state=count=>({user:{userType:'User'},database:{centerId:'01',centers:[{id:'01',name:'ศูนย์หนึ่ง'},{id:'02',name:'ศูนย์สอง'}].slice(0,count),jobs:[],faults:[]}});
test('employee with multiple allowed centers can switch center',()=>{const html=content('jobs',state(2));assert.match(html,/<select data-field="jobCenter">/);assert.match(html,/ศูนย์สอง/)});
test('employee with one allowed center cannot switch center',()=>{const html=content('jobs',state(1));assert.match(html,/<select data-field="jobCenter" disabled>/);assert.doesNotMatch(html,/ศูนย์สอง/)});
test('multiple center access does not unlock daily report dates for users',()=>{const html=content('daily',state(2));assert.match(html,/data-field="reportStart" disabled/);assert.match(html,/data-field="reportEnd" disabled/)});

test('admin center selector only contains centers returned by access checks',()=>{const data=state(1);data.user.userType='Admin';const html=content('jobs',data);assert.match(html,/ศูนย์หนึ่ง/);assert.doesNotMatch(html,/ศูนย์สอง/)});
test('staff table displays the primary center instead of the allowed center list',()=>{const data=state(2);data.staffRecords=[{id:'00002',name:'Employee',centerId:'01',allowedCenterIds:['01','02'],userType:'Admin',active:true}];const html=content('staff',data);assert.match(html,/<th>ศูนย์บริการประจำ<\/th>/);assert.match(html,/<td>01<\/td><td>Admin<\/td>/);assert.doesNotMatch(html,/01, 02|ศูนย์ที่เข้าถึงได้/);assert.deepEqual(data.staffRecords[0].allowedCenterIds,['01','02'])});

for(const userType of ['Admin','User'])test(`${userType}: all report and work center dropdowns follow the allowed center list`,()=>{
 for(const [page,field] of [['jobs','jobCenter'],['fault','faultCenter'],['daily','reportCenter'],['insurance','insuranceCenter'],['tax','taxCenter'],['ry','ryCenter'],['sms','smsCenter']]){
  for(const count of [1,2]){
   const data=state(count);data.user.userType=userType;
   const html=content(page,data),select=html.match(new RegExp(`<select data-field="${field}"([^>]*)>([\\s\\S]*?)</select>`));
   assert.ok(select,`${page} center dropdown exists`);
   assert.equal(select[1].includes('disabled'),count===1,`${page} switch availability`);
   assert.match(select[2],/ศูนย์หนึ่ง/);
   assert.equal(select[2].includes('ศูนย์สอง'),count===2,`${page} allowed centers`);
   assert.match(select[2],/value="01"\s*selected/);
  }
 }
});
