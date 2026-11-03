import fs from 'node:fs/promises';import {execFileSync} from 'node:child_process';
const root='http://127.0.0.1:5022',out='artifacts/pos-returns';await fs.mkdir(out,{recursive:true});const checks=[];
const r=await fetch(root+'/api/dang-nhap',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({username:'testfixman',password:'TestOnly3010!'})});if(!r.ok)throw Error('Fixture login');const{token}=await r.json();const headers={'Content-Type':'application/json',Authorization:'Bearer '+token};
const check=(ok,label)=>{if(!ok)throw Error(label);checks.push(label);};
async function sql(name,text){const file=out+'/member-'+name+'.sql';await fs.writeFile(file,text);return execFileSync('dotnet',['run','--project','tests/pos-returns/db/audit-db.csproj','--no-build','--',file],{encoding:'utf8'});}
async function get(path){const r=await fetch(root+path,{headers});if(!r.ok)throw Error(await r.text());return r.json();}
function invoice(id,rate,code=null,voucher=0){const discount=Math.max(1000000*rate/100,voucher),vatAmount=Math.round((1000000-discount)*.08);return{id:'TESTPOS-MEMBER-'+id,customer:{name:'TESTPOS khách hàng',phone:'0998301099'},items:[{sku:'TESTPOS-SALE',name:'TESTPOS sale',qty:8,price:75000,total:600000},{sku:'TESTPOS-OTHER',name:'TESTPOS khác',qty:2,price:200000,total:400000}],subtotal:1000000,discount,vatAmount,total:1000000-discount+vatAmount,paymentMethod:'Tiền mặt',paid:1000000-discount+vatAmount,promoCode:code,isCod:false,employeeId:'TESTFIX-MAN',source:'POS'};}
async function saved(payload){const r=await fetch(root+'/api/hoa-don',{method:'POST',headers,body:JSON.stringify(payload)});if(!r.ok)throw Error(await r.text());return get('/api/hoa-don/'+payload.id);}
try{
 const cases=[{spent:0,rate:0,rank:'Bạc'},{spent:5000000,rate:0,rank:'Bạc'},{spent:5000001,rate:5,rank:'Vàng'},{spent:15000000,rate:5,rank:'Vàng'},{spent:15000001,rate:10,rank:'Kim Cương'}];
 for(const[index,c]of cases.entries()){
  await sql('tier-'+index,`UPDATE khachhang SET tongchitieu=${c.spent},hangthe='Bạc' WHERE makh='TESTPOS-KH';`);
  const payload=invoice(String(index),c.rate),actual=await saved(payload);
  check(actual.discount===payload.discount&&actual.vatAmount===payload.vatAmount&&actual.total===payload.total,`${c.spent} đồng: ${c.rank} giảm ${c.rate}% toàn bộ hai SKU trước VAT, tổng ${payload.total}`);
  const state=await get('/api/app-state'),customer=state.partnersData.find(x=>x.id==='TESTPOS-KH');check(customer.totalSpent===c.spent+payload.total,'Chi tiêu được cộng sau thanh toán, không dùng đơn hiện tại để giảm ngược chính đơn đó');
 }
 await sql('gold',"UPDATE khachhang SET tongchitieu=6000000 WHERE makh='TESTPOS-KH';");let payload=invoice('GOLD-V10',5,'TSTPOS-V10',100000);let actual=await saved(payload);check(actual.discount===100000&&actual.total===972000&&actual.promoCode==='TSTPOS-V10','Vàng 5% và voucher 10%: chỉ giảm 10%, VAT trên 900.000');
 await sql('diamond',"UPDATE khachhang SET tongchitieu=16000000 WHERE makh='TESTPOS-KH';");payload=invoice('DIAMOND-FIXED',10,'TSTPOS-CASH',10000);actual=await saved(payload);check(actual.discount===100000&&actual.total===972000&&!actual.promoCode,'Kim Cương 10% tốt hơn voucher tiền: giữ ưu đãi thành viên, không ghi voucher là đã áp dụng');
 await sql('diamond-tie',"UPDATE khachhang SET tongchitieu=16000000 WHERE makh='TESTPOS-KH';");payload=invoice('DIAMOND-TIE',10,'TSTPOS-V10',100000);actual=await saved(payload);check(actual.discount===100000&&!actual.promoCode,'Hai mức giảm bằng nhau: ưu tiên thành viên');
 await sql('reject-stack',"UPDATE khachhang SET tongchitieu=6000000 WHERE makh='TESTPOS-KH';");payload=invoice('INVALID-STACK',5,'TSTPOS-V10',100000);payload.discount=150000;payload.vatAmount=68000;payload.total=918000;payload.paid=918000;const rejected=await fetch(root+'/api/hoa-don',{method:'POST',headers,body:JSON.stringify(payload)});check(rejected.status===409,'API từ chối client tự cộng dồn thành viên 5% và voucher 10%');
}catch(error){checks.push('FAIL: '+error.message);process.exitCode=1;}
finally{await fs.writeFile(out+'/membership-api-results.json',JSON.stringify(checks,null,2));console.log(JSON.stringify(checks,null,2));}
