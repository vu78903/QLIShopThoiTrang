// Các giá trị dùng chung giữa POS và đổi trả; đơn giá gốc luôn được giữ để hiển thị.
function effectiveProductPrice(product) {
    return product?.salePrice !== null && product?.salePrice !== undefined
        ? Math.min(Number(product.price),Number(product.salePrice)) : Number(product?.price || 0);
}

function voucherAmount(promo,subtotal) {
    const fixed=['SO_TIEN','VND'].includes(normalizeSkuText(promo.kind || '').replace(/ /g,'_'));
    return Math.min(subtotal,Math.max(0,Math.round(fixed ? Number(promo.percent) : subtotal*Number(promo.percent)/100)));
}

function voucherText(promo) {
    return ['SO_TIEN','VND'].includes(normalizeSkuText(promo.kind || '').replace(/ /g,'_')) ? money(promo.percent) : promo.percent+'%';
}

function renderPosVoucherOptions(subtotal) {
    const select=document.getElementById('posVoucherSelect');if(!select)return;
    const coupons=promotionsData.filter(p=>!(p.products||[]).length && isPromotionActive(p,Number.MAX_SAFE_INTEGER))
        .sort((a,b)=>voucherAmount(b,subtotal)-voucherAmount(a,subtotal));
    select.innerHTML='<option value="">Chọn voucher hoặc nhập mã bên dưới</option>'+coupons.map(p=>{
        const eligible=subtotal>=Number(p.minOrder||0);
        return `<option value="${escapeHtml(p.code)}" ${eligible?'':'disabled'}>${escapeHtml(p.code)} – ${escapeHtml(voucherText(p))}${eligible?'':` (đơn từ ${money(p.minOrder)})`}</option>`;
    }).join('');
    select.value=appliedPromotion?.code || '';
    select.disabled=checkoutInProgress;
}

function selectPosVoucher(code) {
    if(checkoutInProgress)return;
    document.getElementById('promoCodeInput').value=code;applyPromotionCode();
}

function setCheckoutBusy(busy) {
    checkoutInProgress=busy;
    document.querySelectorAll('[onclick="checkout()"],#cartBody button,#cartBody input,#promoCodeInput,#posVoucherSelect').forEach(control=>control.disabled=busy);
    const label=document.getElementById('checkoutButtonLabel');
    if(label){if(busy){label.dataset.idleText=label.textContent;label.textContent='Đang xử lý…';}else if(label.dataset.idleText){label.textContent=label.dataset.idleText;delete label.dataset.idleText;}}
}

function refundableValue(item,qty) {
    if(!Number.isInteger(qty)||qty<1)return 0;
    const purchased=Number(item.purchasedQty),returned=Number(item.returnedQty||0);
    const paid=Number(item.paidTotal ?? item.price*purchased);
    const already=Number(item.returnedValue ?? Math.round(paid*returned/purchased));
    return Math.max(0,Math.min(paid-already,Math.round(paid*(returned+qty)/purchased)-Math.round(paid*returned/purchased)));
}

let webReturnRequests=[];
let webReturnRequestBusy=false;
async function loadWebReturnRequests() {
    const body=document.getElementById('webReturnRequestBody');if(!body)return;
    try{
        const response=await fetch(`${API_BASE_URL}/api/doi-tra/yeu-cau`,{cache:'no-store'});
        if(!response.ok)throw Error(await getApiErrorMessage(response,'Không tải được yêu cầu từ website.'));
        webReturnRequests=await response.json();
        body.innerHTML=webReturnRequests.map((request,index)=>`<tr><td>${escapeHtml(request.id)}</td><td>${escapeHtml(request.invoiceId)}</td><td>${escapeHtml(request.customerName)}</td><td>${escapeHtml(request.createdAt)}</td><td>${request.items.map(item=>`${escapeHtml(item.sku)} × ${item.qty}`).join('<br>')}</td><td>${request.status==='TU_CHOI'?'Đã từ chối':'Chờ tiếp nhận'}</td><td><button class="btn btn-secondary" onclick="openWebReturnRequest(${index})">Xem yêu cầu</button></td></tr>`).join('') || '<tr><td colspan="7" class="table-message">Chưa có yêu cầu đổi trả từ website.</td></tr>';
    }catch(error){body.innerHTML=`<tr><td colspan="7" class="table-message">${escapeHtml(error.message)}</td></tr>`;}
}

