(function () {
    "use strict";
    var wrapper = document.querySelector(".account-address-grid");
    if (!wrapper) return;
    var province = document.getElementById("ProvinceCode");
    var ward = document.getElementById("WardCode");
    var status = document.getElementById("location-status");
    var submit = document.getElementById("register-submit");
    var oldProvince = wrapper.getAttribute("data-province-selected") || "";
    var oldWard = wrapper.getAttribute("data-ward-selected") || "";
    var requestNo = 0;

    function options(select, list, placeholder) {
        select.replaceChildren();
        var empty = document.createElement("option");
        empty.value = "";
        empty.textContent = placeholder;
        select.appendChild(empty);
        list.forEach(function (item) {
            if (!item || !item.Code || !item.Name) return;
            var opt = document.createElement("option");
            opt.value = String(item.Code);
            opt.textContent = item.Name;
            select.appendChild(opt);
        });
    }
    async function fetchList(url) {
        var response = await fetch(url, { headers: { "Accept": "application/json" }, credentials: "same-origin" });
        if (!response.ok) throw new Error("Dịch vụ địa chỉ hiện không sẵn sàng.");
        var list = await response.json();
        if (!Array.isArray(list)) throw new Error("Dữ liệu địa chỉ không hợp lệ.");
        return list;
    }
    async function loadWards(code, selected) {
        var current = ++requestNo;
        options(ward, [], "Đang tải phường/xã...");
        ward.disabled = true;
        submit.disabled = true;
        if (!code) { options(ward, [], "Chọn tỉnh/thành phố trước"); status.textContent = "Vui lòng chọn tỉnh/thành phố."; return; }
        status.textContent = "Đang tải phường/xã...";
        try {
            var url = wrapper.getAttribute("data-wards-url") + "?provinceCode=" + encodeURIComponent(code);
            var list = await fetchList(url);
            if (current !== requestNo) return;
            options(ward, list, "Chọn phường/xã");
            if (selected && list.some(function (x) { return String(x.Code) === selected; })) ward.value = selected;
            ward.disabled = false;
            submit.disabled = false;
            status.textContent = "Vui lòng chọn phường/xã nơi nhận hàng.";
        } catch (error) {
            if (current !== requestNo) return;
            options(ward, [], "Không tải được phường/xã");
            status.textContent = "Không tải được phường/xã. Hãy kiểm tra kết nối mạng và tải lại trang.";
        }
    }
    province.addEventListener("change", function () { loadWards(province.value, ""); });
    (async function () {
        options(province, [], "Đang tải tỉnh/thành phố...");
        province.disabled = true;
        try {
            var list = await fetchList(wrapper.getAttribute("data-provinces-url"));
            options(province, list, "Chọn tỉnh/thành phố");
            province.disabled = false;
            if (oldProvince && list.some(function (x) { return String(x.Code) === oldProvince; })) {
                province.value = oldProvince;
                await loadWards(oldProvince, oldWard);
            } else {
                status.textContent = "Chọn tỉnh/thành phố để tải phường/xã.";
            }
        } catch (error) {
            options(province, [], "Không tải được tỉnh/thành phố");
            status.textContent = "Không tải được tỉnh/thành phố. Hãy kiểm tra kết nối mạng và tải lại trang.";
        }
    }());
}());
