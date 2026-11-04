import fs from 'node:fs';import path from 'node:path';import {pathToFileURL} from 'node:url';
import {chromium} from '../../winform/qlicuahang/node_modules/playwright-core/index.mjs';
const root=process.cwd(),out=path.join(root,'artifacts/best-sellers');fs.mkdirSync(out,{recursive:true});
const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
const context=await browser.newContext({viewport:{width:1440,height:960}});let mode='normal';const requests=[],checks=[],errors=[];
await context.addInitScript(()=>{window.userRole='admin';window.currentEmployee={id:'M',name:'Manager'};window.staffAuthenticated=true;window.lucide={createIcons(){}};window.Chart=class{destroy(){}};});
await context.route('https://**',r=>r.fulfill({status:200,body:''}));
await context.route('http://127.0.0.1:5001/api/**',async r=>{
 const url=new URL(r.request().url());
 if(url.pathname==='/api/bao-cao/ban-chay'){
  requests.push({from:url.searchParams.get('from'),to:url.searchParams.get('to')});
  return r.fulfill({status:mode==='error'?500:200,contentType:'application/json',body:JSON.stringify(mode==='error'?{message:'Lỗi thử báo cáo'}:mode==='empty'?[]:[{productId:'SP-001',name:'Áo thun nam',quantity:12,orderCount:4},{productId:'SP-002',name:'Quần jeans nam',quantity:7,orderCount:3}])});
 }
 return r.fulfill({status:200,contentType:'application/json',body:JSON.stringify({productsData:[],productTypesData:[],partnersData:[],promotionsData:[],importRequests:[],salesHistory:[],deliveryOrders:[],codOrders:[],cancelledOrders:[]})});
});
const page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));
const check=(name,ok)=>{checks.push({name,passed:ok});if(!ok)throw Error(name);};
try{
 await page.goto(pathToFileURL(path.join(root,'winform/qlicuahang/Views/bao-cao.html')).href);await page.waitForFunction(()=>window.staffApiDataReady===true);
 await page.getByRole('button',{name:'Bán chạy',exact:true}).click();await page.waitForSelector('#bestSellersBody tr:nth-child(2)');
 check('Report button opens ranking',await page.locator('#bestSellersModal').isVisible());
 check('Ranking has product ID, quantities, orders',(await page.locator('#bestSellersBody').innerText()).includes('SP-001')&&(await page.locator('#bestSellersBody tr').first().innerText()).includes('12'));
 check('Default is selected report day',requests.at(-1).from===requests.at(-1).to&&!!requests.at(-1).from);
 await page.screenshot({path:path.join(out,'report-best-sellers.png')});
 await page.locator('#bestSellersPeriod').selectOption('30days');await page.waitForTimeout(200);
 const last=requests.at(-1);check('30 days sends inclusive period',(new Date(last.to)-new Date(last.from))/86400000===29);
 await page.locator('#bestSellersPeriod').selectOption('all');await page.waitForTimeout(200);check('All time has no date limit',requests.at(-1).from===null&&requests.at(-1).to===null);
 mode='empty';await page.locator('#bestSellersPeriod').selectOption('day');await page.waitForSelector('#bestSellersBody :text("Chưa có sản phẩm")');check('Empty result is explicit',true);
 mode='error';await page.locator('#bestSellersPeriod').selectOption('all');await page.waitForSelector('#bestSellersBody [role="alert"]');check('API errors shown in ranking',true);
 mode='normal';await page.locator('#bestSellersPeriod').selectOption('day');await page.waitForSelector('#bestSellersBody tr:nth-child(2)');check('Retry restores data',true);
 await page.locator('#bestSellersModal').getByRole('button',{name:'Đóng',exact:true}).click();check('Close button works',await page.locator('#bestSellersModal').count()===0);
 check('No JavaScript errors',errors.length===0);console.log('PASS '+checks.length+' best sellers UI checks');
}finally{fs.writeFileSync(path.join(out,'ui-results.json'),JSON.stringify({checks,requests,errors},null,2));await browser.close();}
