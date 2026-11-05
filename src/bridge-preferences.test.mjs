import test from 'node:test';
import assert from 'node:assert/strict';
import {key,loadConnection,rememberConnection,forgetConnection} from './bridge-preferences.mjs';
const fakeToken='a'.repeat(64);
function memory(){const m=new Map();return {getItem:k=>m.get(k),setItem:(k,v)=>m.set(k,v),removeItem:k=>m.delete(k)};}
test('reopening restores only pairing token and selected reader',()=>{const s=memory();rememberConnection(s,fakeToken,'USB 2');assert.deepEqual(loadConnection(s),{token:fakeToken,reader:'USB 2'});assert.deepEqual(Object.keys(JSON.parse(s.getItem(key))).sort(),['reader','token','version']);rememberConnection(s,fakeToken,'USB 1');assert.equal(loadConnection(s).reader,'USB 1');});
test('forget removes stored pairing',()=>{const s=memory();rememberConnection(s,fakeToken,'USB');assert.equal(forgetConnection(s),true);assert.equal(loadConnection(s),null);});
test('unavailable browser storage does not break attendance',()=>{assert.equal(loadConnection(null),null);assert.equal(rememberConnection(null,fakeToken,'USB'),false);assert.equal(forgetConnection(null),false);});
test('invalid or outdated stored credentials are not restored',()=>{const s=memory();for(const x of ['bad JSON',JSON.stringify({version:1,token:'bad',reader:'USB'}),JSON.stringify({version:9,token:fakeToken,reader:'USB'})]){s.setItem(key,x);assert.equal(loadConnection(s),null);}});
