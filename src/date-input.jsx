import React from 'react';
import {formatDateInput} from './date-fields.js';
export default function DateInput({label,value,onChange,disabled=false}){
 return <span className="date-input-wrap date-standard"><input type="date" lang="en-GB" tabIndex={-1} aria-label={'ปฏิทิน '+label} value={value} onChange={onChange} disabled={disabled}/><input type="text" className="date-text" data-date-text inputMode="numeric" placeholder="วว/ดด/ปปปป" aria-label={label+' (วัน/เดือน/ปี)'} autoComplete="off" defaultValue={formatDateInput(value)} disabled={disabled}/><button type="button" className="date-calendar-button" data-action="open-date-picker" aria-label={'เลือก'+label} disabled={disabled}><svg viewBox="0 0 24 24" aria-hidden="true"><rect x="3" y="5" width="18" height="16" rx="2"/><path d="M16 3v4M8 3v4M3 11h18"/></svg></button></span>;
}
