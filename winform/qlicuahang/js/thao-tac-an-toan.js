// Một lần bấm là một thao tác; mở khóa cả khi API/network lỗi.
// Giữ một khóa chung đến khi mọi request đã đọc xong body; không sửa trạng thái disabled của nghiệp vụ.
const staffUiBusy = (() => {
    const operations = new Map(), inertBefore = new Map();
    let overlay = null, observer = null, hideTimer = null, focusBefore = null, priorAriaBusy = null;
    function freeze(element) {
        if (!(element instanceof HTMLElement) || element === overlay || inertBefore.has(element)) return;
        inertBefore.set(element, element.inert); element.inert = true;
    }
    function announce(busy) { window.chrome?.webview?.postMessage(busy ? 'staff-ui-busy' : 'staff-ui-idle'); }
    function ensureOverlay() {
        if (overlay?.isConnected) return;
        overlay = document.createElement('dialog'); overlay.id = 'staffBusyDialog'; overlay.className = 'staff-busy-dialog';
        overlay.setAttribute('aria-modal','true'); overlay.setAttribute('aria-labelledby','staffBusyTitle');
        overlay.setAttribute('aria-describedby','staffBusyHint');
        overlay.innerHTML = '<span class="staff-busy-spinner" aria-hidden="true"></span><strong id="staffBusyTitle" role="status" aria-live="polite">Đang tải dữ liệu…</strong><p id="staffBusyHint">Vui lòng chờ hoàn tất trước khi thao tác tiếp.</p>';
        overlay.addEventListener('cancel', event => event.preventDefault()); document.body.appendChild(overlay);
    }
    function hide() {
        if (operations.size) return;
        observer?.disconnect(); observer = null;
        if (overlay?.open) overlay.close();
        for (const [element, wasInert] of inertBefore) element.inert = wasInert;
        inertBefore.clear(); document.documentElement.classList.remove('staff-ui-busy');
        if (priorAriaBusy === null) document.body.removeAttribute('aria-busy'); else document.body.setAttribute('aria-busy',priorAriaBusy);
        if (focusBefore?.isConnected && !focusBefore.disabled && !focusBefore.closest('[inert]')) focusBefore.focus({preventScroll:true});
        focusBefore = null; announce(false);
    }
    function begin(label) {
        clearTimeout(hideTimer); const id = Symbol(); operations.set(id,label);
        ensureOverlay();
        if (!overlay.open) {
            focusBefore = document.activeElement; priorAriaBusy = document.body.getAttribute('aria-busy');
            document.body.setAttribute('aria-busy','true'); document.documentElement.classList.add('staff-ui-busy');
            [...document.body.children].forEach(freeze);
            observer = new MutationObserver(records => records.forEach(record => record.addedNodes.forEach(freeze)));
            observer.observe(document.body,{childList:true}); overlay.showModal(); announce(true);
        }
        document.getElementById('staffBusyTitle').textContent = [...operations.values()].includes('Đang xử lý yêu cầu…') ? 'Đang xử lý yêu cầu…' : label;
        let released = false;
        return () => {
            if (released) return; released = true; operations.delete(id);
            if (!operations.size) hideTimer = setTimeout(hide,0);
        };
    }
    // Chặn cả Enter, phím tắt và click được xếp hàng từ lần bấm trước.
    for (const name of ['click','dblclick','pointerdown','keydown','submit']) document.addEventListener(name,event => {
        if (!operations.size || overlay?.contains(event.target)) return;
        event.preventDefault(); event.stopImmediatePropagation();
    },true);
    return {begin, get isBusy(){return operations.size > 0;}, get pending(){return operations.size;}};
})();
window.staffUiBusy = staffUiBusy;
const activeFormActions = new Set();
const mutatingActionNames = [
    'saveCrudData','saveProductManagementData','toggleProductVariantStatus','deleteProductVariant',
    'approveImportRequest','rejectImportRequest','deleteProduct','deleteProductType','deletePartner','deletePromotion',
    'advanceDeliveryStatus','advanceCodStatus','deleteCod','cancelDelivery','importProductRows',
    'removeProductImage','removeAllProductImages','removeCart','clearCart','confirmCardPayment','cancelQrPayment','openEditPartner',
    'createReturnRequest','handleWebReturnRequest','submitStockReview','submitStockReconcile','importPartnerRows'
];

