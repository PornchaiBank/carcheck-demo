export const pagePrograms={jobs:'018',entry:'001',vehiclecopy:'019',attendance:'020',password:'002',centers:'003',staff:'004',permissions:'005',insurers:'006',types:'007',daily:'008',announcements:'009',insurance:'011',tax:'012',ry:'013',splashadmin:'014',publicadmin:'015',sms:'016',fault:'017'};
export const isAdmin=user=>String(user?.userType||'').trim().toLowerCase()==='admin';
export const canOpenPage=(id,user)=>['public','login'].includes(id)||!!user&&(id==='dash'||(user.programIds||[]).map(value=>String(value).trim()).includes(pagePrograms[id]));
