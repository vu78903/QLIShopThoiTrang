import fs from 'node:fs/promises';import {execFileSync} from 'node:child_process';
const root='http://127.0.0.1:5022',out='artifacts/pos-returns';await fs.mkdir(out,{recursive:true});const checks=[];
const check=(ok,label)=>{if(!ok)throw Error(label);checks.push(label)};
async function login(username){const r=await fetch(root+'/api/dang-nhap',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({username,password:'TestOnly3010!'})});if(!r.ok)throw Error('Không đăng nhập được fixture');return(await r.json()).token;}
const token=await login('testfixman'),cash=await login('testfixcash'),warehouse=await login('test69kho');
async function send(path,body,method='POST',auth=token,key){return fetch(root+path,{method,headers:{'Content-Type':'application/json',...(auth?{Authorization:'Bearer '+auth}:{}),...(key?{'X-Request-ID':key}:{})},...(body===undefined?{}:{body:JSON.stringify(body)})});}
async function get(path,auth=token){const r=await send(path,undefined,'GET',auth);if(!r.ok)throw Error(path+': '+await r.text());return r.json();}
async function ok(r){if(!r.ok)throw Error(await r.text());return r;}
const stock=async(sku='TESTPOS-SALE')=>(await get('/api/san-pham/quan-ly')).find(p=>p.sku===sku).stock;
function invoice(id,qty=1,mode='COD',sku='TESTPOS-SALE',price=75000){const subtotal=qty*price,vatAmount=Math.round(subtotal*.08),total=subtotal+vatAmount;return{id:'TEST69-'+id,customer:{name:'TEST69 khách hàng',phone:'0998301014'},items:[{sku,name:'TEST69',qty,price,total:subtotal}],subtotal,discount:0,vatAmount,total,paymentMethod:mode==='COD'?'COD / Thu hộ':'Tiền mặt',paid:mode==='COD'?0:total,promoCode:null,isCod:mode==='COD',deliveryMode:mode,receiverName:'TEST69 người nhận',receiverPhone:'0998301014',receiverAddress:'TEST69 địa chỉ',employeeId:'TESTFIX-MAN',shippingFee:0,source:'POS'};}
async function sql(name,text){const file=out+'/fix69-'+name+'.sql';await fs.writeFile(file,text);return execFileSync('dotnet',['run','--project','tests/pos-returns/db/audit-db.csproj','--no-build','--',file],{encoding:'utf8'});}
try{
 let before=await stock();const cod=invoice('COD',3);await ok(await send('/api/hoa-don',cod));check(await stock()===before-3,'6: COD trừ tồn ngay khi tạo đơn');
 await ok(await send('/api/hoa-don',cod));check(await stock()===before-3,'6/8: Gửi lại COD không trừ tồn thêm');
 let state=await get('/api/app-state');check(state.codOrders.some(x=>x.id===cod.id),'6: COD mới còn trong danh sách giao hàng');
 for(const status of ['Shipper đã lấy hàng','Hàng đang được vận chuyển','Hàng giao thành công']){const results=await Promise.all([send(`/api/hoa-don/${cod.id}/giao-hang`,{status},'PUT'),send(`/api/hoa-don/${cod.id}/giao-hang`,{status},'PUT')]);for(const r of results)await ok(r);}
 check(await stock()===before-3,'6/8: Cập nhật giao thành công đồng thời không trừ tồn lần hai');
 const points=await sql('points',"SELECT tongchitieu,diemtichluy FROM khachhang WHERE makh='TEST69-KH';");check(points.includes('243000')&&points.includes('24'),'8: COD hoàn tất ghi nhận chi tiêu/tích điểm đúng một lần');
 const cancelled=invoice('CANCEL',2);before=await stock();await ok(await send('/api/hoa-don',cancelled));
 for(const r of await Promise.all([send(`/api/hoa-don/${cancelled.id}/huy`,{reason:'TEST69 hủy'}),send(`/api/hoa-don/${cancelled.id}/huy`,{reason:'TEST69 hủy'})]))await ok(r);
 check(await stock()===before-2,'6: Hủy đơn không tự cộng kho');
 state=await get('/api/app-state');check(state.cancelledOrders.some(x=>x.id===cancelled.id)&&!state.codOrders.some(x=>x.id===cancelled.id),'6: Đơn hủy chuyển tab, giữ lịch sử và chi tiết');
 check((await get('/api/hoa-don/'+cancelled.id)).items[0].qty===2,'6: Hủy COD không xóa chi tiết hóa đơn');
 let rows=await get('/api/kiem-nhan-kho');check(rows.some(x=>x.documentId===cancelled.id&&x.status==='CHO_KIEM'&&x.qty===2),'6: Từng SKU hủy xuất hiện trong danh sách kiểm nhận');
 const path=`/api/kiem-nhan-kho/DON_HUY/${cancelled.id}/TESTPOS-SALE`,approval={restock:true,note:'TEST69 đã nhận đủ, hàng tốt',received:true};
 check((await send('/api/kiem-nhan-kho',undefined,'GET',null)).status===401,'6: Danh sách kiểm nhận bắt buộc đăng nhập');
 check((await send(path,approval,'POST',cash)).status===403&&(await send(path,approval,'POST',warehouse)).status===403,'6: Thu ngân/kho không tự duyệt cộng tồn');
 check((await send('/api/kiem-nhan-kho',undefined,'GET',warehouse)).ok,'6: Nhân viên kho được xem danh sách kiểm nhận');
 check((await send(path,{...approval,received:false})).status===409,'6: Không nhập lại hàng khi chưa xác nhận đã nhận thực tế');
 for(const r of await Promise.all([send(path,approval),send(path,approval)]))await ok(r);
 check(await stock()===before,'6/8: Duyệt hai lần chỉ cộng lại đúng số hàng hủy một lần');
 check((await send(path,{...approval,restock:false})).status===409,'6: Không sửa quyết định đã duyệt để tác động tồn lần nữa');
 const sale=invoice('SALE',2,'COUNTER');before=await stock();await ok(await send('/api/hoa-don',sale));
 const ret={invoiceId:sale.id,returnSku:'TESTPOS-SALE',qty:1,reason:'TEST69 trả hàng',resolution:'HOAN_TIEN',employeeId:'TESTFIX-MAN',clientRequestId:'TEST69-RET-1'};
 let r=await ok(await send('/api/doi-tra',ret));const returned=await r.json();check(await stock()===before-2,'6: Tiếp nhận đổi trả không tự nhập lại kho');
 rows=await get('/api/kiem-nhan-kho');check(rows.some(x=>x.documentId===returned.id&&x.source==='DOI_TRA'&&x.qty===1),'6: Hàng trả vào danh sách riêng theo phiếu/SKU');
 const returnPath=`/api/kiem-nhan-kho/DOI_TRA/${returned.id}/TESTPOS-SALE`;
 await ok(await send(returnPath,{restock:false,received:true,note:'TEST69 hàng lỗi, không đủ điều kiện bán'}));check(await stock()===before-2,'6: Hàng trả không đạt chất lượng không cộng tồn');
 check((await send(returnPath,approval)).status===409,'6: Hàng đã loại khỏi tồn không được duyệt lại để cộng kho');
 const second=await(await ok(await send('/api/doi-tra',{...ret,clientRequestId:'TEST69-RET-2'}))).json();
 for(const r of await Promise.all([send(`/api/kiem-nhan-kho/DOI_TRA/${second.id}/TESTPOS-SALE`,approval),send(`/api/kiem-nhan-kho/DOI_TRA/${second.id}/TESTPOS-SALE`,approval)]))await ok(r);
 check(await stock()===before-1,'6/8: Hàng trả đạt chất lượng nhập lại đúng một lần');
 const scarce=[invoice('LAST-A',1,'COD','TEST69-SCARCE',20000),invoice('LAST-B',1,'COD','TEST69-SCARCE',20000)];const contested=await Promise.all(scarce.map(x=>send('/api/hoa-don',x)));
 check(contested.filter(r=>r.ok).length===1&&contested.filter(r=>r.status===409).length===1&&await stock('TEST69-SCARCE')===0,'6: Hai máy tạo COD tranh hàng cuối, chỉ một đơn được giữ tồn');
 const legacy=invoice('LEGACY',2);await sql('legacy',`INSERT INTO hoadon(sohd,ngaylap,loaihoadon,phuongthucthanhtoan,tongcong,chietkhau,thuevat,khachphaitra,tienkhachdua,tienthua,manv,trangthaithanhtoan,trangthaigiaohang,datrutonkho,daghinhanthanhtoan) VALUES('${legacy.id}',LOCALTIMESTAMP,'COD','COD',150000,0,12000,162000,0,0,'TESTFIX-MAN','CHUA_THANH_TOAN','Vừa xác nhận đơn',NULL,NULL); INSERT INTO chitiethoadon(sohd,masku,soluong,dongia,thanhtien)VALUES('${legacy.id}','TESTPOS-SALE',2,75000,150000);`);
 check((await get('/api/kiem-nhan-kho/doi-chieu')).some(x=>x.invoiceId===legacy.id&&x.deducted===null),'6: Đơn cũ chưa rõ xuất kho được đưa vào đối chiếu');
 before=await stock();await ok(await send('/api/kiem-nhan-kho/doi-chieu/'+legacy.id,{alreadyDeducted:false}));await ok(await send('/api/kiem-nhan-kho/doi-chieu/'+legacy.id,{alreadyDeducted:false}));check(await stock()===before-2,'6/8: Đối chiếu đơn chưa trừ kho xuất đúng một lần');
 const supplier=(await get('/api/san-pham/quan-ly')).find(x=>x.sku==='TESTPOS-SALE').supplierId;
 const imp={supplierId:supplier,items:[{sku:'TESTPOS-SALE',qty:50,unitCost:50000}],note:'TEST69 nhập kho',clientRequestId:'TEST69-IMPORT'};
 const imports=await Promise.all([send('/api/phieu-nhap-kho',imp),send('/api/phieu-nhap-kho',imp)]);const ids=await Promise.all(imports.map(async r=>(await(await ok(r)).json()).id));check(ids[0]===ids[1],'8: Tạo nhập kho đồng thời cùng yêu cầu chỉ sinh một phiếu');
 before=await stock();for(const r of await Promise.all([send('/api/phieu-nhap-kho/'+ids[0]+'/duyet'),send('/api/phieu-nhap-kho/'+ids[0]+'/duyet')]))await ok(r);check(await stock()===before+50,'8: Duyệt nhập kho đồng thời chỉ cộng một kiện (50 cái)');
 const key='TEST69-MUTATION-IMPORT',body={...imp,clientRequestId:null,note:'TEST69 kiểm tra mã chống gửi lặp'};
 const repeated=await Promise.all([send('/api/phieu-nhap-kho',body,'POST',token,key),send('/api/phieu-nhap-kho',body,'POST',token,key)]);const cachedIds=await Promise.all(repeated.map(async r=>(await(await ok(r)).json()).id));check(cachedIds[0]===cachedIds[1],'8: API chống gửi lặp bằng X-Request-ID ngay cả phiếu sinh mã tự động');
 check((await send('/api/phieu-nhap-kho',{...body,note:'TEST69 nội dung khác'},'POST',token,key)).status===409,'8: Cùng mã yêu cầu nhưng nội dung khác bị từ chối');
 const accountPath='/api/doi-tac/'+encodeURIComponent('Khách hàng')+'/TEST69-KH/tai-khoan';let account=await get(accountPath);check(account.username==='test69customer'&&account.password==='TestOnly3010!','7: Admin đọc đúng tài khoản/mật khẩu khách hàng đang có');
 check((await send(accountPath,undefined,'GET',cash)).status===403,'7: Thu ngân không được đọc tài khoản/mật khẩu khách');
 const customer={category:'Khách hàng',id:'TEST69-KH',name:'TEST69 khách hàng',phone:'0998301014',email:'test69@example.com',address:'TEST',username:'test69updated',password:'TestUpdatedOnly!',gender:'Nam',birthDate:'2000-01-01'};
 await ok(await send('/api/doi-tac',customer));account=await get(accountPath);check(account.username===customer.username&&account.password===customer.password,'7: Sửa/lưu/mở lại đọc đúng tên đăng nhập và mật khẩu mới');
 await ok(await send('/api/doi-tac',{...customer,username:null,password:null}));account=await get(accountPath);check(account.password===customer.password,'7: Sửa thông tin khác không làm mất mật khẩu');
 state=await get('/api/app-state');const publicCustomer=state.partnersData.find(x=>x.id==='TEST69-KH');check(!('password'in publicCustomer)&&!('username'in publicCustomer),'7: API dữ liệu chung không phát tán tài khoản/mật khẩu');
 check((await send('/api/doi-tac',customer,'POST',cash)).status===403,'7: Thu ngân không được sửa tài khoản/mật khẩu');
 rows=await get('/api/kiem-nhan-kho');check(rows.find(x=>x.documentId===cancelled.id).status==='DA_NHAP_LAI'&&rows.find(x=>x.documentId===returned.id).status==='KHONG_NHAP_LAI','6: Lưu lịch sử kiểm nhận và quyết định theo từng SKU');
 const paidDelivery=invoice('PAID-DELIVERY',1,'DELIVERY');before=await stock();await ok(await send('/api/hoa-don',paidDelivery));check(await stock()===before-1,'6: Đơn giao đã thanh toán cũng trừ tồn ngay khi tạo');
 await ok(await send(`/api/hoa-don/${paidDelivery.id}/huy`,{reason:'TEST69 hủy đơn trả trước'}));check(await stock()===before-1&&(await get('/api/hoa-don/'+paidDelivery.id)).paid===paidDelivery.total,'6: Hủy giao trả trước giữ chứng từ/tiền đã thu, không tự cộng tồn');
 await ok(await send(`/api/kiem-nhan-kho/DON_HUY/${paidDelivery.id}/TESTPOS-SALE`,approval));check(await stock()===before,'6: Hàng giao trả trước chỉ cộng lại sau kiểm nhận');
 check((await send(`/api/hoa-don/${paidDelivery.id}/giao-hang`,{status:'Shipper đã lấy hàng'},'PUT')).status===409,'6: Đơn đã hủy không được giao tiếp');
 await sql('legacy-already',`INSERT INTO hoadon(sohd,ngaylap,loaihoadon,phuongthucthanhtoan,tongcong,chietkhau,thuevat,khachphaitra,tienkhachdua,tienthua,manv,trangthaithanhtoan,trangthaigiaohang,datrutonkho,daghinhanthanhtoan) VALUES('TEST69-LEGACY-ALREADY',LOCALTIMESTAMP,'COD','COD',150000,0,12000,162000,0,0,'TESTFIX-MAN','CHUA_THANH_TOAN','Vừa xác nhận đơn',NULL,NULL),('TEST69-LEGACY-CANCEL',LOCALTIMESTAMP,'DA_HUY','COD',150000,0,12000,162000,0,0,'TESTFIX-MAN','CHUA_THANH_TOAN',NULL,NULL,NULL); INSERT INTO chitiethoadon(sohd,masku,soluong,dongia,thanhtien)VALUES('TEST69-LEGACY-ALREADY','TESTPOS-SALE',2,75000,150000),('TEST69-LEGACY-CANCEL','TESTPOS-SALE',2,75000,150000); UPDATE sanpham SET soluongtonkho=soluongtonkho-2,soluong=soluong-2 WHERE masku='TESTPOS-SALE';`);
 before=await stock();await ok(await send('/api/kiem-nhan-kho/doi-chieu/TEST69-LEGACY-ALREADY',{alreadyDeducted:true}));check(await stock()===before,'6: Đơn cũ đã xuất kho được xác nhận mà không trừ thêm');
 await ok(await send('/api/hoa-don/TEST69-LEGACY-ALREADY/huy',{reason:'TEST69'}));await ok(await send('/api/kiem-nhan-kho/DON_HUY/TEST69-LEGACY-ALREADY/TESTPOS-SALE',approval));check(await stock()===before+2,'6: Đơn cũ đã xuất kho được kiểm nhận nhập lại đúng số lượng');
 before=await stock();check((await get('/api/kiem-nhan-kho/doi-chieu')).some(x=>x.invoiceId==='TEST69-LEGACY-CANCEL'),'6: Nhận diện cả đơn cũ hủy bằng loại hóa đơn, không có trạng thái giao');
 await ok(await send('/api/kiem-nhan-kho/doi-chieu/TEST69-LEGACY-CANCEL',{alreadyDeducted:false}));check(await stock()===before&&!(await get('/api/kiem-nhan-kho')).some(x=>x.documentId==='TEST69-LEGACY-CANCEL'),'6: Đơn cũ đã hủy nhưng chưa xuất kho không bị trừ hay cộng tồn');
}catch(error){checks.push('FAIL: '+error.message);process.exitCode=1;}
finally{await fs.writeFile(out+'/fix69-api-results.json',JSON.stringify(checks,null,2));console.log(JSON.stringify(checks,null,2));}
