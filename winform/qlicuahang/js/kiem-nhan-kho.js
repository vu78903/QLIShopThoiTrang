let stockReviewRows = [], stockUnconfirmedRows = [], stockReviewSelection = null, stockReconcileSelection = null;
let stockReviewLoading = null, stockReviewSaving = false, stockReviewAttempt = null;
const stockReviewLabels = {CHO_KIEM:'Chờ nhận và kiểm hàng',DANG_KIEM:'Đã kiểm một phần — còn chờ',DA_NHAP_LAI:'Đã nhập lại kho',NHAP_LAI_MOT_PHAN:'Đã kiểm đủ — nhập lại một phần',KHONG_NHAP_LAI:'Đã kiểm đủ — không nhập lại',CAN_DOI_CHIEU:'Cần đối chiếu đơn gốc'};

async function loadStockReviews() {
    if (!document.getElementById('stockReviewBody')) return;
    if (stockReviewLoading) return stockReviewLoading;
    const returnPage=!!document.getElementById('returnStockReviewCard');
    stockReviewLoading=(async()=>{try {
        const [reviews,unconfirmed] = await Promise.all([fetch(API_BASE_URL+(returnPage?'/api/doi-tra/kiem-nhan':'/api/kiem-nhan-kho'),{cache:'no-store'}),returnPage?Promise.resolve({ok:true,json:async()=>[]}):fetch(API_BASE_URL+'/api/kiem-nhan-kho/doi-chieu',{cache:'no-store'})]);
        if (!reviews.ok || !unconfirmed.ok) throw Error('Không tải được danh sách kiểm nhận kho.');
        stockReviewRows = await reviews.json(); stockUnconfirmedRows = await unconfirmed.json();
        renderStockReviews();
    } catch(error) { stockReviewRows=[];document.getElementById('stockReviewBody').innerHTML = `<tr><td colspan="${returnPage?9:8}">${escapeHtml(error.message)}</td></tr>`; }
    finally{stockReviewLoading=null;}})();
    return stockReviewLoading;
}
function renderStockReviews() {
    const body = document.getElementById('stockReviewBody'); if (!body) return;
    const source = document.getElementById('stockReviewSource').value, status = document.getElementById('stockReviewStatus').value;
    const returnPage=!!document.getElementById('returnStockReviewCard'),search=(document.getElementById('stockReviewSearch')?.value||'').trim().toLocaleLowerCase('vi');
    body.innerHTML = stockReviewRows.map((row,index) => ({row,index})).filter(({row}) => (!source || row.source===source) &&
        (!status || (status==='CHO_KIEM' ? ['CHO_KIEM','DANG_KIEM','CAN_DOI_CHIEU'].includes(row.status) : row.status===status)) &&
        (!search || [row.documentId,row.invoiceId,row.sku,row.name].join(' ').toLocaleLowerCase('vi').includes(search))).map(({row,index}) => {
        const checked=Number(row.receivedQty||0),accepted=Number(row.restockedQty||0);
        const quantity=row.source==='DOI_TRA'?`Theo phiếu: ${row.qty}<br>Đã kiểm: ${checked} · Nhập kho: ${accepted}<br>Không nhập: ${checked-accepted} · Còn chờ: ${row.qty-checked}`:row.qty;
        const payment=row.paymentStatus==='DA_HOAN_TIEN'?'Đã hoàn tiền':row.paymentStatus==='CHO_HOAN_TIEN'?'Chờ hoàn tiền':row.paymentStatus==='CAN_DOI_SOAT_THU_THEM'?'Cần đối soát thu thêm':'Không chênh lệch';
        const paymentCell=returnPage?`<td>${payment}<br><small>Hoàn: ${money(row.refundAmount||0)}<br>Thu thêm: ${money(row.extraAmount||0)}<br>Số tiền của cả phiếu</small></td>`:'';
        const canInspect=window.userRole==='admin' && ['CHO_KIEM','DANG_KIEM'].includes(row.status);
        return `<tr><td>${row.source==='DON_HUY'?'Đơn hủy':'Đổi/trả'}<br>${escapeHtml(row.documentId)}${returnPage?`<br><small>Hóa đơn: ${escapeHtml(row.invoiceId)}</small>`:''}</td><td>${escapeHtml(row.sku)}<br>${escapeHtml(row.name)}</td><td>${escapeHtml(row.color)} / ${escapeHtml(row.size)}</td><td>${quantity}</td><td>${escapeHtml(row.reason)}</td><td>${stockReviewLabels[row.status]||escapeHtml(row.status)}</td><td>${escapeHtml(row.reviewedBy||'—')}<br>${escapeHtml(row.reviewedAt||'')}</td>${paymentCell}<td>${canInspect?`<button class="btn btn-primary" onclick="openStockReview(${index})">Nhận / kiểm hàng</button>`:row.status==='CAN_DOI_CHIEU'?'Đối chiếu đơn gốc tại Tồn kho':''}${row.source==='DOI_TRA'&&(row.receivedQty||row.history?.length)?`<button class="btn btn-secondary" onclick="openReturnStockHistory(${index})">Lịch sử kiểm</button>`:''}</td></tr>`;
    }).join('') || `<tr><td colspan="${returnPage?9:8}">Không có hàng trong nhóm này.</td></tr>`;
    if(!document.getElementById('stockUnconfirmedCard'))return;
    document.getElementById('stockUnconfirmedCard').hidden = stockUnconfirmedRows.length===0;
    document.getElementById('stockUnconfirmedBody').innerHTML = stockUnconfirmedRows.map((row,index) => `<tr><td>${escapeHtml(row.invoiceId)}</td><td>${escapeHtml(row.type)}</td><td>${escapeHtml(row.status)}</td><td>${row.deducted===false?'Chưa giữ tồn':'Chưa rõ đã trừ tồn'}</td><td>${window.userRole==='admin'?`<button class="btn btn-secondary" onclick="openStockReconcile(${index})">Đối chiếu</button>`:'Cửa hàng trưởng đối chiếu'}</td></tr>`).join('');
}
function closeStockAction() {
    if(stockReviewSaving)return;
    if (typeof activeFormActions!=='undefined' && (activeFormActions.has('submitStockReview') || activeFormActions.has('submitStockReconcile'))) return;
    document.getElementById('stockActionModal')?.remove();
}
function openStockReview(index) {
    const row=stockReviewRows[index]; if (stockReviewSaving || !row || !['CHO_KIEM','DANG_KIEM'].includes(row.status) || window.userRole!=='admin') return;
    stockReviewAttempt=null;
    stockReviewSelection=row; document.getElementById('stockActionModal')?.remove();
    document.body.insertAdjacentHTML('beforeend',`<div class="modal-bg open" id="stockActionModal" role="dialog" aria-modal="true"><div class="modal"><h2>Kiểm nhận hàng thực tế</h2><div class="stock-action-fields"><p>${escapeHtml(row.documentId)} · <strong>${escapeHtml(row.sku)}</strong> · ${row.qty} cái</p><p>${escapeHtml(row.name)} · ${escapeHtml(row.color)} / ${escapeHtml(row.size)}</p><label>Tình trạng hàng đã nhận</label><textarea class="input" id="stockReviewNote" maxlength="1000"></textarea><label>Kết quả kiểm nhận</label><select class="input" id="stockReviewDecision"><option value="">Chọn kết quả</option><option value="restock">Đủ hàng, đạt chất lượng — nhập lại kho bán</option><option value="reject">Không đạt chất lượng — không nhập lại kho bán</option></select><label><input type="checkbox" id="stockReviewReceived"> Đã nhận đủ hàng thực tế để kiểm tra</label><p id="stockActionError" role="alert"></p></div><div class="modal-actions"><button class="btn btn-secondary" onclick="closeStockAction()">Đóng</button><button class="btn btn-primary" onclick="submitStockReview()">Lưu kiểm nhận</button></div></div></div>`);
    if(row.source==='DOI_TRA'){
        const remaining=row.qty-Number(row.receivedQty||0),fields=document.querySelector('#stockActionModal .stock-action-fields');
        fields.innerHTML=`<p>Phiếu <strong>${escapeHtml(row.documentId)}</strong> · ${escapeHtml(row.name)} (${escapeHtml(row.color)} / ${escapeHtml(row.size)})</p><p>Đã kiểm ${row.receivedQty||0}/${row.qty} · Còn chờ <strong>${remaining}</strong></p><label for="stockReviewReceivedQty">Số lượng thực nhận và kiểm trong lần này</label><input id="stockReviewReceivedQty" class="input" type="number" min="1" max="${remaining}" step="1" value="${remaining}"><label for="stockReviewRestockQty">Số lượng đạt chất lượng, nhập lại kho bán</label><input id="stockReviewRestockQty" class="input" type="number" min="0" max="${remaining}" step="1" value="0"><p class="cell-note">Lượng không đạt = thực nhận − nhập kho. Hàng chưa về vẫn chờ nhận. Kiểm hàng không xác nhận thu/hoàn tiền.</p><label for="stockReviewNote">Tình trạng và lý do đạt / không đạt</label><textarea id="stockReviewNote" class="input" maxlength="1000"></textarea><label><input type="checkbox" id="stockReviewReceived"> Tôi đã nhận và kiểm số lượng ghi trên</label><p id="stockActionError" role="alert"></p>`;
    }
}
async function submitStockReview() {
    if(stockReviewSaving)return;
    const row=stockReviewSelection;if(!row)return;
    const note=document.getElementById('stockReviewNote').value.trim();
    if(row.source==='DOI_TRA'){
        const received=Number(document.getElementById('stockReviewReceivedQty').value),accepted=Number(document.getElementById('stockReviewRestockQty').value),remaining=row.qty-Number(row.receivedQty||0);
        if(!Number.isInteger(received)||received<1||received>remaining||!Number.isInteger(accepted)||accepted<0||accepted>received||!note||note.length>1000||!document.getElementById('stockReviewReceived').checked){document.getElementById('stockActionError').textContent='Xác nhận đã nhận hàng, ghi tình trạng; lượng nhận không vượt phần chờ và lượng nhập kho không vượt lượng nhận.';return;}
        const payload={restock:accepted>0,note,received:true,receivedQty:received,restockQty:accepted,expectedReceivedQty:Number(row.receivedQty||0)};
        const signature=JSON.stringify(payload);if(stockReviewAttempt?.signature!==signature)stockReviewAttempt={signature,reviewId:crypto.randomUUID()};payload.reviewId=stockReviewAttempt.reviewId;
        stockReviewSaving=true;
        try{
            if(!await confirmDeletion(`Đã nhận và kiểm ${received} cái: nhập kho ${accepted}, không nhập ${received-accepted}; còn chờ ${remaining-received}. Không xác nhận thu/hoàn tiền.`, 'Xác nhận kiểm hàng','Xác nhận'))return;
            const modal=document.getElementById('stockActionModal');modal.querySelectorAll('button,input,textarea').forEach(el=>el.disabled=true);modal.setAttribute('aria-busy','true');
            const button=modal.querySelector('[onclick="submitStockReview()"]');button.innerHTML='<span class="stock-review-spinner" aria-hidden="true"></span> Đang lưu kiểm nhận…';
            const response=await fetch(`${API_BASE_URL}/api/kiem-nhan-kho/DOI_TRA/${encodeURIComponent(row.documentId)}/${encodeURIComponent(row.sku)}`,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(payload)});
            if(!response.ok)throw Error(await getApiErrorMessage(response,'Không lưu được kiểm nhận.'));
            modal.remove();await loadAppState();renderAll();await loadStockReviews();toast(`Đã kiểm ${received} cái, nhập lại kho ${accepted} cái.`);
        }catch(error){const target=document.getElementById('stockActionError');if(target)target.textContent=error.message;}
        finally{stockReviewSaving=false;const modal=document.getElementById('stockActionModal');if(modal){modal.removeAttribute('aria-busy');modal.querySelectorAll('button,input,textarea').forEach(el=>el.disabled=false);modal.querySelector('[onclick="submitStockReview()"]').textContent='Lưu kiểm nhận';}}
        return;
    }
    const decision=document.getElementById('stockReviewDecision').value;
    if (!decision || !note || !document.getElementById('stockReviewReceived').checked) {document.getElementById('stockActionError').textContent='Xác nhận đã nhận hàng, chọn kết quả và ghi tình trạng thực tế.'; return;}
    if (!await confirmDeletion(decision==='restock'?`Xác nhận đã nhận đủ ${row.qty} cái ${row.sku}, đạt chất lượng để cộng vào tồn bán?`:'Hàng sẽ không được cộng vào tồn bán. Xác nhận kết quả này?', 'Duyệt kiểm nhận', 'Xác nhận')) return;
    try {
        const response=await fetch(`${API_BASE_URL}/api/kiem-nhan-kho/${row.source}/${encodeURIComponent(row.documentId)}/${encodeURIComponent(row.sku)}`,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({restock:decision==='restock',note,received:true})});
        if (!response.ok) throw Error(await getApiErrorMessage(response,'Không lưu được kiểm nhận.'));
        document.getElementById('stockActionModal')?.remove();await loadAppState();renderAll();await loadStockReviews();toast('Đã lưu kiểm nhận hàng.');
    } catch(error) { const target=document.getElementById('stockActionError'); if(target)target.textContent=error.message; }
}
function openReturnStockHistory(index){
    if(stockReviewSaving)return;const row=stockReviewRows[index];if(!row)return;
    document.getElementById('stockActionModal')?.remove();
    const history=(row.history||[]).map(event=>`<tr><td>${escapeHtml(new Date(event.reviewedAt).toLocaleString('vi-VN'))}</td><td>${escapeHtml(event.employeeId)}</td><td>${event.receivedQty}</td><td>${event.restockQty}</td><td>${event.receivedQty-event.restockQty}</td><td>${escapeHtml(event.note)}</td></tr>`).join('');
    document.body.insertAdjacentHTML('beforeend',`<div class="modal-bg open" id="stockActionModal" role="dialog" aria-modal="true"><div class="modal return-inspection-history"><h2>Lịch sử kiểm hàng ${escapeHtml(row.documentId)}</h2><div class="stock-action-fields"><p>${escapeHtml(row.name)} · ${escapeHtml(row.sku)}</p><div class="table-wrap"><table class="data-table"><thead><tr><th>Thời gian</th><th>Người kiểm</th><th>Đã kiểm</th><th>Nhập kho</th><th>Không nhập</th><th>Tình trạng</th></tr></thead><tbody>${history||`<tr><td colspan="6">Kết quả chuyển từ dữ liệu cũ: đã kiểm ${row.receivedQty||0}, nhập kho ${row.restockedQty||0}. ${escapeHtml(row.reason)}</td></tr>`}</tbody></table></div><p>Kiểm nhận hàng độc lập với xác nhận thu thêm hoặc hoàn tiền.</p></div><div class="modal-actions"><button class="btn btn-secondary" onclick="closeStockAction()">Đóng</button></div></div></div>`);
}
async function openStockReconcile(index) {
    const row=stockUnconfirmedRows[index];if(!row || window.userRole!=='admin')return;
    stockReconcileSelection=row; document.getElementById('stockActionModal')?.remove();
    document.body.insertAdjacentHTML('beforeend',`<div class="modal-bg open" id="stockActionModal" role="dialog" aria-modal="true"><div class="modal"><h2>Đối chiếu tồn của đơn cũ</h2><div class="stock-action-fields"><p>Đơn <strong>${escapeHtml(row.invoiceId)}</strong></p><div id="stockReconcileItems">Đang tải chi tiết…</div><label>Kết quả đối chiếu chứng từ xuất kho</label><select class="input" id="stockReconcileDecision"><option value="">Chọn kết quả</option><option value="false">Chưa trừ tồn</option>${row.deducted===null?'<option value="true">Đã trừ tồn trước đây</option>':''}</select><p id="stockActionError" role="alert"></p></div><div class="modal-actions"><button class="btn btn-secondary" onclick="closeStockAction()">Đóng</button><button class="btn btn-primary" onclick="submitStockReconcile()">Xác nhận đối chiếu</button></div></div></div>`);
    try {const response=await fetch(`${API_BASE_URL}/api/hoa-don/${encodeURIComponent(row.invoiceId)}`);if(!response.ok)throw Error('Không đọc được hóa đơn.');const invoice=await response.json();const target=document.getElementById('stockReconcileItems');if(target)target.textContent=invoice.items.map(item=>`${item.sku} × ${item.qty}`).join(' · ');}
    catch(error){const target=document.getElementById('stockActionError');if(target)target.textContent=error.message;}
}
async function submitStockReconcile() {
    const value=document.getElementById('stockReconcileDecision').value,row=stockReconcileSelection;
    if(!value){document.getElementById('stockActionError').textContent='Cần đối chiếu chứng từ trước khi chọn.';return;}
    const message=value==='true'?'Chỉ đánh dấu đơn đã xuất kho trước đây, không trừ tồn thêm.':'Đơn đang giao sẽ trừ tồn ngay; đơn đã hủy sẽ không trừ hay cộng tồn.';
    if(!await confirmDeletion(message,'Xác nhận đối chiếu đơn cũ','Xác nhận'))return;
    try {const response=await fetch(`${API_BASE_URL}/api/kiem-nhan-kho/doi-chieu/${encodeURIComponent(row.invoiceId)}`,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({alreadyDeducted:value==='true'})});if(!response.ok)throw Error(await getApiErrorMessage(response,'Không đối chiếu được tồn.'));document.getElementById('stockActionModal')?.remove();await loadAppState();renderAll();await loadStockReviews();toast('Đã đối chiếu tồn kho.');}
    catch(error){const target=document.getElementById('stockActionError');if(target)target.textContent=error.message;}
}
async function cancelShipmentOrder(order) {
    if(!order)return;
    if(!await confirmDeletion('Giữ nguyên lịch sử đơn. Hàng hủy chỉ được cộng kho sau khi kiểm nhận; tiền đã thu chưa được hoàn tự động.','Hủy đơn giao hàng','Hủy đơn'))return;
    try {const response=await fetch(`${API_BASE_URL}/api/hoa-don/${encodeURIComponent(order.receipt?.id||order.id)}/huy`,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({reason:'Nhân viên hủy đơn giao hàng'})});if(!response.ok)throw Error(await getApiErrorMessage(response,'Không hủy được đơn.'));await loadAppState();renderAll();toast('Đã hủy, giữ lịch sử và chờ kiểm nhận hàng.');}
    catch(error){toast(error.message);}
}
async function cancelDelivery(index) {await cancelShipmentOrder(deliveryOrders[index]);}
document.addEventListener('DOMContentLoaded',loadStockReviews);
