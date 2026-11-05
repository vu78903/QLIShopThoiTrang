import fs from 'node:fs/promises';
import path from 'node:path';
import {pathToFileURL} from 'node:url';
const {chromium}=await import(process.env.FORM_TEST_PLAYWRIGHT || new URL('../../winform/qlicuahang/node_modules/playwright-core/index.mjs',import.meta.url).href);
const out='artifacts/return-inspection/ui';await fs.mkdir(out,{recursive:true});
const checks=[],errors=[],requests=[];let failNext=false;
const state={productsData:[{productId:'SP-001',sku:'OLD',name:'Áo thử',type:'Áo thun nam',audience:'NAM',price:100000,stock:10,color:'Đen',size:'M',unit:'Cái'}],partnersData:[],productTypesData:[],promotionsData:[],importRequests:[],salesHistory:[],deliveryOrders:[],codOrders:[],cancelledOrders:[]};
const row={source:'DOI_TRA',documentId:'PDT-LOCAL-01',invoiceId:'HD-LOCAL-01',sku:'OLD',name:'Áo thử',color:'Đen',size:'M',qty:5,receivedQty:0,restockedQty:0,status:'CHO_KIEM',reason:'Khách trả sản phẩm',history:[],paymentStatus:'CHO_HOAN_TIEN',refundAmount:500000,extraAmount:0};
const webRequest={id:row.documentId,invoiceId:row.invoiceId,customerName:'Khách web',phone:'0900000000',createdAt:'05/11/2026 10:00',status:'HOAN_TAT',recipient:'Người nhận hàng đổi',exchangePhone:'0900000022',exchangeAddress:'Địa chỉ mới <img src=x onerror=alert(1)>',accountHolder:'NGUOI NHAN',bankName:'NGAN HANG TEST',bankAccount:'•••• 7890',refundAmount:108000,extraAmount:0,items:[{sku:'OLD',name:'Áo thử',color:'Đen',size:'S',qty:2,reason:'Đổi phân loại',resolution:'DOI_SAN_PHAM',replacementSku:'NEW',replacementName:'Áo thử',replacementQty:1,replacementColor:'Xanh',replacementSize:'M'}],images:[]};
const browser=await chromium.launch({headless:true,executablePath:process.env.FORM_TEST_EDGE || 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
const context=await browser.newContext({viewport:{width:1500,height:1000}});
await context.addInitScript(()=>{window.userRole='admin';window.currentEmployee={id:'M',name:'Quản lý thử'};});
await context.route('https://**/*',route=>route.fulfill({body:''}));
await context.route('http://127.0.0.1:5001/api/**',async route=>{
    const request=route.request(),url=new URL(request.url());
    if(request.method()==='GET'){
        if(url.pathname==='/api/app-state')return route.fulfill({json:state});
        if(url.pathname==='/api/doi-tra/kiem-nhan')return route.fulfill({json:[row]});
        if(url.pathname==='/api/doi-tra/yeu-cau')return route.fulfill({json:[webRequest]});
        if(url.pathname.startsWith('/api/kiem-nhan-kho'))throw Error('Returns page must not require inventory read permission');
        return route.fulfill({json:[]});
    }
    if(url.pathname.startsWith('/api/kiem-nhan-kho/DOI_TRA/')){
        const input=request.postDataJSON();requests.push(input);await new Promise(resolve=>setTimeout(resolve,350));
        if(failNext){failNext=false;return route.fulfill({status:503,json:{message:'Mất kết nối thử nghiệm'}});}
        if(!row.history.some(event=>event.reviewId===input.reviewId)){
            row.receivedQty+=input.receivedQty;row.restockedQty+=input.restockQty;
            row.status=row.receivedQty<row.qty?'DANG_KIEM':row.restockedQty===row.qty?'DA_NHAP_LAI':row.restockedQty===0?'KHONG_NHAP_LAI':'NHAP_LAI_MOT_PHAN';
            row.history.push({...input,employeeId:'M',reviewedAt:new Date().toISOString()});
            row.reason=input.note;row.reviewedBy='M';row.reviewedAt='05/11/2026 10:00';state.productsData[0].stock+=input.restockQty;
        }
        return route.fulfill({status:204});
    }
    throw Error('Unexpected mutation: '+url.pathname);
});
const page=await context.newPage();page.on('pageerror',error=>errors.push(error.message));
const check=(condition,label)=>{if(!condition)throw Error(label);checks.push(label);};
async function fill(received,restock,note){await page.locator('#stockReviewReceivedQty').fill(String(received));await page.locator('#stockReviewRestockQty').fill(String(restock));await page.locator('#stockReviewNote').fill(note);await page.locator('#stockReviewReceived').check();}
async function submit(){await page.evaluate(()=>{submitStockReview();submitStockReview();});await page.locator('#deleteAcceptButton').click();}
async function settled(){await page.waitForFunction(()=>!stockReviewSaving&&!activeFormActions.has('submitStockReview'));}
try{
    await page.goto(pathToFileURL(path.resolve('winform/qlicuahang/Views/doi-tra.html')).href);
    await page.waitForSelector('[onclick="openStockReview(0)"]');
    await page.waitForSelector('[onclick="openWebReturnRequest(0)"]');await page.evaluate(()=>openWebReturnRequest(0));
    const details=await page.locator('#webReturnModal').innerText();
    check(details.includes('Người nhận hàng đổi')&&details.includes('0900000022')&&details.includes('Địa chỉ mới'),'Web request displays contact and exchange-address snapshots');
    check(details.includes('NEW × 1')&&details.includes('Xanh / M'),'Web request retains independent replacement quantity, color and size');
    check(details.includes('•••• 7890')&&details.includes('108.000')&&await page.locator('#webReturnModal img').count()===0,'Bank and money information renders safely with escaped customer address');
    await page.locator('[onclick="showReturnInspection(0)"]').click();await page.waitForFunction(()=>document.getElementById('stockReviewSearch').value==='PDT-LOCAL-01');
    check(await page.locator('#webReturnModal').count()===0&&await page.locator('#stockReviewBody tr').count()===1,'Approved web request opens its matching receiving record');
    check((await page.locator('#stockReviewBody').innerText()).includes('Chờ hoàn tiền'),'Returns page shows stock progress and refund status independently');
    check(state.productsData[0].stock===10&&requests.length===0,'Loading an approved return does not restock goods');
    await page.locator('#returnStockReviewCard').screenshot({path:out+'/receiving-panel.png'});
    await page.evaluate(()=>openStockReview(0));await page.evaluate(()=>submitStockReview());
    check(requests.length===0&&(await page.locator('#stockActionError').innerText()).length>0,'Actual receipt and condition confirmation are required');
    await fill(6,2,'Too many');await page.evaluate(()=>submitStockReview());check(requests.length===0,'Cannot receive more than the remaining quantity');
    await fill(2,3,'More accepted than received');await page.evaluate(()=>submitStockReview());check(requests.length===0,'Cannot restock more than the actual received quantity');
    await fill(2,1,'Một tốt, một rách');await submit();
    await page.waitForSelector('#stockActionModal[aria-busy="true"]');
    check(await page.locator('#stockReviewNote').isDisabled()&&await page.locator('.stock-review-spinner').count()===1,'Saving locks inputs and displays a spinner');
    await page.evaluate(()=>closeStockAction());check(await page.locator('#stockActionModal').count()===1,'Cannot close the receiving dialog during save');
    await settled();check(requests.length===1&&state.productsData[0].stock===11,'Double click sends one request and only accepted goods increase stock');
    check(row.receivedQty===2&&row.restockedQty===1&&row.status==='DANG_KIEM','Partial receipt retains the remaining three items');
    await page.evaluate(()=>openStockReview(0));await fill(3,0,'Ba sản phẩm còn lại không đạt');failNext=true;await submit();await settled();
    check(await page.locator('#stockReviewNote').isEnabled()&&(await page.locator('#stockActionError').innerText()).includes('Mất kết nối'),'Failed save preserves data and unlocks the dialog');
    const retryId=requests.at(-1).reviewId;await submit();await settled();
    check(requests.at(-1).reviewId===retryId,'Retry preserves the same receiving identifier');
    check(state.productsData[0].stock===11&&row.receivedQty===5&&row.status==='NHAP_LAI_MOT_PHAN','Rejected goods do not enter saleable stock; fully inspected batch closes');
    check(await page.locator('#stockReviewBody [onclick="openStockReview(0)"]').count()===0,'Completed batches cannot be received again');
    await page.locator('#stockReviewStatus').selectOption('');await page.evaluate(()=>openReturnStockHistory(0));
    check(await page.locator('#stockActionModal tbody tr').count()===2&&(await page.locator('#stockActionModal').innerText()).includes('Một tốt'),'History shows both receiving events and their recorded condition');
    await page.evaluate(()=>closeStockAction());await page.screenshot({path:out+'/completed-return.png',fullPage:true});
    check(row.paymentStatus==='CHO_HOAN_TIEN'&&row.refundAmount===500000,'Receiving goods does not claim a refund has been paid');
    await page.reload();await page.waitForFunction(()=>stockReviewRows.length===1);await page.locator('#stockReviewStatus').selectOption('');
    check((await page.locator('#stockReviewBody').innerText()).includes('Đã kiểm: 5'),'Reload restores server receiving progress');
    await page.evaluate(()=>{window.userRole='cashier';stockReviewRows[0].status='CHO_KIEM';renderStockReviews();openStockReview(0);});
    check(await page.locator('#stockActionModal').count()===0&&await page.locator('#stockReviewBody [onclick^="openStockReview"]').count()===0,'Read-only role cannot open a stock receiving action');
    check(errors.length===0,'No JavaScript errors');
}catch(error){checks.push('FAIL: '+error.message);process.exitCode=1;await page.screenshot({path:out+'/failure.png',fullPage:true});}
finally{await browser.close();await fs.writeFile(out+'/results.json',JSON.stringify({checks,errors},null,2));console.log(JSON.stringify({checks,errors},null,2));}
