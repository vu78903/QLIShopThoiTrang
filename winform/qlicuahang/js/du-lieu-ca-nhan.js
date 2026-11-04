// Bản lưu cũ giữ nguyên để quản lý xác định người nhận; không tự gán cho người đăng nhập.
const LEGACY_STORAGE_KEY = 'y_fashion_erp_state_v2';
const STAFF_STORAGE_OWNER = window.currentEmployee?.id || '';
const staffStorageKey = owner => LEGACY_STORAGE_KEY + ':account:' + encodeURIComponent(owner);
const APP_STORAGE_KEY = STAFF_STORAGE_OWNER ? staffStorageKey(STAFF_STORAGE_OWNER) : null;
const LEGACY_ASSIGNMENTS_KEY = LEGACY_STORAGE_KEY + ':assignments';
let staffRecoveringLegacy = false;

async function legacyReportIdentity(report) {
    const hash = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(JSON.stringify(report)));
    return Array.from(new Uint8Array(hash), b => b.toString(16).padStart(2, '0')).join('');
}

async function getUnclaimedLegacyState() {
    const raw = localStorage.getItem(LEGACY_STORAGE_KEY);
    if (!raw) return null;
    const data = JSON.parse(raw);
    if (!data || typeof data !== 'object' || Array.isArray(data)) throw Error('Bản lưu cũ không hợp lệ.');
    const assignments = JSON.parse(localStorage.getItem(LEGACY_ASSIGNMENTS_KEY) || '{"reports":{}}');
    const reports = [];
    for (const [index, report] of (Array.isArray(data.shiftReports) ? data.shiftReports : []).entries()) {
        const key = await legacyReportIdentity(report);
        if (!assignments.reports?.[key]) reports.push({index, key, report});
    }
    return {data, assignments, reports,
        hasCart: !assignments.cart && ((data.cart?.length || 0) > 0 || !!data.checkoutAttempt),
        hasReturn: !assignments.returnAttempt && !!data.returnAttempt};
}

async function showLegacyStorageNotice() {
    document.getElementById('legacyStorageNotice')?.remove();
    let legacy;
    try { legacy = await getUnclaimedLegacyState(); }
    catch {
        const notice = document.createElement('div'); notice.id = 'legacyStorageNotice'; notice.setAttribute('role','status');
        notice.textContent = 'Bản lưu dùng chung cũ chưa đọc được và vẫn được giữ nguyên. Liên hệ quản lý để khôi phục.';
        Object.assign(notice.style,{padding:'10px 20px',background:'#eef2ff'});document.body.prepend(notice);return;
    }
    if (!legacy || (!legacy.hasCart && !legacy.hasReturn && !legacy.reports.length)) return;
    const notice = document.createElement('div');notice.id='legacyStorageNotice';notice.setAttribute('role','status');
    Object.assign(notice.style,{padding:'10px 20px',background:'#eef2ff',display:'flex',gap:'16px',alignItems:'center'});
    const text=document.createElement('span');text.textContent='Có bản nháp/báo cáo cũ chưa xác định chủ. Dữ liệu được giữ riêng, chưa gán cho tài khoản nào.';notice.append(text);
    if(window.userRole==='admin') {
        const button=document.createElement('button');button.type='button';button.className='btn btn-secondary';button.textContent='Khôi phục dữ liệu cũ';
        button.addEventListener('click',openLegacyRecovery);notice.append(button);
    } else text.textContent+=' Liên hệ quản lý nếu cần khôi phục.';
    document.body.prepend(notice);
}

async function openLegacyRecovery() {
    if(window.userRole!=='admin' || window.staffApiDataReady!==true)return;
    const legacy=await getUnclaimedLegacyState();if(!legacy)return;
    const employees=partnersData.filter(p=>p.category==='Nhân viên');
    const options='<option value="">Chưa gán</option>'+employees.map(p=>`<option value="${escapeHtml(p.id)}">${escapeHtml(p.id)} · ${escapeHtml(p.name)}</option>`).join('');
    document.getElementById('legacyRecoveryModal')?.remove();
    document.body.insertAdjacentHTML('beforeend',`<div class="modal-bg open" id="legacyRecoveryModal" role="dialog" aria-modal="true"><div class="modal" style="max-height:85vh;overflow:auto"><h2>Khôi phục bản lưu dùng chung cũ</h2><p>Chọn đúng người nhận cho từng mục. Không tự nhận diện theo tên; không ghi đè bản nháp đang có. Bản lưu gốc vẫn được giữ nguyên.</p>
        ${legacy.hasCart?`<label>Giỏ nháp và mã đặt hàng đang dở</label><select class="input" id="legacyCartOwner">${options}</select>`:''}
        ${legacy.hasReturn?`<label>Mã yêu cầu đổi/trả đang dở</label><select class="input" id="legacyReturnOwner">${options}</select>`:''}
        ${legacy.reports.map(({index,report})=>`<label>${escapeHtml(report.shiftId || report.dateKey || 'Báo cáo')} · ${escapeHtml(report.cashier || 'Chưa rõ nhân viên')}</label><select class="input" id="legacyReportOwner_${index}">${options}</select>`).join('')}
        <p id="legacyRecoveryError" role="alert"></p><div class="modal-actions"><button class="btn btn-secondary" onclick="document.getElementById('legacyRecoveryModal').remove()">Để sau</button><button class="btn btn-primary" id="legacyRecoveryApply" onclick="applyLegacyRecovery()">Khôi phục các mục đã chọn</button></div></div></div>`);
}

