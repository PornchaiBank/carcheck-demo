import React,{useEffect,useState} from 'react';

const apiRoot='http://127.0.0.1:5188';
const welcome='* * * * * * * * * ยินดีต้อนรับผู้ใช้งานทุกท่าน * * * * * * * * *';

export default function PublicHome({onLoaded}){
 const [data,setData]=useState(null),[error,setError]=useState(''),[active,setActive]=useState(0),[paused,setPaused]=useState(false);
 useEffect(()=>{const controller=new AbortController();fetch(apiRoot+'/api/public/home',{cache:'no-store',credentials:'omit',signal:controller.signal}).then(async response=>{if(!response.ok)throw new Error(`API ตอบกลับ ${response.status}`);return response.json()}).then(value=>{setData(value);onLoaded(value)}).catch(e=>{if(e.name!=='AbortError')setError('ยังโหลดข้อมูลหน้าหลักจากฐานเดิมไม่ได้')});return()=>controller.abort();},[]);
 const announcements=data?.announcements||[];
 const links=(data?.slides||[]).filter(slide=>slide.type?.includes('I')&&slide.url);
 useEffect(()=>{if(paused||announcements.length<2)return;const timer=setInterval(()=>setActive(index=>(index+1)%announcements.length),5000);return()=>clearInterval(timer);},[paused,announcements.length]);
 const current=announcements[active];
 const currentLink=links.find(link=>String(link.id)===String(current?.id))?.url||'';
 const currentTitle=(current?.name||String(active+1)).replace(/^ประกาศที่\s*/,'');
 const move=step=>setActive(index=>(index+step+announcements.length)%announcements.length);
 const picture=current&&<img src={apiRoot+current.url} alt={current.name||`ประกาศ ${active+1}`}/>;
 return <div className="legacy-home">
  <section className={'public-hero '+(data?.hasHeaderImage?'has-image':'')}>
   {data?.hasHeaderImage&&<img className="public-hero-image" src={apiRoot+'/api/public/header-image'} alt="สถานตรวจสภาพรถ"/>}
   <div className="public-hero-glow"/><div className="public-hero-copy"><span className="public-kicker">CARCHECK · VEHICLE SERVICE</span><h2>ตรวจรถมั่นใจ<br/>พร้อมทุกการเดินทาง</h2><p>บริการตรวจสภาพรถ พ.ร.บ. และภาษีประจำปี<br/>ด้วยขั้นตอนที่ชัดเจนและข้อมูลที่ตรวจสอบได้</p></div>
   <div className="public-hero-mark" aria-hidden="true"><span>ตรอ.</span><small>สถานตรวจสภาพรถ</small></div>
  </section>
  <div className="legacy-marquee" role="status"><span>{welcome}</span></div>
  <section className="public-services" aria-label="บริการหลัก"><article><b>01</b><div><strong>ตรวจสภาพรถ</strong><span>เตรียมรถให้พร้อมตามข้อกำหนด</span></div></article><article><b>02</b><div><strong>พ.ร.บ.</strong><span>ดูแลความคุ้มครองภาคบังคับ</span></div></article><article><b>03</b><div><strong>ภาษีประจำปี</strong><span>ติดตามวันครบกำหนดได้สะดวก</span></div></article></section>
  <section id="public-news" className="public-news"><header><div><span className="public-kicker">UPDATES & ANNOUNCEMENTS</span><h2>ข่าวสารและประกาศ</h2><p>ติดตามข้อมูลสำคัญและข่าวประชาสัมพันธ์จากศูนย์บริการ</p></div><span className="slide-count">{announcements.length?String(active+1).padStart(2,'0'):'00'} <i/> {String(announcements.length).padStart(2,'0')}</span></header>{current?<div className="legacy-slider" onMouseEnter={()=>setPaused(true)} onMouseLeave={()=>setPaused(false)}><div className="news-window-bar"><span className="window-lights" aria-hidden="true"><i/><i/><i/></span><strong>CARCHECK NEWS</strong><button type="button" onClick={()=>setPaused(value=>!value)} aria-label={paused?'เล่นสไลด์ต่อ':'หยุดสไลด์ชั่วคราว'}>{paused?'▶':'Ⅱ'}</button></div><figure key={current.id||active} className="slide-reveal">{currentLink?<a href={currentLink} target="_blank" rel="noopener noreferrer" aria-label={`เปิดลิงก์ ${currentTitle}`}>{picture}</a>:picture}</figure>{announcements.length>1&&<><button type="button" className="slider-prev" onClick={()=>move(-1)} aria-label="ประกาศก่อนหน้า"><svg aria-hidden="true" viewBox="0 0 24 24"><path d="m14.5 5-7 7 7 7"/></svg></button><button type="button" className="slider-next" onClick={()=>move(1)} aria-label="ประกาศถัดไป"><svg aria-hidden="true" viewBox="0 0 24 24"><path d="m9.5 5 7 7-7 7"/></svg></button><div className="slider-dots" aria-label="เลือกประกาศ">{announcements.map((item,index)=><button type="button" key={item.id} className={index===active?'active':''} onClick={()=>setActive(index)} aria-label={`ประกาศ ${index+1}`} aria-current={index===active?'true':undefined}/>)}</div></>}</div>:<div className="attendance-empty">ยังไม่มีรูปประกาศในฐานข้อมูล</div>}</section>
  {error&&<div className="notice">{error} · กรุณาตรวจว่า Local API กำลังทำงาน</div>}
 </div>;
}
