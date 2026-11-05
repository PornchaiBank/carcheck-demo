export const key='carcheck.bridge.connection.v1';
export function loadConnection(storage){
 try{const x=JSON.parse(storage.getItem(key));return x?.version===1&&/^[a-f0-9]{64}$/i.test(x.token)&&typeof x.reader==='string'?{token:x.token,reader:x.reader}:null;}catch{return null;}
}
export function rememberConnection(storage,token,reader){
 try{storage.setItem(key,JSON.stringify({version:1,token,reader}));return true;}catch{return false;}
}
export function forgetConnection(storage){try{storage.removeItem(key);return true;}catch{return false;}}
