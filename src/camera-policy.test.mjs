import test from 'node:test';
import assert from 'node:assert/strict';
import {isMobileDevice,cameraErrorMessage} from './camera-policy.js';

test('iPhone and Android devices use mobile camera mode',()=>{
 for(const userAgent of ['Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X)','Mozilla/5.0 (Linux; Android 15; Pixel)'])assert.equal(isMobileDevice({userAgent}),true);
});
test('iPad desktop-mode identification uses multitouch rather than viewport width',()=>{
 assert.equal(isMobileDevice({userAgent:'Mozilla/5.0 (Macintosh; Intel Mac OS X)',platform:'MacIntel',maxTouchPoints:5}),true);
 assert.equal(isMobileDevice({userAgent:'Mozilla/5.0 (Macintosh; Intel Mac OS X)',platform:'MacIntel',maxTouchPoints:0}),false);
});
test('desktop and Windows touch devices retain normal file selection',()=>{
 assert.equal(isMobileDevice({userAgent:'Windows NT 10.0',platform:'Win32',maxTouchPoints:10}),false);
 assert.equal(isMobileDevice({userAgent:'X11; Linux x86_64',maxTouchPoints:0}),false);
 assert.equal(isMobileDevice(null),false);
});
test('mobile and Android platform client hints are honored',()=>{
 assert.equal(isMobileDevice({userAgentData:{mobile:true}}),true);
 assert.equal(isMobileDevice({userAgentData:{mobile:false,platform:'Android'}}),true);
});
test('camera permission and hardware errors give actionable messages',()=>{
 assert.match(cameraErrorMessage({name:'NotAllowedError'}),/อนุญาต/);
 assert.match(cameraErrorMessage({name:'NotFoundError'}),/ไม่พบกล้อง/);
 assert.match(cameraErrorMessage({name:'NotReadableError'}),/ปิดแอป/);
 assert.equal(cameraErrorMessage(new Error('HTTPS required')),'HTTPS required');
});
