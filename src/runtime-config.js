const configuredRoot=window.CARCHECK_CONFIG?.apiRoot?.trim();

export const apiRoot=(configuredRoot||'http://127.0.0.1:5188').replace(/\/+$/,'');
export const apiBase=`${apiRoot}/api`;
export const apiUrl=path=>`${apiRoot}${path.startsWith('/')?path:`/${path}`}`;
