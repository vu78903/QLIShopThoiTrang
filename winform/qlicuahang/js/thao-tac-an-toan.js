// Một lần bấm là một thao tác; mở khóa cả khi API/network lỗi.
const activeFormActions = new Set();
const mutatingActionNames = [
    'saveCrudData','saveProductManagementData','toggleProductVariantStatus','deleteProductVariant',
    'approveImportRequest','rejectImportRequest','deleteProduct','deleteProductType','deletePartner','deletePromotion',
    'advanceDeliveryStatus','advanceCodStatus','deleteCod','cancelDelivery','importProductRows',
    'removeProductImage','removeAllProductImages','removeCart','clearCart','confirmCardPayment','cancelQrPayment','openEditPartner',
    'createReturnRequest','handleWebReturnRequest','submitStockReview','submitStockReconcile'
];

// Gộp các request đang bay có cùng nội dung. Mỗi response được clone cho từng caller.
const originalFormFetch = window.fetch.bind(window);
const pendingFormRequests = new Map();
const failedFormRequestKeys = new Map();
window.fetch = async function(resource, options = {}) {
    const url = typeof resource === 'string' ? resource : resource.url;
    const method = (options.method || resource?.method || 'GET').toUpperCase();
    if (!url.startsWith(API_BASE_URL + '/api/') || ['GET','OPTIONS'].includes(method)) return originalFormFetch(resource, options);
    const signature = method + url + (options.body || '');
    const existing = pendingFormRequests.get(signature);
    if (existing) return (await existing).clone();
    const headers = new Headers(options.headers || resource?.headers);
    if (!headers.has('X-Request-ID')) headers.set('X-Request-ID', failedFormRequestKeys.get(signature) || crypto.randomUUID());
    failedFormRequestKeys.set(signature, headers.get('X-Request-ID'));
    if (failedFormRequestKeys.size > 100) failedFormRequestKeys.delete(failedFormRequestKeys.keys().next().value);
    const promise = originalFormFetch(resource, {...options, headers});
    pendingFormRequests.set(signature, promise);
    try {
        const response = await promise;
        if (response.ok) failedFormRequestKeys.delete(signature);
        return response.clone();
    } finally { pendingFormRequests.delete(signature); }
};

for (const name of mutatingActionNames) {
    const action = window[name];
    if (typeof action !== 'function') continue;
    window[name] = async function(...args) {
        if (activeFormActions.has(name)) return;
        activeFormActions.add(name);
        const buttons = [...document.querySelectorAll('button[onclick]')]
            .filter(button => button.getAttribute('onclick').includes(name + '('));
        if (name === 'saveCrudData') buttons.push(...document.querySelectorAll('#crudModalBg .modal-actions button'));
        const prior = new Map(buttons.map(button => [button, button.disabled]));
        buttons.forEach(button => {button.disabled = true; button.setAttribute('aria-busy','true');});
        try { return await action.apply(this,args); }
        finally {
            activeFormActions.delete(name);
            for (const [button,disabled] of prior) if (button.isConnected) {button.disabled = disabled; button.removeAttribute('aria-busy');}
        }
    };
}
