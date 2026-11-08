import React,{useEffect,useState} from 'react';

export default function DashboardAnnouncements({messages=[],images=[]}){
 const slides=images.filter(image=>image.hasImage&&image.previewUrl),[active,setActive]=useState(0),[paused,setPaused]=useState(false);
 useEffect(()=>{if(active>=slides.length)setActive(0)},[active,slides.length]);
 useEffect(()=>{if(paused||slides.length<2)return;const timer=setInterval(()=>setActive(index=>(index+1)%slides.length),5000);return()=>clearInterval(timer)},[paused,slides.length]);
 const move=step=>setActive(index=>(index+step+slides.length)%slides.length),current=slides[active];
 return <div className="dash-content"><div className="dash-marquee" role="status"><span>{messages.length?messages.join('   •   '):'ยินดีต้อนรับเข้าสู่ระบบบริหารงาน ตรอ.'}</span></div><section className="panel dash-announcement-panel">{current?<div className="dash-announcement-slider" onMouseEnter={()=>setPaused(true)} onMouseLeave={()=>setPaused(false)}><img key={current.id} src={current.previewUrl} alt={current.name||`ประกาศภายใน ${active+1}`}/>{slides.length>1&&<><button type="button" className="dash-slide-prev" onClick={()=>move(-1)} aria-label="ประกาศก่อนหน้า">‹</button><button type="button" className="dash-slide-next" onClick={()=>move(1)} aria-label="ประกาศถัดไป">›</button><div className="dash-slide-dots">{slides.map((slide,index)=><button type="button" key={slide.id} className={index===active?'active':''} onClick={()=>setActive(index)} aria-label={`ประกาศ ${index+1}`}/>)}</div></>}</div>:<div className="attendance-empty">ยังไม่มีรูปประกาศภายใน</div>}</section></div>;
}
