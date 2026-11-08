const clean=value=>String(value??'').trim();

export function createJob(values,sequence=1,now=new Date()){
  const customerName=clean(values.customerName);
  const licensePlate=clean(values.licensePlate);
  if(!customerName)throw new Error('กรุณาระบุชื่อ–นามสกุลลูกค้า');
  if(!licensePlate)throw new Error('กรุณาระบุทะเบียนรถ');
  const amountNames=['inspectionFee','act1','act2','act3','act12','otherFee','annualTax'];
  const amounts=Object.fromEntries(amountNames.map(name=>[name,Number(String(values[name]??'').replaceAll(',',''))||0]));
  const total=Object.values(amounts).reduce((sum,value)=>sum+value,0);
  return {
    id:`MEM-${String(sequence).padStart(4,'0')}`,
    createdAt:now.toISOString(),
    customerName,
    citizenId:clean(values.citizenId),
    phone:clean(values.phone),
    licensePlate,
    registrationProvince:clean(values.registrationProvince),
    vehicleType:clean(values.vehicleType),
    taxExpiry:clean(values.taxExpiry),
    insurer:clean(values.insurer),
    insuranceExpiry:clean(values.insuranceExpiry),
    note:clean(values.note),
    amounts,
    total,
    status:'รายการชั่วคราว'
  };
}

export function filterJobs(jobs,term=''){
  const query=clean(term).toLocaleLowerCase('th-TH');
  if(!query)return jobs;
  return jobs.filter(job=>[job.id,job.licensePlate,job.customerName,job.phone].some(value=>String(value??'').toLocaleLowerCase('th-TH').includes(query)));
}

export function createFault(values,jobs,sequence=1,now=new Date()){
  const jobId=clean(values.jobId);
  const job=jobs.find(item=>item.id===jobId);
  if(!job)throw new Error('กรุณาเลือกเลขเอกสารจากรายการรับงานชั่วคราว');
  const note=clean(values.note);
  if(!note)throw new Error('กรุณาระบุรายละเอียดข้อบกพร่อง');
  return {id:`FAULT-${String(sequence).padStart(3,'0')}`,jobId,note,createdAt:now.toISOString(),customerName:job.customerName,licensePlate:job.licensePlate};
}

