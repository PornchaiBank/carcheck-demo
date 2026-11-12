import React,{useEffect,useId,useRef,useState} from 'react';
import {isMobileDevice} from './camera-policy.js';
import CameraCapture from './camera-capture.jsx';

export default function ImageUpload({name,label,existingImage='',required=false,disabled=false,onPreview,cameraOnMobile=false}){
 const id=useId(),input=useRef(null),browse=useRef(null);
 const [file,setFile]=useState(null),[preview,setPreview]=useState(''),[dragging,setDragging]=useState(false),[error,setError]=useState('');
 const [cameraOnly]=useState(()=>cameraOnMobile&&isMobileDevice()),[cameraOpen,setCameraOpen]=useState(false);
 useEffect(()=>{if(!file){setPreview('');return}const url=URL.createObjectURL(file);setPreview(url);return()=>URL.revokeObjectURL(url)},[file]);
 function assignFile(value){const transfer=new DataTransfer();if(value)transfer.items.add(value);input.current.files=transfer.files}
 function choose(files,captured=false){
  if(disabled)return;
  if(cameraOnly&&!captured){assignFile(file);setError('กรุณาถ่ายรูปผ่านกล้องเท่านั้น');return}
  const next=files?.[0];if(!next)return;
  const message=files.length>1?'แนบรูปได้ครั้งละ 1 รูป':!['image/jpeg','image/png','image/gif'].includes(next.type)?'กรุณาเลือกไฟล์ JPG, PNG หรือ GIF':next.size>5_242_880?'รูปภาพต้องมีขนาดไม่เกิน 5 MB':'';
  if(message){assignFile(file);setError(message);return}
  assignFile(next);setFile(next);setError('');
 }
 const image=preview||existingImage;
 return <div className="image-upload" role="group" aria-labelledby={`${id}-label`}>
  <span id={`${id}-label`} className="image-upload-label">{label}{required&&<span className="image-upload-required"> *</span>}</span>
  <input ref={input} id={id} className="image-upload-input" name={name} type="file" accept="image/jpeg,image/png,image/gif" required={required} disabled={disabled} tabIndex={-1} aria-label={label} aria-describedby={`${id}-help`} onClick={event=>{if(cameraOnly)event.preventDefault()}} onChange={event=>choose(event.target.files)} onInvalid={event=>{event.preventDefault();setError(cameraOnly?'กรุณาถ่ายรูปผ่านกล้องก่อนบันทึก':'กรุณาแนบรูปภาพก่อนบันทึก');browse.current?.focus()}}/>
  <div className={`image-upload-dropzone${dragging?' is-dragging':''}${image?' has-image':''}${disabled?' is-disabled':''}`} onDragOver={event=>{event.preventDefault();if(!disabled&&!cameraOnly){event.dataTransfer.dropEffect='copy';setDragging(true)}}} onDragLeave={event=>{if(!event.currentTarget.contains(event.relatedTarget))setDragging(false)}} onDrop={event=>{event.preventDefault();setDragging(false);choose(event.dataTransfer.files)}}>
   {image?<button type="button" className="image-upload-thumbnail" disabled={disabled} onClick={()=>onPreview?.(image)} aria-label="ดูรูปขยาย"><img src={image} alt={`ตัวอย่าง${label}`}/><span>ดูรูปขยาย ↗</span></button>:<div className="image-upload-symbol" aria-hidden="true"><svg viewBox="0 0 24 24"><path d="M12 16V4m-4 4 4-4 4 4M4 15v4a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-4"/></svg></div>}
   <div className="image-upload-copy"><strong>{file?file.name:image?'แนบรูปภาพแล้ว':cameraOnly?'ถ่ายรูปผ่านกล้องเท่านั้น':'ลากรูปภาพมาวางที่นี่'}</strong><span>{file?`${(file.size/1024/1024).toFixed(2)} MB · พร้อมบันทึก`:image?(cameraOnly?'ใช้รูปเดิม หรือถ่ายรูปใหม่เพื่อเปลี่ยน':'ใช้รูปเดิม หรือเลือกรูปใหม่เพื่อเปลี่ยน'):(cameraOnly?'เปิดกล้องเพื่อถ่ายภาพเอกสารของคุณ':'หรือเลือกไฟล์จากเครื่องของคุณ')}</span><div className="image-upload-actions"><button ref={browse} type="button" className="image-upload-browse" disabled={disabled} onClick={()=>cameraOnly?setCameraOpen(true):input.current?.click()}>{cameraOnly?(image?'ถ่ายรูปใหม่':'เปิดกล้องถ่ายรูป'):(image?'เปลี่ยนรูปภาพ':'เลือกรูปภาพ')}</button>{file&&<button type="button" className="image-upload-remove" disabled={disabled} onClick={()=>{input.current.value='';setFile(null);setError('')}}>ยกเลิกรูปที่เลือก</button>}</div></div>
  </div>
  <small id={`${id}-help`} className="image-upload-help">{cameraOnly?'ถ่ายภาพใหม่จากกล้อง · JPEG · ขนาดไม่เกิน 5 MB':'JPG, PNG หรือ GIF · ขนาดไม่เกิน 5 MB · 1 รูป'}</small>
  {error&&<p className="image-upload-error" role="alert">{error}</p>}
  {cameraOpen&&<CameraCapture onClose={()=>{setCameraOpen(false);browse.current?.focus()}} onCapture={photo=>{choose([photo],true);setCameraOpen(false);browse.current?.focus()}}/>}
 </div>;
}