// Gộp các request đang bay có cùng nội dung. Mỗi response được clone cho từng caller.
const originalFormFetch = window.fetch.bind(window);
const pendingFormRequests = new Map();
const failedFormRequestKeys = new Map();
window.fetch = async function(resource, options = {}) {
    const url = new URL(resource instanceof Request ? resource.url : String(resource),location.href).href;
    const method = (options.method || resource?.method || 'GET').toUpperCase();
    const pathname = new URL(url).pathname.toLowerCase().replace(/\/+$/,'');
    if (!url.startsWith(API_BASE_URL + '/api/') || pathname==='/api/dang-xuat') return originalFormFetch(resource, options);
    const writes = !['GET','HEAD','OPTIONS'].includes(method);
    if (writes && window.staffApiDataReady === false) {
        const message='Chưa xác minh được dữ liệu. Vui lòng tải lại trước khi ghi thay đổi.';
        if(typeof toast==='function')toast(message);
        throw new Error(message);
    }
    const signature = method + url + (options.body || '');
    const existing = writes ? pendingFormRequests.get(signature) : null;
    if (existing) return (await existing).clone();
    const headers = new Headers(options.headers || resource?.headers);
    if (writes && !headers.has('X-Request-ID')) headers.set('X-Request-ID', failedFormRequestKeys.get(signature) || crypto.randomUUID());
    if (writes) failedFormRequestKeys.set(signature, headers.get('X-Request-ID'));
    if (failedFormRequestKeys.size > 100) failedFormRequestKeys.delete(failedFormRequestKeys.keys().next().value);
    const background = options.staffBackground === true || (!writes && /^\/api\/thanh-toan-qr\/[^/]+$/.test(pathname));
    const finish = background ? () => {} : staffUiBusy.begin(writes ? 'Đang xử lý yêu cầu…' : 'Đang tải dữ liệu…');
    const controller = new AbortController(), externalSignal = options.signal || (resource instanceof Request ? resource.signal : null);
    const abort = () => controller.abort(externalSignal.reason);
    if (externalSignal?.aborted) abort(); else externalSignal?.addEventListener('abort',abort,{once:true});
    const timeout = setTimeout(() => controller.abort(new DOMException('Hệ thống phản hồi quá lâu. Vui lòng tải lại để kiểm tra kết quả trước khi thử lại.','TimeoutError')),writes ? 60000 : 30000);
    const promise = (async () => {
        const response = await originalFormFetch(resource, {...options, headers, signal:controller.signal});
        await response.clone().arrayBuffer();
        return response;
    })();
    if (writes) pendingFormRequests.set(signature, promise);
    try {
        const response = await promise;
        if (writes && response.ok) failedFormRequestKeys.delete(signature);
        return response.clone();
    } finally {
        if (writes) pendingFormRequests.delete(signature);
        clearTimeout(timeout); externalSignal?.removeEventListener('abort',abort); finish();
    }
};

for (const name of mutatingActionNames) {
    const action = window[name];
    if (typeof action !== 'function') continue;
    window[name] = async function(...args) {
        if (activeFormActions.has(name) || staffUiBusy.isBusy) return;
        activeFormActions.add(name);
        const buttons = [...document.querySelectorAll('button[onclick]')]
            .filter(button => button.getAttribute('onclick').includes(name + '('));
        if (name === 'saveCrudData') buttons.push(...document.querySelectorAll('#crudModalBg .modal-actions button'));
        const prior = new Map(buttons.map(button => [button, button.disabled]));
        try {
            // Để handler kiểm tra trạng thái nút ban đầu trước khi lớp dùng chung khóa nút.
            // activeFormActions đã chặn lời gọi lặp ngay từ đầu.
            const result = action.apply(this,args);
            buttons.forEach(button => {button.disabled = true; button.setAttribute('aria-busy','true');});
            return await result;
        }
        finally {
            activeFormActions.delete(name);
            for (const [button,disabled] of prior) if (button.isConnected) {button.disabled = disabled; button.removeAttribute('aria-busy');}
        }
    };
}
