import fs from 'node:fs/promises';import {execFileSync} from 'node:child_process';
const out='artifacts/pos-returns';await fs.mkdir(out,{recursive:true});const root='http://127.0.0.1:5022',checks=[];
const login=await fetch(root+'/api/dang-nhap',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({username:'testfixman',password:'TestOnly3010!'})});if(!login.ok)throw Error(await login.text());const {token}=await login.json();
const headers={'Content-Type':'application/json',Authorization:'Bearer '+token};const post=(url,body)=>fetch(root+url,{method:'POST',headers,body:JSON.stringify(body)});
const get=async url=>{const r=await fetch(root+url,{headers});if(!r.ok)throw Error(url+': '+await r.text());return r.json()};
const check=(ok,label)=>{if(!ok)throw Error(label);checks.push(label)};
function invoice(id,items,voucher=null,amount=0){const subtotal=items.reduce((s,i)=>s+i.price*i.qty,0),discount=Math.min(subtotal,Math.max(Math.round(subtotal*.05),amount)),vatAmount=Math.round((subtotal-discount)*.08),total=subtotal-discount+vatAmount;return {id,customer:{name:'TESTPOS khách hàng',phone:'0998301099'},items:items.map(i=>({...i,name:'TESTPOS',total:i.price*i.qty})),subtotal,discount,vatAmount,total,paymentMethod:'Tiền mặt',paid:total,promoCode:voucher,isCod:false,employeeId:'TESTFIX-MAN',source:'POS'}}
async function saved(payload){const r=await post('/api/hoa-don',payload);if(!r.ok)throw Error(await r.text());return r}
async function sql(name,text){const file=out+'/'+name+'.sql';await fs.writeFile(file,text);return execFileSync('dotnet',['run','--project','tests/pos-returns/db/audit-db.csproj','--no-build','--',file],{encoding:'utf8'});}
try{
 const products=await get('/api/san-pham/quan-ly'),p=products.find(p=>p.sku==='TESTPOS-SALE');check(p.price===100000 && p.salePrice===75000,'API trả riêng giá gốc 100.000 và giá sale 75.000');
 const a=invoice('TESTPOS-INV-A',[{sku:p.sku,qty:2,price:75000}],'TSTPOS-V10',15000);check(a.total===145800,'Giá sale + mức giảm tốt hơn giữa hạng Vàng và voucher + VAT đúng 145.800');
 const duplicate=await Promise.all([post('/api/hoa-don',a),post('/api/hoa-don',a)]);if(!duplicate.every(r=>r.ok))throw Error(await Promise.all(duplicate.map(r=>r.text())));check(true,'Hai yêu cầu thanh toán đồng thời cùng mã đều nhận kết quả thành công');
 let state=await get('/api/app-state');check(state.salesHistory.filter(i=>i.id===a.id).length===1 && state.productsData.find(p=>p.sku==='TESTPOS-SALE').stock===98,'Chỉ tạo một hóa đơn và trừ kho một lần');
 await saved(a);state=await get('/api/app-state');check(state.productsData.find(p=>p.sku==='TESTPOS-SALE').stock===98,'Gửi lại sau lỗi mạng không trừ kho lần hai');
 let r=await post('/api/hoa-don',{...a,customer:{name:'Đã đổi nội dung',phone:'0998301099'}});check(!r.ok,'Cùng mã nhưng nội dung khác bị từ chối');
 r=await post('/api/hoa-don',invoice('TESTPOS-BAD-PRICE',[{sku:p.sku,qty:1,price:100000}]));check(!r.ok,'Không chấp nhận giá gốc khi SKU đang sale');
 r=await post('/api/hoa-don',invoice('TESTPOS-BAD-PROMO',[{sku:p.sku,qty:2,price:75000}],'TSTPOS-SALE',37500));check(!r.ok,'Không dùng chương trình giảm SKU thành voucher toàn giỏ');
 r=await post('/api/hoa-don',invoice('TESTPOS-BAD-MIN',[{sku:p.sku,qty:1,price:75000}],'TSTPOS-V10',7500));check(!r.ok,'Chặn voucher chưa đạt giá trị đơn tối thiểu');
 const fixed=invoice('TESTPOS-INV-FIXED',[{sku:p.sku,qty:1,price:75000}],'TSTPOS-CASH',10000);await saved(fixed);check(fixed.total===70200,'Voucher số tiền tốt hơn hạng khách đúng 70.200');
 const detail=await get('/api/doi-tra/hoa-don/'+a.id);check(detail.items[0].paidTotal===145800 && detail.items[0].price===72900,'Đổi trả đọc giá thực trả gồm phân bổ voucher và VAT');
 const ret={invoiceId:a.id,returnSku:p.sku,qty:1,reason:'Hàng bị hư',resolution:'HOAN_TIEN',employeeId:'TESTFIX-MAN',clientRequestId:'TESTPOS-RETURN-A'};
 const twice=await Promise.all([post('/api/doi-tra',ret),post('/api/doi-tra',ret)]);const receipts=await Promise.all(twice.map(async r=>{if(!r.ok)throw Error(await r.text());return r.json()}));check(receipts[0].id===receipts[1].id && receipts[0].refundAmount===72900,'Hoàn tiền cùng yêu cầu hai lần chỉ tạo một phiếu 72.900');
 r=await post('/api/doi-tra',{...ret,clientRequestId:'TESTPOS-RETURN-B'});if(!r.ok)throw Error(await r.text());const second=await r.json();check(second.refundAmount+receipts[0].refundAmount===145800,'Hai lần trả từng phần cộng đúng số thực trả ban đầu');
 r=await post('/api/doi-tra',{...ret,clientRequestId:'TESTPOS-RETURN-OVER'});check(!r.ok,'Chặn trả vượt số lượng đã mua');
 const web=invoice('TESTPOS-INV-WEB',[{sku:'TESTPOS-OTHER',qty:2,price:200000},{sku:'TESTPOS-SALE',qty:1,price:75000}]);await saved(web);
 await sql('web-request',`INSERT INTO phieudoitra(sophieudt,sohd,manv,nguondon,trangthai,hinhthucxuly,anhminhchung) VALUES('TESTPOS-WEB-REQ','TESTPOS-INV-WEB',NULL,'WEB','CHO_DUYET','DOI_VA_TRA','["https://example.com/proof.png"]'::jsonb),('TESTPOS-WEB-REJECT','TESTPOS-INV-WEB',NULL,'WEB','CHO_DUYET','HOAN_TIEN','[]'::jsonb);\nINSERT INTO chitietdoitra(sophieudt,masku,soluong,loaigiaodich,lydotra,maskudoi,soluongdoi) VALUES('TESTPOS-WEB-REQ','TESTPOS-SALE',1,'HOAN_TIEN','Khách gửi yêu cầu trả mẫu màu đen',NULL,0),('TESTPOS-WEB-REQ','TESTPOS-OTHER',1,'DOI_SAN_PHAM','Khách yêu cầu đổi đúng mã sale','TESTPOS-SALE',1),('TESTPOS-WEB-REJECT','TESTPOS-OTHER',1,'HOAN_TIEN','Yêu cầu thứ hai',NULL,0);`);
 const requests=await get('/api/doi-tra/yeu-cau'),request=requests.find(q=>q.id==='TESTPOS-WEB-REQ');check(request.items.length===2 && request.items.find(i=>i.sku==='TESTPOS-OTHER').replacementSku==='TESTPOS-SALE' && request.images.length===1,'Đọc đúng mọi SKU, lý do, SKU muốn đổi và ảnh từ hai bảng tiếng Việt');
 r=await fetch(root+'/api/doi-tra/yeu-cau');check(r.status===401,'Yêu cầu đổi trả khách hàng được bảo vệ xác thực');
 const before=(await get('/api/san-pham/quan-ly')).find(p=>p.sku==='TESTPOS-SALE').stock;
 const approvals=await Promise.all([post('/api/doi-tra/yeu-cau/TESTPOS-WEB-REQ/duyet',{returnSku:'INVALID-TAMPER'}),post('/api/doi-tra/yeu-cau/TESTPOS-WEB-REQ/duyet',{})]);const results=await Promise.all(approvals.map(async r=>{if(!r.ok)throw Error(await r.text());return r.json()}));
 check(results[0].id===request.id && results[1].id===request.id && results[0].items.length===2,'Duyệt hai lần vẫn đúng phiếu yêu cầu nhiều SKU, bỏ qua nội dung sửa từ client');
 check(results[0].returnValue===282150 && results[0].replacementValue===81000 && results[0].refundAmount===201150,'Hoàn tiền hàng cũ theo thực trả, đổi hàng mới theo giá sale có VAT');
 check((await get('/api/san-pham/quan-ly')).find(p=>p.sku==='TESTPOS-SALE').stock===before-1,'Duyệt đổi hàng chỉ xuất SKU mới một lần');
 r=await post('/api/doi-tra/yeu-cau/TESTPOS-WEB-REJECT/tu-choi',{reason:'Không đáp ứng chính sách'});check(r.ok && (await get('/api/doi-tra/yeu-cau')).find(q=>q.id==='TESTPOS-WEB-REJECT').rejectionReason==='Không đáp ứng chính sách','Từ chối ghi đúng lý do, không tự đổi yêu cầu');
 r=await post('/api/doi-tra/yeu-cau/TESTPOS-WEB-REQ/tu-choi',{reason:'Không được hủy phiếu đã xử lý'});check(r.status===409,'Không từ chối lại yêu cầu đã hoàn tất');
 const history=await get('/api/doi-tra');check(history.filter(h=>h.id===request.id).length===1,'Lịch sử không nhân đôi phiếu khi có nhiều SKU');
 await sql('legacy-invoice',`INSERT INTO hoadon(sohd,ngaylap,loaihoadon,phuongthucthanhtoan,tongcong,chietkhau,thuevat,khachphaitra,tienkhachdua,tienthua,sodienthoai_kh,makh,manv,trangthaithanhtoan,nguondon) VALUES('TESTPOS-LEGACY',LOCALTIMESTAMP,'TAI_QUAY','Tiền mặt',200000,50000,12000,162000,162000,0,'0998301099','TESTPOS-KH','TESTFIX-MAN','DA_THANH_TOAN','POS'); INSERT INTO chitiethoadon(sohd,masku,soluong,dongia,thanhtien) VALUES('TESTPOS-LEGACY','TESTPOS-SALE',2,100000,200000);`);
 const legacy=await get('/api/doi-tra/hoa-don/TESTPOS-LEGACY');check(legacy.items[0].paidTotal===162000 && legacy.items[0].price===81000,'Hóa đơn cũ chưa có snapshot vẫn phân bổ đúng thực trả, không dùng sale hiện tại');
 await fs.writeFile(out+'/test-invoices.json',JSON.stringify({a,web,fixed,request},null,2));
}catch(e){checks.push('FAIL: '+e.message);process.exitCode=1}
finally{await fs.writeFile(out+'/api-results.json',JSON.stringify(checks,null,2));console.log(JSON.stringify(checks,null,2))}
