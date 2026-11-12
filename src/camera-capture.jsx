import React,{useEffect,useId,useRef,useState} from 'react';
import {createPortal} from 'react-dom';
import {cameraErrorMessage} from './camera-policy.js';

export async function capturePhoto(video){
 if(!video?.videoWidth||!video?.videoHeight)throw Error('กล้องยังไม่พร้อม กรุณารอสักครู่');
 const scale=Math.min(1,2048/Math.max(video.videoWidth,video.videoHeight)),canvas=document.createElement('canvas');
 canvas.width=Math.round(video.videoWidth*scale);canvas.height=Math.round(video.videoHeight*scale);
 const context=canvas.getContext('2d');if(!context)throw Error('ไม่สามารถสร้างรูปภาพได้');
 context.drawImage(video,0,0,canvas.width,canvas.height);
 const blob=await new Promise(resolve=>canvas.toBlob(resolve,'image/jpeg',.9));
 if(!blob)throw Error('ไม่สามารถสร้างรูปภาพได้');
 if(blob.size>5_242_880)throw Error('รูปถ่ายมีขนาดเกิน 5 MB กรุณาถ่ายใหม่');
 return new File([blob],`vehicle-photo-${Date.now()}.jpg`,{type:'image/jpeg'});
}

export default function CameraCapture({onCapture,onClose}){
 const title=useId(),video=useRef(null),stream=useRef(null),close=useRef(null),alive=useRef(true);
 const [ready,setReady]=useState(false),[busy,setBusy]=useState(false),[error,setError]=useState(''),[attempt,setAttempt]=useState(0);
 useEffect(()=>{alive.current=true;close.current?.focus();return()=>{alive.current=false}},[]);
 useEffect(()=>{
  let active=true;setReady(false);setError('');
  const stop=()=>{stream.current?.getTracks().forEach(track=>track.stop());stream.current=null;if(video.current)video.current.srcObject=null};
  async function start(){try{
   if(!window.isSecureContext)throw Error('กรุณาเปิดเว็บไซต์ผ่าน HTTPS เพื่อใช้งานกล้อง');
   if(!navigator.mediaDevices?.getUserMedia)throw Error('เบราว์เซอร์นี้ไม่รองรับกล้อง กรุณาเปิดด้วย Safari หรือ Chrome รุ่นปัจจุบัน');
   const media=await navigator.mediaDevices.getUserMedia({audio:false,video:{facingMode:{ideal:'environment'},width:{ideal:1920},height:{ideal:1440}}});
   if(!active){media.getTracks().forEach(track=>track.stop());return}
   stream.current=media;video.current.srcObject=media;await video.current.play();
   if(active)setReady(video.current.videoWidth>0);
  }catch(cause){if(active){stop();setReady(false);setError(cameraErrorMessage(cause))}}}
  const key=event=>{if(event.key==='Escape'){event.preventDefault();event.stopPropagation();onClose()}};
  const hidden=()=>{if(document.visibilityState==='hidden'){stop();onClose()}};
  document.addEventListener('keydown',key,true);document.addEventListener('visibilitychange',hidden);start();
  return()=>{active=false;stop();document.removeEventListener('keydown',key,true);document.removeEventListener('visibilitychange',hidden)};
 },[attempt]);
 async function takePhoto(){if(busy||!ready)return;setBusy(true);try{const file=await capturePhoto(video.current);if(alive.current)onCapture(file)}catch(cause){if(alive.current)setError(cameraErrorMessage(cause))}finally{if(alive.current)setBusy(false)}}
 return createPortal(<div className="camera-capture-backdrop" onClick={event=>{event.stopPropagation();if(!busy)onClose()}}><section className="camera-capture-modal" role="dialog" aria-modal="true" aria-labelledby={title} onClick={event=>event.stopPropagation()}><header><div><h2 id={title}>ถ่ายรูปคู่มือ / สำเนารถ</h2><p>จัดเอกสารให้อยู่ในกรอบและเห็นข้อความชัดเจน</p></div><button ref={close} type="button" className="camera-close" aria-label="ปิดกล้อง" onClick={onClose}>×</button></header><div className="camera-view"><video ref={video} autoPlay playsInline muted onLoadedData={()=>setReady(true)}/>{!ready&&!error&&<span className="camera-loading" role="status">กำลังเปิดกล้อง…</span>}</div>{error&&<p className="camera-capture-error" role="alert">{error}</p>}<footer><button type="button" className="secondary" onClick={onClose}>ยกเลิก</button>{error&&<button type="button" className="secondary" onClick={()=>setAttempt(value=>value+1)}>ลองเปิดกล้องอีกครั้ง</button>}<button type="button" className="action camera-shutter" disabled={!ready||busy} onClick={takePhoto}>{busy?'กำลังสร้างรูป…':'ถ่ายรูป'}</button></footer></section></div>,document.querySelector('#tor-design')||document.body);
}
