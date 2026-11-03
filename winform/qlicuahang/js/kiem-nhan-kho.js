let stockReviewRows = [], stockUnconfirmedRows = [], stockReviewSelection = null, stockReconcileSelection = null;
const stockReviewLabels = {CHO_KIEM:'Chờ kiểm nhận',DA_NHAP_LAI:'Đã nhập lại kho',KHONG_NHAP_LAI:'Không nhập lại',CAN_DOI_CHIEU:'Cần đối chiếu đơn gốc'};

async function loadStockReviews() {
    if (!document.getElementById('stockReviewBody')) return;
    try {
        const [reviews,unconfirmed] = await Promise.all([fetch(API_BASE_URL+'/api/kiem-nhan-kho'),fetch(API_BASE_URL+'/api/kiem-nhan-kho/doi-chieu')]);
        if (!reviews.ok || !unconfirmed.ok) throw Error('Không tải được danh sách kiểm nhận kho.');
        stockReviewRows = await reviews.json(); stockUnconfirmedRows = await unconfirmed.json();
        renderStockReviews();
    } catch(error) { document.getElementById('stockReviewBody').innerHTML = `<tr><td colspan="8">${escapeHtml(error.message)}</td></tr>`; }
}
function renderStockReviews() {
    const body = document.getElementById('stockReviewBody'); if (!body) return;
    const source = document.getElementById('stockReviewSource').value, status = document.getElementById('stockReviewStatus').value;
    body.innerHTML = stockReviewRows.map((row,index) => ({row,index})).filter(({row}) => (!source || row.source===source) &&
        (!status || (status==='CHO_KIEM' ? ['CHO_KIEM','CAN_DOI_CHIEU'].includes(row.status) : row.status===status))).map(({row,index}) =>
        `<tr><td>${row.source==='DON_HUY'?'Đơn hủy':'Đổi/trả'}<br>${escapeHtml(row.documentId)}</td><td>${escapeHtml(row.sku)}<br>${escapeHtml(row.name)}</td><td>${escapeHtml(row.color)} / ${escapeHtml(row.size)}</td><td>${row.qty}</td><td>${escapeHtml(row.reason)}</td><td>${stockReviewLabels[row.status]||escapeHtml(row.status)}</td><td>${escapeHtml(row.reviewedBy||'—')}<br>${escapeHtml(row.reviewedAt||'')}</td><td>${window.userRole==='admin' && row.status==='CHO_KIEM'?`<button class="btn btn-primary" onclick="openStockReview(${index})">Kiểm nhận</button>`:''}</td></tr>`).join('') || '<tr><td colspan="8">Không có hàng trong nhóm này.</td></tr>';
    document.getElementById('stockUnconfirmedCard').hidden = stockUnconfirmedRows.length===0;
    document.getElementById('stockUnconfirmedBody').innerHTML = stockUnconfirmedRows.map((row,index) => `<tr><td>${escapeHtml(row.invoiceId)}</td><td>${escapeHtml(row.type)}</td><td>${escapeHtml(row.status)}</td><td>${row.deducted===false?'Chưa giữ tồn':'Chưa rõ đã trừ tồn'}</td><td>${window.userRole==='admin'?`<button class="btn btn-secondary" onclick="openStockReconcile(${index})">Đối chiếu</button>`:'Cửa hàng trưởng đối chiếu'}</td></tr>`).join('');
}
function closeStockAction() {
    if (typeof activeFormActions!=='undefined' && (activeFormActions.has('submitStockReview') || activeFormActions.has('submitStockReconcile'))) return;
    document.getElementById('stockActionModal')?.remove();
}
function openStockReview(index) {
    const row=stockReviewRows[index]; if (!row || row.status!=='CHO_KIEM' || window.userRole!=='admin') return;
    stockReviewSelection=row; document.getElementById('stockActionModal')?.remove();
    document.body.insertAdjacentHTML('beforeend',`<div class="modal-bg open" id="stockActionModal" role="dialog" aria-modal="true"><div class="modal"><h2>Kiểm nhận hàng thực tế</h2><div class="stock-action-fields"><p>${escapeHtml(row.documentId)} · <strong>${escapeHtml(row.sku)}</strong> · ${row.qty} cái</p><p>${escapeHtml(row.name)} · ${escapeHtml(row.color)} / ${escapeHtml(row.size)}</p><label>Tình trạng hàng đã nhận</label><textarea class="input" id="stockReviewNote" maxlength="1000"></textarea><label>Kết quả kiểm nhận</label><select class="input" id="stockReviewDecision"><option value="">Chọn kết quả</option><option value="restock">Đủ hàng, đạt chất lượng — nhập lại kho bán</option><option value="reject">Không đạt chất lượng — không nhập lại kho bán</option></select><label><input type="checkbox" id="stockReviewReceived"> Đã nhận đủ hàng thực tế để kiểm tra</label><p id="stockActionError" role="alert"></p></div><div class="modal-actions"><button class="btn btn-secondary" onclick="closeStockAction()">Đóng</button><button class="btn btn-primary" onclick="submitStockReview()">Lưu kiểm nhận</button></div></div></div>`);
}
async function submitStockReview() {
    const row=stockReviewSelection, decision=document.getElementById('stockReviewDecision').value, note=document.getElementById('stockReviewNote').value.trim();
    if (!decision || !note || !document.getElementById('stockReviewReceived').checked) {document.getElementById('stockActionError').textContent='Xác nhận đã nhận hàng, chọn kết quả và ghi tình trạng thực tế.'; return;}
    if (!await confirmDeletion(decision==='restock'?`Xác nhận đã nhận đủ ${row.qty} cái ${row.sku}, đạt chất lượng để cộng vào tồn bán?`:'Hàng sẽ không được cộng vào tồn bán. Xác nhận kết quả này?', 'Duyệt kiểm nhận', 'Xác nhận')) return;
    try {
        const response=await fetch(`${API_BASE_URL}/api/kiem-nhan-kho/${row.source}/${encodeURIComponent(row.documentId)}/${encodeURIComponent(row.sku)}`,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({restock:decision==='restock',note,received:true})});
        if (!response.ok) throw Error(await getApiErrorMessage(response,'Không lưu được kiểm nhận.'));
        document.getElementById('stockActionModal')?.remove();await loadAppState();renderAll();await loadStockReviews();toast('Đã lưu kiểm nhận hàng.');
    } catch(error) { const target=document.getElementById('stockActionError'); if(target)target.textContent=error.message; }
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