async function applyLegacyRecovery() {
    if(staffRecoveringLegacy || window.userRole!=='admin')return;
    staffRecoveringLegacy=true;const button=document.getElementById('legacyRecoveryApply');if(button)button.disabled=true;
    try {
        if(!await loadAppState() || window.userRole!=='admin')throw Error('Cần xác thực lại quyền quản lý trước khi khôi phục.');
        const legacy=await getUnclaimedLegacyState();if(!legacy)return;
        const allowed=new Set(partnersData.filter(p=>p.category==='Nhân viên').map(p=>p.id));
        const staged=new Map(),before=new Map();
        function target(owner) {
            if(!allowed.has(owner))throw Error('Người nhận không còn là nhân viên đang hoạt động.');
            const key=staffStorageKey(owner);
            if(!staged.has(key)) {
                const raw=localStorage.getItem(key);before.set(key,raw);
                const value=raw?JSON.parse(raw):{staffOwnerId:owner,staffOwnerRole:'',cart:[],shiftReports:[],checkoutAttempt:null,returnAttempt:null};
                if(value.staffOwnerId!==owner)throw Error('Bản lưu đích có chủ sở hữu không khớp; chưa ghi đè.');
                staged.set(key,value);
            }
            return staged.get(key);
        }
        const assignments=JSON.parse(JSON.stringify(legacy.assignments));assignments.reports||={};
        const cartOwner=document.getElementById('legacyCartOwner')?.value;
        if(legacy.hasCart && cartOwner) {
            const value=target(cartOwner);
            if(value.cart?.length || value.checkoutAttempt)throw Error('Tài khoản đích đã có giỏ/mã đặt hàng đang dở. Chưa ghi đè.');
            value.cart=legacy.data.cart || [];value.checkoutAttempt=legacy.data.checkoutAttempt || null;value.appliedPromotion=legacy.data.appliedPromotion || null;assignments.cart=cartOwner;
        }
        const returnOwner=document.getElementById('legacyReturnOwner')?.value;
        if(legacy.hasReturn && returnOwner) {
            const value=target(returnOwner);if(value.returnAttempt)throw Error('Tài khoản đích đã có yêu cầu đổi/trả đang dở.');
            value.returnAttempt=legacy.data.returnAttempt;assignments.returnAttempt=returnOwner;
        }
        for(const {index,key,report} of legacy.reports) {
            const owner=document.getElementById('legacyReportOwner_'+index)?.value;if(!owner)continue;
            const value=target(owner);value.shiftReports||=[];
            if(!value.shiftReports.some(r=>JSON.stringify(r)===JSON.stringify(report)))value.shiftReports.push(report);
            assignments.reports[key]=owner;
        }
        if(!staged.size)throw Error('Chọn người nhận cho ít nhất một mục.');
        const oldAssignments=localStorage.getItem(LEGACY_ASSIGNMENTS_KEY),written=[];
        try {
            for(const [key,value] of staged){localStorage.setItem(key,JSON.stringify(value));written.push(key);}
            localStorage.setItem(LEGACY_ASSIGNMENTS_KEY,JSON.stringify(assignments));
        } catch(error) {
            for(const key of written.reverse()){try{const old=before.get(key);if(old===null)localStorage.removeItem(key);else localStorage.setItem(key,old);}catch{}}
            try{if(oldAssignments===null)localStorage.removeItem(LEGACY_ASSIGNMENTS_KEY);else localStorage.setItem(LEGACY_ASSIGNMENTS_KEY,oldAssignments);}catch{}
            throw Error('Chưa hoàn tất lưu khôi phục. Bản lưu cũ vẫn nguyên vẹn; kiểm tra dung lượng và thử lại.');
        }
        document.getElementById('legacyRecoveryModal')?.remove();
        staffDraftLoaded=false;await loadAppState();renderAll();await showLegacyStorageNotice();
        toast('Đã khôi phục các mục được chọn vào đúng tài khoản. Bản lưu gốc vẫn được giữ.');
    } catch(error) {
        const errorBox=document.getElementById('legacyRecoveryError');if(errorBox)errorBox.textContent=error.message;
    } finally {staffRecoveringLegacy=false;if(button)button.disabled=false;}
}
