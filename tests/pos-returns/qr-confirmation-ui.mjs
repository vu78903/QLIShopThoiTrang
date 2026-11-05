import fs from 'node:fs/promises';import path from 'node:path';import {pathToFileURL} from 'node:url';
const {chromium}=await import(process.env.FORM_TEST_PLAYWRIGHT || new URL('../../winform/qlicuahang/node_modules/playwright-core/index.mjs',import.meta.url).href);
const out='artifacts/qr-confirmation/ui';await fs.mkdir(out,{recursive:true});
const checks=[],errors=[],creates=[],invoicePosts=[],cancels=[];let status='PENDING',pollError=false,createError=false,paid=false,code=900000;
const product={productId:'QR-P',sku:'QR-SKU',name:'Áo kiểm thử QR',type:'Áo thun nam',audience:'NAM',price:100000,stock:10,color:'Đen',size:'S',unit:'Cái'};
const state={productsData:[product],partnersData:[{id:'QR-C',category:'Nhân viên',name:'Thu ngân QR',role:'Nhân viên thu ngân'},{id:'QR-K',category:'Khách hàng',name:'Khách QR',phone:'0900000555',totalSpent:0}],productTypesData:[],promotionsData:[],importRequests:[],salesHistory:[],deliveryOrders:[],codOrders:[],cancelledOrders:[]};
const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});const context=await browser.newContext({viewport:{width:1500,height:1000}});
await context.addInitScript(()=>{window.userRole='cashier';window.currentEmployee={id:'QR-C',name:'Thu ngân QR'};});
await context.route('https://**/*',route=>route.request().url().startsWith('https://img.vietqr.io/')?route.fulfill({contentType:'image/svg+xml',body:'<svg xmlns="http://www.w3.org/2000/svg" width="280" height="280"><rect width="280" height="280" fill="#eef3fa"/><text x="140" y="130" text-anchor="middle" fill="#163253">QR GIẢ LẬP</text><text x="140" y="160" text-anchor="middle" fill="#163253">KHÔNG CHUYỂN TIỀN</text></svg>'}):route.fulfill({body:''}));
await context.route('http://127.0.0.1:5001/api/**',async route=>{
 const request=route.request(),url=new URL(request.url());
 if(url.pathname==='/api/app-state')return route.fulfill({json:state});
 if(request.method()==='POST'&&url.pathname==='/api/thanh-toan-qr'){
  const invoice=request.postDataJSON();creates.push(invoice);await new Promise(r=>setTimeout(r,200));
  if(createError)return route.fulfill({status:409,json:{message:'Chưa cấu hình PAYOS_CLIENT_ID, PAYOS_API_KEY và PAYOS_CHECKSUM_KEY.'}});
  code++;paid=false;status='PENDING';return route.fulfill({json:{orderCode:code,invoiceId:invoice.id,amount:invoice.total,description:'TEST QR',bankBin:'970422',accountNumber:'0000000000',accountName:'TEST ONLY',paymentLinkId:'fixture-'+code,checkoutUrl:'https://example.test/qr',status}});
 }
 if(url.pathname.startsWith('/api/thanh-toan-qr/')){
  if(request.method()==='DELETE'){cancels.push(code);status='CANCELLED';return route.fulfill({status:204});}
  if(pollError)return route.fulfill({status:503,json:{message:'Provider thử nghiệm mất kết nối'}});
  if(paid){product.stock=9;state.salesHistory=[{id:creates.at(-1).id,dateKey:'2026-11-05',time:'15:00',customerName:'Khách QR',paymentMethod:'Quét mã QR',total:108000}];}
  await new Promise(r=>setTimeout(r,100));return route.fulfill({json:{orderCode:code,status:paid?'PAID':status,amount:108000,amountPaid:paid?108000:0,paid}});
 }
 if(url.pathname==='/api/hoa-don'&&request.method()==='POST'){invoicePosts.push(request.postDataJSON());return route.fulfill({json:{}});}
 return route.fulfill({json:[]});
});
const page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));const check=(ok,label)=>{if(!ok)throw Error(label);checks.push(label);};
async function basket(){await page.goto(pathToFileURL(path.resolve('winform/qlicuahang/Views/ban-hang.html')).href);await page.waitForFunction(()=>window.staffApiDataReady&&!staffUiBusy.isBusy);await page.evaluate(()=>{cart=[];checkoutAttempt=null;addToCartBySku('QR-SKU');document.getElementById('customerSearch').value='0900000555';updateCustomerInfo();document.querySelector('[name=paymentMethod][value="Quét mã QR"]').checked=true;handlePaymentChange();window.receiptOpenCount=0;const original=openReceiptModal;window.openReceiptModal=(...args)=>{window.receiptOpenCount++;return original(...args);};});}
async function qrReady(){await page.waitForFunction(()=>pendingQrOrderCode!==null&&!staffUiBusy.isBusy);}
try{
 await basket();await page.evaluate(()=>{checkout();checkout();});await qrReady();
 check(creates.length===1,'Double click creates one QR payment session');
 check(await page.locator('#qrPaymentModalBg').evaluate(e=>e.classList.contains('open')),'QR payment dialog opens');
 check((await page.locator('#qrPaymentImage').getAttribute('src')).includes('amount=108000'),'QR uses exact invoice total');
 await page.evaluate(()=>{stopQrPaymentPolling();window.pendingCheck=checkQrPaymentStatus();});await page.waitForFunction(()=>document.getElementById('qrPaymentStatus').textContent.includes('Đang chờ ngân hàng'));await page.evaluate(()=>stopQrPaymentPolling());
 check(await page.evaluate(()=>cart.length===1&&receiptOpenCount===0)&&invoicePosts.length===0&&product.stock===10,'PENDING does not confirm invoice, clear cart or deduct stock');
 check(await page.locator('[onclick="checkout()"]').isDisabled(),'Checkout stays locked while awaiting payment');
 await page.locator('#qrPaymentModalBg').screenshot({path:out+'/pending-qr.png'});
 pollError=true;await page.evaluate(()=>{window.failedPoll=checkQrPaymentStatus();});await page.waitForFunction(()=>document.getElementById('qrPaymentStatus').textContent.includes('Mất kết nối'));await page.evaluate(()=>stopQrPaymentPolling());pollError=false;
 check(await page.evaluate(()=>cart.length===1&&receiptOpenCount===0),'Provider error does not falsely confirm payment');
 paid=true;await page.evaluate(()=>{window.paidPolls=Promise.all([checkQrPaymentStatus(),checkQrPaymentStatus()]);});await page.waitForFunction(()=>cart.length===0&&!checkoutInProgress&&!checkoutCompletionInProgress);
 check(await page.evaluate(()=>receiptOpenCount===1&&lastReceipt.total===108000),'PAID automatically opens one completed receipt');
 check(await page.locator('#qrPaymentModalBg').evaluate(e=>!e.classList.contains('open')),'Paid QR dialog closes automatically');
 check(invoicePosts.length===0,'UI does not POST a second invoice after backend confirms payment');
 check(await page.evaluate(()=>productsData.find(p=>p.sku==='QR-SKU').stock===9),'Post-payment reload shows stock reduced once');
 check(await page.locator('[onclick="checkout()"]').isEnabled(),'UI unlocks after confirmed payment');
 await page.screenshot({path:out+'/confirmed-receipt.png',fullPage:true});
 product.stock=10;await basket();await page.evaluate(()=>checkout());await qrReady();await page.locator('[onclick="cancelQrPayment()"]').click();await page.waitForFunction(()=>!checkoutInProgress);
 check(cancels.length===1&&await page.evaluate(()=>cart.length===1&&receiptOpenCount===0),'Cancel unpaid QR preserves cart and creates no receipt');
 createError=true;await basket();await page.evaluate(()=>checkout());await page.waitForFunction(()=>!checkoutInProgress&&!staffUiBusy.isBusy);
 check((await page.locator('#qrFallback').innerText()).includes('PAYOS_CLIENT_ID'),'Missing merchant configuration is displayed to staff');
 check(await page.evaluate(()=>pendingQrOrderCode===null&&cart.length===1&&receiptOpenCount===0),'Failed QR creation does not mark sale as paid');
 check(errors.length===0,'No JavaScript errors');
}catch(error){checks.push('FAIL: '+error.message);process.exitCode=1;await page.screenshot({path:out+'/failure.png',fullPage:true});}
finally{await browser.close();await fs.writeFile(out+'/results.json',JSON.stringify({checks,errors},null,2));console.log(JSON.stringify({checks,errors},null,2));}