function closeWebReturnRequest(){if(!webReturnRequestBusy)document.getElementById('webReturnModal')?.remove();}
function openWebReturnRequest(index) {
    const request=webReturnRequests[index];if(!request||webReturnRequestBusy)return;
    document.getElementById('webReturnModal')?.remove();
    const modal=document.createElement('div');modal.id='webReturnModal';modal.className='modal-bg open';modal.setAttribute('role','dialog');modal.setAttribute('aria-modal','true');
    const pending=['CHO_DUYET','CHO_TIEP_NHAN'].includes(request.status);
    modal.innerHTML=`<div class="modal" style="width:min(1050px,95vw);max-width:1050px"><h2>Yêu cầu ${escapeHtml(request.id)}</h2><div style="padding:20px;max-height:70vh;overflow:auto"><p>Hóa đơn: <strong>${escapeHtml(request.invoiceId)}</strong> · ${escapeHtml(request.customerName)}</p><table class="data-table"><thead><tr><th>SKU đã mua</th><th>Màu/size</th><th>Số lượng</th><th>Lý do khách gửi</th><th>Yêu cầu</th><th>SKU muốn đổi</th></tr></thead><tbody>${request.items.map(item=>`<tr><td>${escapeHtml(item.sku)}<br>${escapeHtml(item.name)}</td><td>${escapeHtml(item.color)} / ${escapeHtml(item.size)}</td><td>${item.qty}</td><td>${escapeHtml(item.reason)}</td><td>${item.resolution==='DOI_SAN_PHAM'?'Đổi sản phẩm':'Hoàn tiền'}</td><td>${escapeHtml(item.replacementSku||'—')}${item.replacementSku?` × ${item.replacementQty||item.qty}<br>${escapeHtml(item.replacementName||'')}`:''}</td></tr>`).join('')}</tbody></table><div>${(request.images||[]).filter(url=>/^https?:\/\//i.test(url)).map(url=>`<img src="${escapeHtml(url)}" alt="Ảnh khách gửi" style="max-width:160px;max-height:160px;margin:8px">`).join('')}</div>${pending?'<label>Lý do từ chối (chỉ nhập khi từ chối)</label><textarea id="webReturnRejectReason" class="input" maxlength="1000"></textarea>':`<p>Lý do từ chối: ${escapeHtml(request.rejectionReason||'')}</p>`}<div id="webReturnActionError" role="alert"></div></div><div class="modal-actions"><button class="btn btn-secondary" onclick="closeWebReturnRequest()">Đóng</button>${pending?`<button class="btn btn-danger" onclick="handleWebReturnRequest(${index},false)">Từ chối</button><button class="btn btn-primary" onclick="handleWebReturnRequest(${index},true)">Duyệt đúng yêu cầu khách</button>`:''}</div></div>`;
    document.body.appendChild(modal);
}

async function handleWebReturnRequest(index,approve) {
    if(webReturnRequestBusy)return;
    const request=webReturnRequests[index],reason=document.getElementById('webReturnRejectReason')?.value.trim()||'';
    if(!approve&&!reason){document.getElementById('webReturnActionError').textContent='Nhập lý do từ chối.';return;}
    webReturnRequestBusy=true;document.querySelectorAll('#webReturnModal button').forEach(b=>b.disabled=true);
    try{
        const response=await fetch(`${API_BASE_URL}/api/doi-tra/yeu-cau/${encodeURIComponent(request.id)}/${approve?'duyet':'tu-choi'}`,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({employeeId:getCurrentEmployee()?.id,reason})});
        if(!response.ok)throw Error(await getApiErrorMessage(response,'Không xử lý được yêu cầu.'));
        const receipt=approve?await response.json():null;
        document.getElementById('webReturnModal')?.remove();await loadAppState();await loadWebReturnRequests();await loadReturnHistory();if(receipt)openReturnReceipt(receipt);toast(approve?'Đã xử lý đúng yêu cầu khách hàng.':'Đã từ chối yêu cầu.');
    }catch(error){const target=document.getElementById('webReturnActionError');if(target)target.textContent=error.message;}
    finally{webReturnRequestBusy=false;document.querySelectorAll('#webReturnModal button').forEach(b=>b.disabled=false);}
}

function renderMultipleReturnReceipt(receipt) {
    const rows=receipt.items.flatMap(item=>[
        `<tr><td>Nhận lại</td><td>${escapeHtml(item.sku)}</td><td>${escapeHtml(item.name)} (${escapeHtml(item.color)}/${escapeHtml(item.size)})</td><td>${item.qty}</td><td>${money(item.returnValue)}</td></tr>`,
        ...(item.replacementSku?[`<tr><td>Đổi mới</td><td>${escapeHtml(item.replacementSku)}</td><td>${escapeHtml(item.replacementName||'')}</td><td>${item.replacementQty||item.qty}</td><td>${money(item.replacementValue)}</td></tr>`]:[])
    ]).join('');
    return `<div class="return-slip-title">PHIẾU ĐỔI TRẢ ${escapeHtml(receipt.id)}</div><div class="return-slip-info"><div>Hóa đơn: ${escapeHtml(receipt.invoiceId)}</div><div>Ngày lập: ${escapeHtml(receipt.createdAt)}</div><div>Khách hàng: ${escapeHtml(receipt.customerName)}</div><div>Nhân viên: ${escapeHtml(receipt.employeeName)}</div><div class="full">Lý do: ${receipt.items.map(i=>escapeHtml(i.reason)).join('; ')}</div></div><table class="return-slip-table"><thead><tr><th>Nghiệp vụ</th><th>SKU</th><th>Sản phẩm</th><th>Số lượng</th><th>Giá trị</th></tr></thead><tbody>${rows}</tbody></table><div class="return-slip-totals"><div><span>Giá trị thực trả hàng nhận lại</span><strong>${money(receipt.returnValue)}</strong></div><div><span>Giá trị hàng đổi mới</span><strong>${money(receipt.replacementValue)}</strong></div><div><span>Hoàn cho khách</span><strong>${money(receipt.refundAmount)}</strong></div><div><span>Khách trả thêm</span><strong>${money(receipt.extraAmount)}</strong></div></div><div class="return-slip-signatures"><div>Khách hàng</div><div>Nhân viên tiếp nhận</div><div>Quản lý</div></div>`;
}
