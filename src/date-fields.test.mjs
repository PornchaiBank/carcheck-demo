import test from 'node:test';
import assert from 'node:assert/strict';
import {formatDateInput,parseDateInput,dateInputMarkup} from './date-fields.js';
test('date fields display day month year even for ambiguous days',()=>{assert.equal(formatDateInput('2026-11-01'),'01/11/2026');assert.equal(formatDateInput('2026-12-31'),'31/12/2026');assert.equal(formatDateInput(''),'')});
test('day first input is converted to API ISO dates',()=>{assert.equal(parseDateInput('1/11/2026'),'2026-11-01');assert.equal(parseDateInput('31/12/2026'),'2026-12-31');assert.equal(parseDateInput(''),'')});
test('invalid dates and month-first input are rejected',()=>{for(const value of ['12/31/2026','31/02/2026','29/02/2026','00/11/2026','11/00/2026','1/1/0000'])assert.equal(parseDateInput(value),null);assert.equal(parseDateInput('29/02/2028'),'2028-02-29')});
test('display field is separate from the ISO field used by reports and filters',()=>{const html=dateInputMarkup('วันที่เริ่มต้น','2026-11-01','reportStart');assert.match(html,/value="2026-11-01"/);assert.match(html,/value="01\/11\/2026"/);assert.match(html,/placeholder="วว\/ดด\/ปปปป"/);assert.equal((html.match(/data-field="reportStart"/g)||[]).length,1)});
