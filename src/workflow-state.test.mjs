import test from 'node:test';
import assert from 'node:assert/strict';
import {createJob,filterJobs,createFault} from './workflow-state.mjs';

test('createJob validates required fields and totals numeric amounts',()=>{
  assert.throws(()=>createJob({customerName:'',licensePlate:'กข 1234'}),/ชื่อ/);
  const job=createJob({customerName:' ลูกค้าทดสอบ ',licensePlate:' กข 1234 ',inspectionFee:'1,200',annualTax:'300'},7,new Date('2026-11-05T01:00:00Z'));
  assert.equal(job.id,'MEM-0007');
  assert.equal(job.total,1500);
  assert.equal(job.customerName,'ลูกค้าทดสอบ');
});

test('filterJobs searches document, plate and customer',()=>{
  const jobs=[createJob({customerName:'สมชาย ใจดี',licensePlate:'กข 1234'},1)];
  assert.equal(filterJobs(jobs,'สมชาย').length,1);
  assert.equal(filterJobs(jobs,'ไม่พบ').length,0);
});

test('createFault must reference an existing in-memory job',()=>{
  const jobs=[createJob({customerName:'สมชาย',licensePlate:'กข 1234'},1)];
  assert.throws(()=>createFault({jobId:'MEM-9999',note:'ไฟหน้าเสีย'},jobs),/เลือกเลขเอกสาร/);
  assert.equal(createFault({jobId:'MEM-0001',note:'ไฟหน้าเสีย'},jobs).licensePlate,'กข 1234');
});
