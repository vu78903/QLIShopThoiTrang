import fs from 'node:fs/promises';
import path from 'node:path';
import {pathToFileURL} from 'node:url';
const {chromium}=await import(process.env.FORM_TEST_PLAYWRIGHT || new URL('../../winform/qlicuahang/node_modules/playwright-core/index.mjs',import.meta.url).href);
const out='artifacts/busy-ui/browser';await fs.mkdir(out,{recursive:true});
const checks=[],errors=[],mutations=[],gates=new Map();
const state={productsData:[],partnersData:[],productTypesData:[],promotionsData:[],importRequests:[],salesHistory:[],deliveryOrders:[],codOrders:[],cancelledOrders:[]};
const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
const context=await browser.newContext({viewport:{width:1400,height:1000}});
await context.addInitScript(()=>{window.userRole='admin';window.currentEmployee={id:'BUSY-TEST',name:'Kiểm thử'};window.testNativeMessages=[];window.chrome.webview={postMessage:message=>window.testNativeMessages.push(message)};});
await context.route('https://**/*',r=>r.fulfill({body:''}));
await context.route('http://127.0.0.1:5001/api/**',async route=>{
 const req=route.request(),url=new URL(req.url());
 if(req.method()!=='GET')mutations.push({path:url.pathname,key:req.headers()['x-request-id']});
 if(url.pathname==='/api/app-state'){await new Promise(r=>setTimeout(r,600));return route.fulfill({json:state});}
 if(url.pathname.startsWith('/api/busy-test/')||url.pathname==='/api/thanh-toan-qr/123'){
  const result=await new Promise(resolve=>gates.set(url.pathname,resolve));
  if(result==='abort')return route.abort('failed');
  try{return await route.fulfill(result||{json:{ok:true}});}catch{}
  return;
 }
 return route.fulfill({json:[]});
});
const page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));
const check=(ok,label)=>{if(!ok)throw Error(label);checks.push(label);};
async function waitGate(key){for(let i=0;i<100&&!gates.has(key);i++)await new Promise(r=>setTimeout(r,20));if(!gates.has(key))throw Error('Missing fixture request '+key);}
function release(key,response){const resolve=gates.get(key);gates.delete(key);resolve(response);}
async function idle(){await page.waitForFunction(()=>!staffUiBusy.isBusy&&!document.getElementById('staffBusyDialog')?.open);}
try{
 await page.goto(pathToFileURL(path.resolve('winform/qlicuahang/Views/doi-tra.html')).href);
 await page.waitForSelector('#staffBusyDialog[open]');
 check(await page.locator('.shell').evaluate(e=>e.inert),'Initial data load makes the page inert');
 check((await page.locator('#staffBusyTitle').innerText()).includes('Đang tải'),'Initial load displays a readable spinner message');
 await page.keyboard.press('Escape');check(await page.locator('#staffBusyDialog').evaluate(e=>e.open),'Escape cannot dismiss loading before completion');
 await page.locator('#staffBusyDialog').screenshot({path:out+'/loading.png'});await page.screenshot({path:out+'/loading-full.png'});await idle();
 await page.evaluate(()=>{window.probeClicks=0;document.body.insertAdjacentHTML('beforeend','<div id="busyProbe" style="position:fixed;right:20px;top:20px;z-index:1000;background:white;padding:12px"><button id="probeButton" onclick="window.probeClicks++">Thao tác thử</button><input id="probeInput" value="Bản nháp giữ nguyên"><button id="roleDisabled" disabled>Không có quyền</button></div>');document.getElementById('probeInput').focus();});
 await page.evaluate(()=>{window.pendingSlow=Promise.all([fetch(API_BASE_URL+'/api/busy-test/a'),fetch(API_BASE_URL+'/api/busy-test/b')]);});await waitGate('/api/busy-test/a');await waitGate('/api/busy-test/b');
 check(await page.evaluate(()=>staffUiBusy.pending===2),'Concurrent requests share one loading state');
 await page.evaluate(()=>{document.getElementById('probeButton').click();document.getElementById('probeButton').dispatchEvent(new MouseEvent('dblclick',{bubbles:true}));});
 check(await page.evaluate(()=>window.probeClicks===0),'Queued clicks and double clicks do not execute while busy');
 await page.evaluate(()=>{document.body.insertAdjacentHTML('beforeend','<button id="lateButton">Mở muộn</button>');});
 check(await page.locator('#lateButton').evaluate(e=>e.inert),'Controls inserted during loading are also locked');
 release('/api/busy-test/a');await page.waitForFunction(()=>staffUiBusy.pending===1);
 check(await page.locator('#staffBusyDialog').evaluate(e=>e.open),'Finishing one of two requests does not unlock early');
 release('/api/busy-test/b');await idle();
 check(await page.locator('#probeInput').inputValue()==='Bản nháp giữ nguyên'&&await page.locator('#roleDisabled').isDisabled(),'Unlock preserves drafts and role-disabled controls');
 check(await page.locator('#probeInput').evaluate(e=>document.activeElement===e),'Input focus is restored after loading');
 await page.locator('#probeButton').click();check(await page.evaluate(()=>window.probeClicks===1),'Controls work again after completion');
 await page.evaluate(()=>{window.duplicateWrite=Promise.all([fetch(API_BASE_URL+'/api/busy-test/write',{method:'POST',body:'{"qty":1}'}),fetch(API_BASE_URL+'/api/busy-test/write',{method:'POST',body:'{"qty":1}'})]).then(async responses=>{window.writeResults=await Promise.all(responses.map(r=>r.json()));});});await waitGate('/api/busy-test/write');
 check(mutations.filter(r=>r.path==='/api/busy-test/write').length===1,'Identical pending writes send one request');
 check((await page.locator('#staffBusyTitle').innerText()).includes('Đang xử lý'),'Saving displays a processing message');await page.locator('#staffBusyDialog').screenshot({path:out+'/saving.png'});
 release('/api/busy-test/write');await page.waitForFunction(()=>window.writeResults?.length===2);await idle();
 await page.evaluate(()=>{window.failedSave=fetch(API_BASE_URL+'/api/busy-test/retry',{method:'POST',body:'{}'}).then(r=>r.status);});await waitGate('/api/busy-test/retry');release('/api/busy-test/retry',{status:503,json:{message:'Lỗi thử'}});await idle();
 check(await page.evaluate(()=>!document.getElementById('busyProbe').inert),'HTTP errors release the page lock');
 const failedKey=mutations.at(-1).key;
 await page.evaluate(()=>{window.retrySave=fetch(API_BASE_URL+'/api/busy-test/retry',{method:'POST',body:'{}'});});await waitGate('/api/busy-test/retry');
 check(mutations.at(-1).key===failedKey&&!!failedKey,'Retry after failure keeps its idempotency key');release('/api/busy-test/retry');await idle();
 await page.evaluate(()=>{window.networkFailure=fetch(API_BASE_URL+'/api/busy-test/network').catch(e=>{window.networkError=e.name;});});await waitGate('/api/busy-test/network');release('/api/busy-test/network','abort');await page.waitForFunction(()=>!!window.networkError);await idle();
 check(await page.evaluate(()=>!document.getElementById('busyProbe').inert),'Network errors release the page lock');
 await page.evaluate(()=>{const original=setTimeout;window.setTimeout=(fn,ms,...args)=>original(fn,ms===30000?80:ms,...args);window.timedRequest=fetch(API_BASE_URL+'/api/busy-test/timeout').catch(e=>{window.timeoutError=e.name;}).finally(()=>window.setTimeout=original);});await waitGate('/api/busy-test/timeout');await page.waitForFunction(()=>!!window.timeoutError);await idle();release('/api/busy-test/timeout');
 check(await page.evaluate(()=>window.timeoutError==='TimeoutError'&&!document.getElementById('busyProbe').inert),'Timeout aborts the request and unlocks the page');
 await page.evaluate(()=>{window.polling=fetch(API_BASE_URL+'/api/thanh-toan-qr/123');});await waitGate('/api/thanh-toan-qr/123');
 check(await page.evaluate(()=>!staffUiBusy.isBusy&&!document.getElementById('staffBusyDialog').open),'Background QR polling does not block the payment dialog');release('/api/thanh-toan-qr/123');
 await page.evaluate(()=>{window.confirmation=confirmDeletion('Thử xác nhận','Kiểm tra','Xác nhận');});check(await page.locator('#deleteCancelButton').isVisible(),'Confirmation remains operable before an API request');await page.locator('#deleteCancelButton').click();
 check(await page.evaluate(()=>window.testNativeMessages.includes('staff-ui-busy')&&window.testNativeMessages.at(-1)==='staff-ui-idle'),'Native host receives matching busy and idle signals');
 await page.goto(pathToFileURL(path.resolve('winform/qlicuahang/Views/dang-nhap.html')).href);
 await page.locator('#username').fill('BUSY-TEST');await page.locator('#password').fill('Synthetic only');await page.evaluate(()=>{const form=document.getElementById('loginForm');form.dispatchEvent(new Event('submit',{bubbles:true,cancelable:true}));form.dispatchEvent(new Event('submit',{bubbles:true,cancelable:true}));});
 check(await page.evaluate(()=>testNativeMessages.filter(m=>m?.type==='login').length===1),'Double submit sends one native login message');
 check(await page.locator('#username').isDisabled()&&await page.locator('#password').isDisabled()&&await page.locator('#togglePassword').isDisabled()&&await page.locator('.login-spinner').count()===1,'Login locks fields and displays a spinner');
 await page.evaluate(()=>completeLogin(false,'Lỗi đăng nhập thử'));
 check(await page.locator('#username').isEnabled()&&await page.locator('#password').inputValue()==='Synthetic only','Failed login unlocks controls and preserves entered data');
 check(errors.length===0,'No browser JavaScript errors');
}catch(error){checks.push('FAIL: '+error.message);process.exitCode=1;await page.screenshot({path:out+'/failure.png',fullPage:true});}
finally{for(const release of gates.values())release();await browser.close();await fs.writeFile(out+'/results.json',JSON.stringify({checks,errors},null,2));console.log(JSON.stringify({checks,errors},null,2));}
