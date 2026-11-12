import test from 'node:test';
import assert from 'node:assert/strict';
import {readVehicleCopyIdentity,hasVehicleCopyIdentity} from './vehicle-copy-validation.js';

test('duplicate lookup only starts after all registration fields and center are present',()=>{
 const identity={centerId:'01',letterCategory:'กข',registrationNumber:'1234',province:'จังหวัดทดสอบ'};
 assert.equal(hasVehicleCopyIdentity(identity),true);
 for(const key of Object.keys(identity))assert.equal(hasVehicleCopyIdentity({...identity,[key]:'  '}),false);
 assert.equal(hasVehicleCopyIdentity(null),false);
});
test('registration identity trims input and scopes lookup to the selected center',()=>{
 const form={elements:{letterCategory:{value:' กข '},registrationNumber:{value:' 1234 '},province:{value:' จังหวัดทดสอบ '}}};
 assert.deepEqual(readVehicleCopyIdentity(form,'02',7),{centerId:'02',letterCategory:'กข',registrationNumber:'1234',province:'จังหวัดทดสอบ',excludeId:7});
});
test('new registration lookup has no excluded record',()=>{
 assert.equal(readVehicleCopyIdentity(null,'01').excludeId,null);
 assert.equal(hasVehicleCopyIdentity(readVehicleCopyIdentity(null,'01')),false);
});
