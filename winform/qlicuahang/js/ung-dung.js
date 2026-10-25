// 1. DỮ LIỆU TẠM TRÊN TRÌNH DUYỆT
        // Các mảng dưới đây đang đóng vai trò như "database nhỏ" khi chạy demo.
        // Sau này nếu có SQL Server/API thì chỉ cần thay phần đọc ghi dữ liệu ở đây.
        let productsData = [];
        let productTypesData = [];
        let partnersData = [];
        let promotionsData = [];
        let deliveryOrders = [];
        let codOrders = [];
        let importRequests = [];
        let cart = [];
        let salesHistory = [];
        let shiftReports = [];
        let appliedPromotion = null;
        let lastReceipt = null;
        let pendingQrReceipt = null;
        let pendingCardReceipt = null;
        let pendingProductImage = null;
        let deleteConfirmationResolver = null;
        let invoiceSearchTimer = null;
        let invoiceSearchRequestId = 0;
        let productSearchTimer = null;
        let returnSearchTimer = null;
        let returnSearchRequestId = 0;
        let selectedReturnInvoice = null;
        let selectedReturnSku = "";
        let lastReturnReceipt = null;
        let deliveryCustomerAutoFilled = false;
        let crudContext = { action: '', target: '', editIndex: -1 };
        let revenueChartInstance = null;
        let topChartInstance = null;
        const APP_STORAGE_KEY = "y_fashion_erp_state_v1";
        const API_BASE_URL = "http://127.0.0.1:5000";
        const VAT_RATE = 8;
        const ADDRESS_OPTIONS = {
            "Thành phố Hồ Chí Minh": ["Tân Sơn Nhì", "Tây Thạnh", "Sơn Kỳ", "Phú Thọ Hòa", "Tân Quý", "Tân Thành", "Hòa Thạnh", "Phú Trung", "Hiệp Tân", "Tân Thới Hòa"],
            "Thành phố Hà Nội": ["Ba Đình", "Hoàn Kiếm", "Cửa Nam", "Hai Bà Trưng", "Đống Đa", "Cầu Giấy", "Thanh Xuân", "Hà Đông"],
            "Thành phố Đà Nẵng": ["Hải Châu", "Thanh Khê", "Sơn Trà", "Ngũ Hành Sơn", "Liên Chiểu", "Cẩm Lệ"]
        };

        // Hiện một hộp xác nhận thống nhất trước mọi thao tác xóa.
        function confirmDeletion(message, title = "Xác nhận xóa") {
            if (deleteConfirmationResolver) deleteConfirmationResolver(false);
            if (!document.getElementById("deleteConfirmModalBg")) {
                document.body.insertAdjacentHTML("beforeend", `
                    <div class="modal-bg" id="deleteConfirmModalBg" role="alertdialog" aria-modal="true" aria-labelledby="deleteConfirmTitle" aria-describedby="deleteConfirmMessage">
                        <div class="modal delete-confirm-modal">
                            <div class="delete-confirm-icon"><i data-lucide="triangle-alert"></i></div>
                            <h2 id="deleteConfirmTitle">Xác nhận xóa</h2>
                            <p id="deleteConfirmMessage"></p>
                            <div class="modal-actions">
                                <button class="btn btn-secondary" id="deleteCancelButton" type="button" onclick="resolveDeleteConfirmation(false)">Không xóa</button>
                                <button class="btn btn-danger" type="button" onclick="resolveDeleteConfirmation(true)"><i data-lucide="trash-2"></i> Xóa</button>
                            </div>
                        </div>
                    </div>`);
                if (window.lucide) lucide.createIcons();
            }
            document.getElementById("deleteConfirmTitle").textContent = title;
            document.getElementById("deleteConfirmMessage").textContent = message;
            document.getElementById("deleteConfirmModalBg").classList.add("open");
            setTimeout(() => document.getElementById("deleteCancelButton")?.focus(), 30);
            return new Promise(resolve => {
                deleteConfirmationResolver = resolve;
            });
        }

        function resolveDeleteConfirmation(confirmed) {
            document.getElementById("deleteConfirmModalBg")?.classList.remove("open");
            const resolver = deleteConfirmationResolver;
            deleteConfirmationResolver = null;
            resolver?.(confirmed);
        }

        // Lấy dữ liệu từ API trước, nếu API chưa chạy thì dùng lại localStorage để app vẫn mở được.
        async function loadAppState() {
            try {
                const raw = localStorage.getItem(APP_STORAGE_KEY);
                if (raw) {
                    const data = JSON.parse(raw);
                    productsData = Array.isArray(data.productsData) ? data.productsData : [];
                    productTypesData = Array.isArray(data.productTypesData) ? data.productTypesData : [];
                    partnersData = Array.isArray(data.partnersData) ? data.partnersData : [];
                    promotionsData = Array.isArray(data.promotionsData) ? data.promotionsData : [];
                    deliveryOrders = Array.isArray(data.deliveryOrders) ? data.deliveryOrders : [];
                    codOrders = Array.isArray(data.codOrders) ? data.codOrders : [];
                    importRequests = Array.isArray(data.importRequests) ? data.importRequests : [];
                    cart = Array.isArray(data.cart) ? data.cart : [];
                    salesHistory = Array.isArray(data.salesHistory) ? data.salesHistory : [];
                    shiftReports = Array.isArray(data.shiftReports) ? data.shiftReports : [];
                    appliedPromotion = data.appliedPromotion || null;
                }
            } catch {
                localStorage.removeItem(APP_STORAGE_KEY);
            }

            try {
                const response = await fetch(`${API_BASE_URL}/api/app-state`, { cache: "no-store" });
                if (!response.ok) throw new Error("API app-state failed");
                const data = await response.json();
                productsData = Array.isArray(data.productsData) ? data.productsData : [];
                productTypesData = Array.isArray(data.productTypesData) ? data.productTypesData : [];
                partnersData = Array.isArray(data.partnersData) ? data.partnersData : [];
                promotionsData = Array.isArray(data.promotionsData) ? data.promotionsData : [];
                importRequests = Array.isArray(data.importRequests) ? data.importRequests : [];
                salesHistory = Array.isArray(data.salesHistory) ? data.salesHistory : salesHistory;
                deliveryOrders = Array.isArray(data.deliveryOrders) ? data.deliveryOrders : [];
                codOrders = Array.isArray(data.codOrders) ? data.codOrders : codOrders;
                saveAppState();
            } catch (error) {
                console.warn("Không tải được dữ liệu từ API, dùng dữ liệu localStorage.", error);
            }
        }

        // Lưu dữ liệu demo để các trang HTML riêng vẫn dùng chung một trạng thái.
        function saveAppState() {
            const data = {
                productsData,
                productTypesData,
                partnersData,
                promotionsData,
                deliveryOrders,
                codOrders,
                importRequests,
                cart,
                salesHistory,
                shiftReports,
                appliedPromotion
            };
            localStorage.setItem(APP_STORAGE_KEY, JSON.stringify(data));
        }

        // 2. HIỂN THỊ DỮ LIỆU LÊN MÀN HÌNH
        // Nhóm hàm render chỉ lo vẽ lại bảng/thẻ theo dữ liệu hiện có.
        // Khi thêm, sửa, xóa xong thì gọi render lại để giao diện cập nhật.
        function renderProducts() {
            const tbody = document.querySelector("#view-products tbody");
            if (!tbody) return;

            const searchInput = document.getElementById("productSearchInput");
            const searchText = searchInput?.value || "";
            const filteredProducts = productsData.filter(product => matchesProductSearch(product, searchText));
            const summary = document.getElementById("productSearchSummary");
            const clearButton = document.getElementById("productSearchClear");
            const isSearching = Boolean(searchText.trim());
            if (summary) {
                summary.textContent = isSearching
                    ? `Tìm thấy ${filteredProducts.length}/${productsData.length} SKU`
                    : `${productsData.length} SKU`;
            }
            if (clearButton) clearButton.style.display = isSearching ? "inline-flex" : "none";

            if (productsData.length === 0) {
                tbody.innerHTML = `<tr><td colspan="10" style="text-align:center; padding:30px; color:#94a3b8;">Chưa có sản phẩm nào. Hãy bấm "+ Thêm Sản phẩm".</td></tr>`;
                return;
            }

            if (filteredProducts.length === 0) {
                tbody.innerHTML = `<tr><td colspan="10" style="text-align:center; padding:30px; color:#94a3b8;">Không tìm thấy sản phẩm theo Mã SP, Mã SKU hoặc tên đã nhập.</td></tr>`;
                if (window.lucide) lucide.createIcons();
                updateInventoryKpi();
                return;
            }

            tbody.innerHTML = filteredProducts.map(p => {
                const idx = productsData.indexOf(p);
                return `
                    <tr>
                        <td>${p.image ? `<img class="product-thumb" src="${escapeHtml(productImageUrl(p.image))}" alt="Ảnh ${escapeHtml(p.name)}">` : `<span class="product-no-image"><i data-lucide="image"></i></span>`}</td>
                        <td><strong>${escapeHtml(p.productId || p.sku)}</strong></td>
                        <td><strong>${escapeHtml(p.sku)}</strong></td>
                        <td>${escapeHtml(p.name)}</td>
                        <td><span class="badge blue">${escapeHtml(p.type || "Chưa phân loại")}</span></td>
                        <td><span class="badge">${escapeHtml(p.color || "Mặc định")}</span></td>
                        <td><span class="badge">${escapeHtml(p.size || "Free size")}</span></td>
                        <td>${new Intl.NumberFormat('vi-VN').format(p.price)} ₫</td>
                        <td><span class="badge ${p.stock <= 0 ? 'red' : (p.stock < 10 ? 'red' : 'green')}">${p.stock}</span></td>
                        <td>
                            <button class="icon-button" title="${p.stock <= 0 ? 'Hết hàng' : 'Thêm vào giỏ'}" ${p.stock <= 0 ? "disabled" : ""} onclick="addToCartBySku('${p.sku}')"><i data-lucide="shopping-cart" style="width:16px;color:#2563eb"></i></button>
                            <button class="icon-button" title="Chỉnh sửa" onclick="openEditProduct(${idx})"><i data-lucide="pencil" style="width:16px"></i></button>
                            <button class="icon-button" title="Xóa" onclick="deleteProduct(${idx})"><i data-lucide="trash-2" style="width:16px;color:#ef4444"></i></button>
                        </td>
                    </tr>
                `;
            }).join("");
            if (window.lucide) lucide.createIcons();
            updateInventoryKpi();
        }

        function normalizeProductSearchText(value) {
            return normalizeSkuText(value).replace(/[^A-Z0-9]+/g, " ").trim();
        }

        function matchesProductSearch(product, keyword) {
            const terms = normalizeProductSearchText(keyword).split(/\s+/).filter(Boolean);
            if (terms.length === 0) return true;
            const searchableFields = [product.productId, product.sku, product.name]
                .map(normalizeProductSearchText);
            return terms.every(term => searchableFields.some(field => field.includes(term)));
        }

        function clearProductSearch() {
            clearTimeout(productSearchTimer);
            const input = document.getElementById("productSearchInput");
            if (input) {
                input.value = "";
                input.focus();
            }
            renderProducts();
        }

        function scheduleProductSearch() {
            clearTimeout(productSearchTimer);
            productSearchTimer = setTimeout(renderProducts, 300);
        }

        // Đường dẫn ảnh trong database là đường dẫn tương đối do API cung cấp.
        function productImageUrl(image) {
            if (!image) return "";
            return image.startsWith("/") ? `${API_BASE_URL}${image}` : image;
        }

        // Vẽ bảng loại sản phẩm như Áo, Quần, Váy...
        function renderProductTypes() {
            const tbody = document.getElementById("productTypeBody");
            if (!tbody) return;

            if (productTypesData.length === 0) {
                tbody.innerHTML = `<tr><td colspan="4" style="text-align:center; padding:30px; color:#94a3b8;">Chưa có loại sản phẩm nào. Bấm "+ Thêm loại" để tạo.</td></tr>`;
                return;
            }

            tbody.innerHTML = productTypesData.map((type, idx) => {
                const count = productsData.filter(product => product.type === type.name).length;
                return `
                    <tr>
                        <td><strong>${type.id}</strong></td>
                        <td>${type.name}</td>
                        <td>${count}</td>
                        <td>
                            <button class="icon-button" title="Chỉnh sửa" onclick="openEditProductType(${idx})"><i data-lucide="pencil" style="width:16px"></i></button>
                            <button class="icon-button" title="Xóa" onclick="deleteProductType(${idx})"><i data-lucide="trash-2" style="width:16px;color:#ef4444"></i></button>
                        </td>
                    </tr>
                `;
            }).join("");
            if (window.lucide) lucide.createIcons();
        }

        // Vẽ lại các phần đang có trên trang hiện tại sau khi dữ liệu đổi.
        function renderAll() {
            renderProducts();
            renderProductTypes();
            renderPartners();
            renderCod();
            renderInventoryTable();
            renderImportRequests();
            renderPromotions();
            renderCart();
            renderReport();
            updateInventoryKpi();
            if (window.lucide) lucide.createIcons();
        }

        let currentPartnerCategory = "Nhà cung cấp"; // Khách mới vẫn chỉ được thêm nhanh ở POS.

        // Đổi tab Nhà cung cấp / Nhân viên.
        function switchPartnerCategory(category, btn) {
            currentPartnerCategory = category;
            document.querySelectorAll(".partner-tab").forEach(t => t.classList.remove("active"));
            if (btn) btn.classList.add("active");
            const customerTab = category === "Khách hàng";
            document.getElementById("partnerAddButton")?.classList.toggle("hidden", customerTab);
            document.getElementById("partnerImportButton")?.classList.toggle("hidden", customerTab);
            renderPartners();
        }

        // Lấy tổng tiền khách đã mua, dùng để xét hạng thành viên.
        function getCustomerTotalSpent(customer) {
            return Number(customer?.totalSpent) || 0;
        }

        // Lấy điểm tích lũy hiện tại của khách.
        function getCustomerPoints(customer) {
            return Number(customer?.points) || 0;
        }

        // Tự xếp hạng thành viên theo tổng chi tiêu, không cho chọn tay.
        function getCustomerRank(customer) {
            const totalSpent = getCustomerTotalSpent(customer);
            if (totalSpent > 15000000) return "Kim Cương";
            if (totalSpent > 5000000) return "Vàng";
            return "Bạc";
        }

        // Trả về % giảm giá theo hạng khách hàng.
        function getCustomerDiscountRate(customer) {
            const rank = getCustomerRank(customer);
            if (rank === "Kim Cương") return 10;
            if (rank === "Vàng") return 5;
            return 0;
        }

        // Tìm khách hàng bằng số điện thoại nhập ở POS.
        function findCustomerByPhone(phone) {
            const normalized = String(phone || "").replace(/\D/g, "");
            if (!normalized) return null;
            return partnersData.find(p => p.category === "Khách hàng" && p.phone === normalized) || null;
        }

        // Vẽ danh sách khách hàng, nhà cung cấp hoặc nhân viên theo tab đang chọn.
        function renderPartners() {
            const tbody = document.getElementById("partnerBody");
            if (!tbody) return;

            const keyword = (document.getElementById("partnerSearch")?.value || "").trim().toLowerCase();
            const filtered = partnersData.filter(p => p.category === currentPartnerCategory && (!keyword ||
                [p.id, p.name, p.phone, p.email].some(value => String(value || "").toLowerCase().includes(keyword))));
            const showsExtra = currentPartnerCategory === "Nhân viên" || currentPartnerCategory === "Khách hàng";
            const extraHeader = document.getElementById("partnerRoleHeader");
            extraHeader?.classList.toggle("hidden", !showsExtra);
            if (extraHeader) extraHeader.textContent = currentPartnerCategory === "Khách hàng" ? "Điểm / hạng" : "Quyền truy cập";

            if (filtered.length === 0) {
                tbody.innerHTML = `<tr><td colspan="${showsExtra ? 8 : 7}" style="text-align:center; padding:30px; color:#94a3b8;">Không tìm thấy ${currentPartnerCategory.toLowerCase()} phù hợp.</td></tr>`;
                return;
            }

            tbody.innerHTML = filtered.map((p, idx) => {
                const originalIndex = partnersData.findIndex(item => item.id === p.id);
                const badgeColor = p.category === "Khách hàng" ? "amber" : (p.category === "Nhà cung cấp" ? "blue" : "green");

                return `
            <tr>
                <td><strong>${p.id}</strong></td>
                <td>${p.name}</td>
                <td>${p.phone}</td>
                <td>${p.email || '-'}</td>
                <td>${p.address}</td>
                <td><span class="badge ${badgeColor}">${p.category}</span></td>
                ${showsExtra ? `<td>${p.category === "Khách hàng" ? `${getCustomerPoints(p)} điểm · ${getCustomerRank(p)}` : (p.role || "-")}</td>` : ""}
                <td>
                    <button class="icon-button" title="Chỉnh sửa" onclick="openEditPartner(${originalIndex})"><i data-lucide="pencil" style="width:16px"></i></button>
                    <button class="icon-button" title="${p.category === 'Nhà cung cấp' ? 'Dừng nhà cung cấp' : 'Dừng hoạt động'}" onclick="deletePartner(${originalIndex})"><i data-lucide="user-x" style="width:16px;color:#ef4444"></i></button>
                </td>
            </tr>
        `;
            }).join("");

            if (window.lucide) lucide.createIcons();
        }

        // Chọn màu nhãn trạng thái COD để nhìn nhanh đơn đang ở bước nào.
        function getCodStatusBadge(status) {
            if (status === "Hàng giao thành công") return "green";
            if (status === "Hàng đang được vận chuyển") return "blue";
            if (status === "Shipper đã lấy hàng") return "amber";
            return "gray";
        }

        // Quy định thứ tự chuyển trạng thái của đơn giao hàng.
        function getNextCodStatus(status) {
            const flow = ["Vừa xác nhận đơn", "Shipper đã lấy hàng", "Hàng đang được vận chuyển", "Hàng giao thành công"];
            const idx = flow.indexOf(status);
            return flow[Math.min(idx + 1, flow.length - 1)] || flow[0];
        }

        // Trừ tồn kho theo các sản phẩm trong hóa đơn, tránh trừ lặp bằng cờ stockDeducted.
        function deductReceiptStock(receipt) {
            if (!receipt || receipt.stockDeducted) return;
            receipt.items.forEach(item => {
                const product = productsData.find(x => x.sku === item.sku);
                if (product) product.stock = Math.max(0, (Number(product.stock) || 0) - item.qty);
            });
            receipt.stockDeducted = true;
        }

        // Cộng điểm và tổng chi tiêu cho khách sau khi hóa đơn thật sự hoàn tất.
        function applyCustomerLoyalty(receipt) {
            if (!receipt || receipt.loyaltyApplied) return;
            const customer = findCustomerByPhone(receipt.customer?.phone);
            if (!customer) return;
            customer.totalSpent = getCustomerTotalSpent(customer) + (Number(receipt.total) || 0);
            customer.points = getCustomerPoints(customer) + (Number(receipt.earnedPoints) || 0);
            receipt.customer.rank = getCustomerRank(customer);
            receipt.customer.points = getCustomerPoints(customer);
            receipt.customer.totalSpent = getCustomerTotalSpent(customer);
            receipt.loyaltyApplied = true;
        }

        // Vẽ các đơn đã thanh toán, shipper chỉ cần lấy hàng và giao.
        function renderDelivery() {
            const tbody = document.getElementById("deliveryBody");
            if (!tbody) return;
            const waiting = deliveryOrders.filter(order => order.status === "Vừa xác nhận đơn").length;
            const moving = deliveryOrders.filter(order => order.status === "Shipper đã lấy hàng" || order.status === "Hàng đang được vận chuyển").length;
            if (document.getElementById("deliveryTotalCount")) document.getElementById("deliveryTotalCount").textContent = deliveryOrders.length;
            if (document.getElementById("deliveryWaitingCount")) document.getElementById("deliveryWaitingCount").textContent = waiting;
            if (document.getElementById("deliveryMovingCount")) document.getElementById("deliveryMovingCount").textContent = moving;
            if (deliveryOrders.length === 0) {
                tbody.innerHTML = `<tr><td colspan="8" class="table-message">Chưa có đơn chờ giao.</td></tr>`;
                return;
            }
            tbody.innerHTML = deliveryOrders.map((order, idx) => `
                <tr>
                    <td><strong>${escapeHtml(order.id)}</strong><small class="cell-note">${escapeHtml(order.source || "POS")}</small></td>
                    <td>${escapeHtml(order.name)}<small class="cell-note">${escapeHtml(order.phone || "-")}</small></td>
                    <td>${escapeHtml(order.address)}</td>
                    <td class="shipment-note">${escapeHtml(order.note || "Không có")}</td>
                    <td class="money-cell">${money(order.shippingFee || 0)}</td>
                    <td class="money-cell"><strong>0 ₫</strong><small class="cell-note paid-note">Đã thanh toán</small></td>
                    <td><span class="badge ${getCodStatusBadge(order.status)}">${escapeHtml(order.status)}</span></td>
                    <td><button class="btn btn-primary btn-compact" onclick="advanceDeliveryStatus(${idx})" ${order.status === "Hàng giao thành công" ? "disabled" : ""}>${escapeHtml(getNextStatusAction(order.status))}</button></td>
                </tr>`).join("");
            if (window.lucide) lucide.createIcons();
        }

        // Vẽ bảng COD để nhân viên theo dõi và biết chính xác số tiền phải thu hộ.
        function renderCod() {
            const tbody = document.getElementById("codBody");
            if (!tbody) return;
            const amount = codOrders.reduce((sum, order) => sum + Number(order.amountToCollect ?? order.receipt?.total ?? 0), 0);
            if (document.getElementById("codTotalCount")) document.getElementById("codTotalCount").textContent = codOrders.length;
            if (document.getElementById("codWaitingCount")) document.getElementById("codWaitingCount").textContent = codOrders.filter(order => order.status === "Vừa xác nhận đơn").length;
            if (document.getElementById("codCollectTotal")) document.getElementById("codCollectTotal").textContent = money(amount);

            if (codOrders.length === 0) {
                tbody.innerHTML = `<tr><td colspan="8" class="table-message">Chưa có đơn hàng COD nào.</td></tr>`;
                return;
            }

            tbody.innerHTML = codOrders.map((x, idx) => `
                    <tr>
                        <td><strong>${escapeHtml(x.id)}</strong><small class="cell-note">${escapeHtml(x.source || "POS")}</small></td>
                        <td>${escapeHtml(x.name)}<small class="cell-note">${escapeHtml(x.phone || "-")}</small></td>
                        <td>${escapeHtml(x.address)}</td>
                        <td class="shipment-note">${escapeHtml(x.note || "Không có")}</td>
                        <td class="money-cell">${money(x.shippingFee || 0)}</td>
                        <td class="money-cell cod-collect"><strong>${money(x.amountToCollect ?? x.receipt?.total ?? 0)}</strong></td>
                        <td><span class="badge ${getCodStatusBadge(x.status)}">${escapeHtml(x.status)}</span></td>
                        <td>
                            <button class="btn btn-primary btn-compact" onclick="advanceCodStatus(${idx})" ${x.status === "Hàng giao thành công" ? "disabled" : ""}>${escapeHtml(getNextStatusAction(x.status))}</button>
                            <button class="icon-button" title="Xóa đơn COD" onclick="deleteCod(${idx})"><i data-lucide="trash-2"></i></button>
                        </td>
                    </tr>
                `).join("");
            if (window.lucide) lucide.createIcons();
        }

        function getNextStatusAction(status) {
            if (status === "Vừa xác nhận đơn") return "Xác nhận shipper lấy hàng";
            if (status === "Shipper đã lấy hàng") return "Bắt đầu vận chuyển";
            if (status === "Hàng đang được vận chuyển") return "Xác nhận giao thành công";
            return "Đã hoàn tất";
        }

        // Hai loại đơn dùng chung một form, chỉ đổi bảng đang hiển thị.
        function setShipmentTab(mode) {
            const showCod = mode === "cod";
            document.getElementById("deliveryPanel")?.classList.toggle("hidden", showCod);
            document.getElementById("codPanel")?.classList.toggle("hidden", !showCod);
            document.getElementById("shipmentTabDelivery")?.classList.toggle("active", !showCod);
            document.getElementById("shipmentTabCod")?.classList.toggle("active", showCod);
            document.getElementById("shipmentTabDelivery")?.setAttribute("aria-selected", String(!showCod));
            document.getElementById("shipmentTabCod")?.setAttribute("aria-selected", String(showCod));
            if (location.hash !== `#${mode}`) history.replaceState(null, "", `#${mode}`);
            renderDelivery();
            renderCod();
        }

        async function updateShipmentStatus(order, nextStatus) {
            const response = await fetch(`${API_BASE_URL}/api/hoa-don/${encodeURIComponent(order.receipt?.id || order.id)}/giao-hang`, {
                method: "PUT",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ status: nextStatus, employeeId: getCurrentEmployee()?.id || null })
            });
            if (!response.ok) throw new Error(await getApiErrorMessage(response, "Không cập nhật được trạng thái giao hàng."));
        }

        async function advanceDeliveryStatus(idx) {
            const order = deliveryOrders[idx];
            if (!order) return;
            const nextStatus = getNextCodStatus(order.status);
            try {
                await updateShipmentStatus(order, nextStatus);
            } catch (error) {
                console.error(error);
                toast(error.message || "Không cập nhật được đơn giao hàng");
                return;
            }
            order.status = nextStatus;
            if (nextStatus === "Hàng giao thành công") deliveryOrders.splice(idx, 1);
            renderDelivery();
            toast(nextStatus === "Hàng giao thành công" ? "Đơn hàng đã giao thành công" : "Đã cập nhật: " + nextStatus);
        }

        async function refreshShipments() {
            await loadAppState();
            renderDelivery();
            renderCod();
            toast("Đã tải đơn mới nhất từ Supabase");
        }

        // Bấm "Cập nhật" thì đơn COD đi sang bước tiếp theo.
        // Khi giao thành công mới tính doanh thu, trừ kho và tích điểm.
        async function advanceCodStatus(idx) {
            const order = codOrders[idx];
            if (!order) return;
            const nextStatus = getNextCodStatus(order.status);
            try {
                await updateShipmentStatus(order, nextStatus);
            } catch (error) {
                console.error(error);
                toast("Không cập nhật được đơn COD vào cơ sở dữ liệu");
                return;
            }
            order.status = nextStatus;
            if (nextStatus === "Hàng giao thành công") {
                deductReceiptStock(order.receipt);
                applyCustomerLoyalty(order.receipt);
                recordPaidReceipt(order.receipt, "COD");
                codOrders.splice(idx, 1);
                toast("Đơn COD đã giao thành công, đã cập nhật tồn kho và tích điểm");
            } else {
                toast("Đã cập nhật trạng thái: " + nextStatus);
            }
            renderCod();
            renderProducts();
            renderPartners();
            updateInventoryKpi();
        }

        // Xóa đơn COD chưa hoàn tất nếu nhập nhầm.
        async function deleteCod(idx) {
            if (!codOrders[idx]) return;
            if (codOrders[idx].status === "Hàng giao thành công") {
                toast("Đơn đã giao thành công không nên xóa khỏi lịch sử");
                return;
            }
            if (!await confirmDeletion("Đơn COD này sẽ bị xóa khỏi danh sách và không thể khôi phục. Bạn có chắc muốn xóa?", "Xóa đơn COD?")) return;
            try {
                const response = await fetch(`${API_BASE_URL}/api/hoa-don/${encodeURIComponent(codOrders[idx].receipt.id)}/cod`, { method: "DELETE" });
                if (!response.ok) throw new Error(await response.text());
            } catch (error) {
                console.error(error);
                toast("Không xóa được đơn COD trong cơ sở dữ liệu");
                return;
            }
            codOrders.splice(idx, 1);
            renderCod();
            toast("Đã xóa đơn COD");
        }

        // Cập nhật các ô tổng tồn, giá trị tồn và số SKU sắp hết hàng.
        function updateInventoryKpi() {
            const totalQty = productsData.reduce((s, p) => s + (Number(p.stock) || 0), 0);
            const totalValue = productsData.reduce((s, p) => s + ((Number(p.price) || 0) * (Number(p.stock) || 0)), 0);
            const lowStock = productsData.filter(p => p.stock < 10).length;
            if (document.getElementById("invTotalQty")) document.getElementById("invTotalQty").textContent = totalQty + " sp";
            if (document.getElementById("invTotalCost")) document.getElementById("invTotalCost").textContent = money(totalValue);
            if (document.getElementById("invLowStock")) document.getElementById("invLowStock").textContent = lowStock + " SKU";
            if (document.getElementById("invTotalSku")) document.getElementById("invTotalSku").textContent = productsData.length + " SKU";
            renderInventoryTable();
            renderImportRequests();
        }

        // Vẽ bảng tồn kho để nhân viên kho nhìn rõ mã nào còn nhiều, mã nào sắp hết.
        function renderInventoryTable() {
            const tbody = document.getElementById("inventoryBody");
            if (!tbody) return;

            if (productsData.length === 0) {
                tbody.innerHTML = `<tr><td colspan="6" style="text-align:center; padding:30px; color:#94a3b8;">Chưa có sản phẩm trong kho.</td></tr>`;
                return;
            }

            tbody.innerHTML = productsData.map(product => {
                const stock = Number(product.stock) || 0;
                const status = stock < 10 ? "Sắp hết hàng" : "Còn hàng";
                const badge = stock < 10 ? "red" : "green";
                return `
                    <tr>
                        <td><strong>${escapeHtml(product.sku)}</strong></td>
                        <td>${escapeHtml(product.name)}</td>
                        <td><span class="badge blue">${escapeHtml(product.type || "Chưa phân loại")}</span></td>
                        <td>${money(Number(product.price) || 0)}</td>
                        <td><span class="badge ${badge}">${stock}</span></td>
                        <td><span class="badge ${badge}">${status}</span></td>
                    </tr>
                `;
            }).join("");
        }

        // Vẽ danh sách phiếu nhập kho chờ duyệt/đã duyệt.
        function renderImportRequests() {
            const tbody = document.getElementById("importRequestBody");
            if (!tbody) return;

            if (importRequests.length === 0) {
                tbody.innerHTML = `<tr><td colspan="9" style="text-align:center; padding:30px; color:#94a3b8;">Chưa có phiếu nhập kho nào.</td></tr>`;
                return;
            }

            const canApprove = window.userRole === "storeManager";
            tbody.innerHTML = importRequests.map((request, idx) => {
                const badge = request.status === "Đã duyệt" ? "green" : (request.status === "Từ chối" ? "red" : "amber");
                const action = request.status === "Chờ duyệt" && canApprove
                    ? `<button class="btn btn-primary" type="button" onclick="approveImportRequest(${idx})">Duyệt</button>
                       <button class="btn btn-danger" type="button" onclick="rejectImportRequest(${idx})">Từ chối</button>`
                    : "-";
                return `
                    <tr>
                        <td><strong>${escapeHtml(request.id)}</strong></td>
                        <td>${escapeHtml(request.supplierName || "-")}</td>
                        <td>${escapeHtml(request.sku)} - ${escapeHtml(request.productName)}</td>
                        <td>${request.qty}</td>
                        <td>${escapeHtml(request.createdBy || "-")}</td>
                        <td>${escapeHtml(request.createdAt || "-")}</td>
                        <td><span class="badge ${badge}">${escapeHtml(request.status)}</span></td>
                        <td>${escapeHtml(request.note || "-")}</td>
                        <td><div style="display:flex; gap:8px; flex-wrap:wrap">${action}</div></td>
                    </tr>
                `;
            }).join("");
        }

        // Tạo option sản phẩm cho phiếu nhập kho.
        function buildImportProductOptions(selectedSku = "") {
            if (productsData.length === 0) return `<option value="">Chưa có sản phẩm</option>`;
            return `<option value="">Chọn sản phẩm cần nhập</option>` + productsData.map(product =>
                `<option value="${product.sku}" ${selectedSku === product.sku ? "selected" : ""}>${product.sku} - ${product.name} (tồn ${product.stock})</option>`
            ).join("");
        }

        // Tạo option nhà cung cấp cho phiếu nhập kho.
        function buildImportSupplierOptions(selectedSupplierId = "") {
            const suppliers = partnersData.filter(partner => partner.category === "Nhà cung cấp");
            if (suppliers.length === 0) return `<option value="">Chưa có nhà cung cấp</option>`;
            return `<option value="">Chọn nhà cung cấp</option>` + suppliers.map(supplier =>
                `<option value="${supplier.id}" ${selectedSupplierId === supplier.id ? "selected" : ""}>${supplier.id} - ${supplier.name}</option>`
            ).join("");
        }

        // Mở form tạo phiếu nhập kho. Nhân viên kho chỉ tạo phiếu, chưa tự cộng tồn.
        function openAddImportRequest() {
            if (productsData.length === 0) {
                alert("Chưa có sản phẩm để tạo phiếu nhập kho!");
                return;
            }
            if (!partnersData.some(partner => partner.category === "Nhà cung cấp")) {
                alert("Chưa có nhà cung cấp. Vui lòng thêm nhà cung cấp trước khi tạo phiếu nhập kho!");
                return;
            }
            crudContext = { action: 'add', target: 'importRequest', editIndex: -1 };
            document.getElementById("crudModalTitle").textContent = "Tạo phiếu nhập kho";
            document.getElementById("crudModalFields").innerHTML = `
                <label>Nhà cung cấp</label>
                <select id="import_supplier" class="input">${buildImportSupplierOptions()}</select>
                <label>Sản phẩm cần nhập</label>
                <select id="import_sku" class="input">${buildImportProductOptions()}</select>
                <label>Số lượng nhập thêm</label>
                <input id="import_qty" class="input" type="number" min="1" placeholder="Nhập số lượng">
                <label>Ghi chú</label>
                <input id="import_note" class="input" placeholder="VD: Sắp hết hàng, cần nhập bổ sung">
            `;
            document.getElementById("crudModalBg").classList.add("open");
        }

        // Cửa hàng trưởng duyệt phiếu thì tồn kho mới được cộng.
        async function approveImportRequest(idx) {
            if (window.userRole !== "storeManager") {
                toast("Chỉ cửa hàng trưởng mới có quyền duyệt phiếu nhập");
                return;
            }
            const request = importRequests[idx];
            if (!request || request.status !== "Chờ duyệt") return;
            const product = productsData.find(item => item.sku === request.sku);
            if (!product) {
                toast("Không tìm thấy sản phẩm trong kho");
                return;
            }
            try {
                const response = await fetch(`${API_BASE_URL}/api/phieu-nhap-kho/${encodeURIComponent(request.id)}/duyet`, { method: "POST" });
                if (!response.ok) throw new Error(await response.text());
            } catch (error) {
                alert("Không duyệt được phiếu nhập kho trên database. Vui lòng kiểm tra API đang chạy.");
                console.error(error);
                return;
            }

            product.stock = (Number(product.stock) || 0) + (Number(request.qty) || 0);
            request.status = "Đã duyệt";
            request.approvedAt = new Date().toLocaleString("vi-VN");
            saveAppState();
            updateInventoryKpi();
            toast("Đã duyệt phiếu và cập nhật tồn kho");
        }

        // Từ chối phiếu nhập nếu tạo nhầm hoặc không cần nhập nữa.
        async function rejectImportRequest(idx) {
            if (window.userRole !== "storeManager") {
                toast("Chỉ cửa hàng trưởng mới có quyền từ chối phiếu nhập");
                return;
            }
            const request = importRequests[idx];
            if (!request || request.status !== "Chờ duyệt") return;
            try {
                const response = await fetch(`${API_BASE_URL}/api/phieu-nhap-kho/${encodeURIComponent(request.id)}/tu-choi`, { method: "POST" });
                if (!response.ok) throw new Error(await response.text());
            } catch (error) {
                console.error(error);
                toast("Không từ chối được phiếu nhập trong cơ sở dữ liệu");
                return;
            }
            request.status = "Từ chối";
            request.approvedAt = new Date().toLocaleString("vi-VN");
            saveAppState();
            renderImportRequests();
            toast("Đã từ chối phiếu nhập kho");
        }

        // Chuyển ngày dạng yyyy-mm-dd từ input HTML thành Date để so sánh hạn voucher.
        function parseLocalDate(value) {
            if (!value) return null;
            const [year, month, day] = value.split("-").map(Number);
            return new Date(year, month - 1, day);
        }

        // Kiểm tra mã khuyến mãi còn hạn và đơn hàng đã đủ điều kiện chưa.
        function isPromotionActive(promo, subtotal = 0) {
            const today = new Date();
            today.setHours(0, 0, 0, 0);
            const start = parseLocalDate(promo.startDate);
            const end = parseLocalDate(promo.endDate);
            if (!start || !end) return false;
            start.setHours(0, 0, 0, 0);
            end.setHours(23, 59, 59, 999);
            return today >= start && today <= end && subtotal >= (Number(promo.minOrder) || 0);
        }

        // Vẽ danh sách voucher/khuyến mãi của cửa hàng.
        function renderPromotions() {
            const tbody = document.getElementById("promotionBody");
            if (!tbody) return;

            if (promotionsData.length === 0) {
                tbody.innerHTML = `<tr><td colspan="7" style="text-align:center; padding:30px; color:#94a3b8;">Chưa có mã khuyến mãi nào. Bấm "+ Thêm voucher" để tạo.</td></tr>`;
                return;
            }

            tbody.innerHTML = promotionsData.map((promo, idx) => {
                const active = isPromotionActive(promo, 0);
                return `
                    <tr>
                        <td><strong>${escapeHtml(promo.code)}</strong></td>
                        <td>${promo.percent}%</td>
                        <td>${promo.startDate}</td>
                        <td>${promo.endDate}</td>
                        <td>Đơn từ ${money(Number(promo.minOrder) || 0)}</td>
                        <td><span class="badge ${active ? "green" : "amber"}">${active ? "Đang trong thời gian" : "Chưa/đã hết hạn"}</span></td>
                        <td>
                            <button class="icon-button" title="Chỉnh sửa" onclick="openEditPromotion(${idx})"><i data-lucide="pencil" style="width:16px"></i></button>
                            <button class="icon-button" title="Xóa" onclick="deletePromotion(${idx})"><i data-lucide="trash-2" style="width:16px;color:#ef4444"></i></button>
                        </td>
                    </tr>
                `;
            }).join("");
            if (window.lucide) lucide.createIcons();
        }

        // 3. THAO TÁC THÊM - XÓA - SỬA 
        // Nhóm này dùng chung modal để thêm/sửa sản phẩm, đối tác, loại sản phẩm và voucher.
        // Tạo danh sách option loại sản phẩm cho form sản phẩm.
        function buildProductTypeOptions(selected = "") {
            if (productTypesData.length === 0) {
                return `<option value="">Chưa có loại sản phẩm</option>`;
            }
            return `<option value="">Chọn loại sản phẩm</option>` + productTypesData.map(type =>
                `<option value="${type.name}" ${selected === type.name ? "selected" : ""}>${type.id} - ${type.name}</option>`
            ).join("");
        }

        // Dùng danh sách màu cố định để dữ liệu sau này lọc trên website không bị lệch tên.
        function buildProductColorOptions(selected = "") {
            const colors = ["Đen", "Trắng", "Xám", "Đỏ", "Xanh dương", "Xanh navy", "Xanh lá", "Xanh đậm", "Vàng", "Cam", "Hồng", "Tím", "Nâu", "Be"];
            const values = selected && !colors.includes(selected) ? [selected, ...colors] : colors;
            return `<option value="">Chọn màu sản phẩm</option>` + values.map(color =>
                `<option value="${escapeHtml(color)}" ${selected === color ? "selected" : ""}>${escapeHtml(color)}</option>`
            ).join("");
        }

        function getProductSizes(name = "", type = "") {
            const text = normalizeSkuText(`${name} ${type}`);
            if (/QUAN.*(JEAN|JEANS|KAKI)|(JEAN|JEANS|KAKI).*QUAN/.test(text)) {
                return ["28", "30", "32", "34", "36"];
            }
            if (/DAY NI(T|CH)/.test(text)) return ["80", "85", "90", "95", "100"];
            if (/\bVO\b/.test(text)) return ["35-38", "39-42", "43-46"];
            if (/\bNON\b/.test(text)) return ["S", "M", "L", "XL", "Free size"];
            if (/PHU KIEN|MAT KINH/.test(text)) return ["Free size"];
            return ["S", "M", "L", "XL", "XXL"];
        }

        function buildProductSizeOptions(selected = "", name = "", type = "") {
            const sizes = getProductSizes(name, type);
            const values = selected && !sizes.includes(selected) ? [selected, ...sizes] : sizes;
            return `<option value="">Chọn size sản phẩm</option>` + values.map(size =>
                `<option value="${escapeHtml(size)}" ${selected === size ? "selected" : ""}>${escapeHtml(size)}</option>`
            ).join("");
        }

        function normalizeSkuText(value) {
            return String(value || "")
                .normalize("NFD").replace(/[\u0300-\u036f]/g, "")
                .replace(/đ/gi, "D").toUpperCase();
        }

        function getProductSkuCode(name) {
            const text = normalizeSkuText(name);
            const rules = [
                [/HOODIE/, "HD"], [/AO THUN/, "AT"], [/SO MI/, "ASM"], [/POLO/, "AP"],
                [/AO KHOAC/, "AK"], [/VEST/, "AV"], [/QUAN.*(JEAN|JEANS)/, "QJ"],
                [/QUAN.*KAKI/, "QK"], [/JOGGER/, "QJG"], [/QUAN.*NGAN|SHORT/, "QN"],
                [/VAY/, "VAY"], [/DAM/, "DAM"], [/MAT KINH/, "MK"], [/DAY NI(T|CH)/, "DN"],
                [/NON/, "NON"], [/VO/, "VO"]
            ];
            const matched = rules.find(([pattern]) => pattern.test(text));
            if (matched) return matched[1];
            const initials = text.split(/[^A-Z0-9]+/).filter(Boolean).map(word => word[0]).join("");
            return (initials || "SP").slice(0, 4);
        }

        function getColorSkuCode(color) {
            const text = normalizeSkuText(color);
            const colors = {
                "DEN": "DEN", "TRANG": "TRANG", "XAM": "XAM", "DO": "DO", "VANG": "VANG",
                "CAM": "CAM", "HONG": "HONG", "TIM": "TIM", "NAU": "NAU", "BE": "BE",
                "XANH DUONG": "XANHD", "XANH LA": "XANHL", "XANH DAM": "XANHDAM", "XANH NAVY": "NAVY"
            };
            return colors[text] || text.replace(/[^A-Z0-9]+/g, "").slice(0, 7) || "MD";
        }

        function getSizeSkuCode(size) {
            return normalizeSkuText(size) === "FREE SIZE" ? "FS" : normalizeSkuText(size).replace(/[^A-Z0-9]+/g, "").slice(0, 4);
        }

        function updateProductSizeOptions() {
            const select = document.getElementById("f_size");
            if (!select) return;
            const current = select.value;
            const name = document.getElementById("f_name")?.value || "";
            const type = document.getElementById("f_type")?.value || "";
            select.innerHTML = buildProductSizeOptions(current, name, type);
            if (Array.from(select.options).some(option => option.value === current)) select.value = current;
            updateGeneratedProductCodes();
        }

        function getNextProductId() {
            const max = productsData.reduce((value, product) => {
                const match = String(product.productId || product.sku || "").match(/^SP-(\d+)$/i);
                return Math.max(value, match ? Number(match[1]) : 0);
            }, 0);
            return `SP-${String(max + 1).padStart(3, "0")}`;
        }

        function updateGeneratedProductCodes() {
            if (crudContext.action !== "add") return;
            const name = document.getElementById("f_name")?.value.trim() || "";
            const type = document.getElementById("f_type")?.value.trim() || "";
            const color = document.getElementById("f_color")?.value.trim() || "";
            const size = document.getElementById("f_size")?.value.trim() || "";
            const existing = productsData.find(product =>
                String(product.name || "").trim().toLowerCase() === name.toLowerCase() &&
                String(product.type || "").trim().toLowerCase() === type.toLowerCase());
            const productId = existing?.productId || existing?.sku || getNextProductId();
            const sequence = (String(productId).match(/\d+$/)?.[0] || "001").padStart(3, "0");
            const base = `${getProductSkuCode(name)}-${sequence}-${getColorSkuCode(color)}-${getSizeSkuCode(size || "FS")}`.slice(0, 20).replace(/-+$/, "");
            let sku = base;
            let suffix = 2;
            while (productsData.some(product => String(product.sku).toUpperCase() === sku.toUpperCase())) {
                const tail = `-${suffix++}`;
                sku = base.slice(0, 20 - tail.length).replace(/-+$/, "") + tail;
            }
            document.getElementById("f_product_id").value = productId;
            document.getElementById("f_sku").value = sku;
        }

        // Mở form thêm sản phẩm mới.
        function openAddProduct() {
            if (productTypesData.length === 0) {
                alert("Vui lòng thêm loại sản phẩm trước khi thêm sản phẩm!");
                openAddProductType();
                return;
            }
            crudContext = { action: 'add', target: 'product', editIndex: -1 };
            pendingProductImage = null;
            document.getElementById("crudModalTitle").textContent = "Thêm Sản Phẩm Mới";
            document.getElementById("crudModalFields").innerHTML = `
                    <label>Mã sản phẩm</label>
                    <input id="f_product_id" class="input" value="${getNextProductId()}" readonly>
                    <label>Mã SKU</label>
                    <input id="f_sku" class="input" value="" readonly>
                    <label>Tên sản phẩm</label>
                    <input id="f_name" class="input" placeholder="Nhập tên sản phẩm" oninput="updateProductSizeOptions()">
                    <label>Loại sản phẩm</label>
                    <select id="f_type" class="input" onchange="updateProductSizeOptions()">${buildProductTypeOptions()}</select>
                    <label>Màu sắc</label>
                    <select id="f_color" class="input" onchange="updateGeneratedProductCodes()">${buildProductColorOptions()}</select>
                    <label>Size</label>
                    <select id="f_size" class="input" onchange="updateGeneratedProductCodes()">${buildProductSizeOptions()}</select>
                    <label>Giá bán</label>
                    <input id="f_price" class="input" type="number" placeholder="Nhập giá niêm yết">
                    <label>Số lượng tồn</label>
                    <input id="f_stock" class="input" type="number" placeholder="Nhập số lượng">
                    ${buildProductImageField()}
                `;
            updateGeneratedProductCodes();
            document.getElementById("crudModalBg").classList.add("open");
        }

        // Mở form sửa sản phẩm theo dòng đang chọn.
        function openEditProduct(idx) {
            crudContext = { action: 'edit', target: 'product', editIndex: idx };
            const p = productsData[idx];
            pendingProductImage = p.image || null;
            document.getElementById("crudModalTitle").textContent = "Cập Nhật Sản Phẩm";
            document.getElementById("crudModalFields").innerHTML = `
                    <label>Mã sản phẩm</label>
                    <input id="f_product_id" class="input" value="${escapeHtml(p.productId || p.sku)}" readonly>
                    <label>Mã SKU</label>
                    <input id="f_sku" class="input" value="${escapeHtml(p.sku)}" readonly>
                    <label>Tên sản phẩm</label>
                    <input id="f_name" class="input" value="${p.name}" placeholder="Tên sản phẩm">
                    <label>Loại sản phẩm</label>
                    <select id="f_type" class="input">${buildProductTypeOptions(p.type || "")}</select>
                    <label>Màu sắc</label>
                    <select id="f_color" class="input">${buildProductColorOptions(p.color || "Mặc định")}</select>
                    <label>Size</label>
                    <select id="f_size" class="input">${buildProductSizeOptions(p.size || "Free size", p.name, p.type)}</select>
                    <label>Giá bán</label>
                    <input id="f_price" class="input" type="number" value="${p.price}" placeholder="Giá bán">
                    <label>Số lượng tồn</label>
                    <input id="f_stock" class="input" type="number" value="${p.stock}" placeholder="Số lượng tồn">
                    ${buildProductImageField(p.image)}
                `;
            document.getElementById("crudModalBg").classList.add("open");
        }

        // Tạo vùng chọn và xem trước ảnh trong form sản phẩm.
        function buildProductImageField(image = null) {
            const preview = image
                ? `<img id="f_image_preview" class="product-image-preview" src="${escapeHtml(productImageUrl(image))}" alt="Ảnh sản phẩm">`
                : `<div id="f_image_preview" class="product-image-empty"><i data-lucide="image"></i><span>Chưa có ảnh</span></div>`;
            return `
                <label>Hình ảnh sản phẩm</label>
                <div class="product-image-editor">
                    ${preview}
                    <div class="product-image-actions">
                        <label class="btn btn-secondary product-image-picker">
                            <i data-lucide="image-up" style="width:16px"></i> Chọn ảnh
                            <input id="f_image_file" type="file" accept="image/png,image/jpeg,image/webp" onchange="handleProductImageChange(event)">
                        </label>
                        <button class="btn btn-danger" type="button" onclick="removeProductImage()">
                            <i data-lucide="trash-2" style="width:16px"></i> Xóa ảnh
                        </button>
                    </div>
                </div>`;
        }

        // Đọc ảnh người dùng chọn để gửi cùng dữ liệu sản phẩm lên API.
        function handleProductImageChange(event) {
            const file = event.target.files?.[0];
            if (!file) return;
            if (!['image/png', 'image/jpeg', 'image/webp'].includes(file.type)) {
                alert("Chỉ chấp nhận ảnh PNG, JPG hoặc WEBP.");
                event.target.value = "";
                return;
            }
            if (file.size > 2 * 1024 * 1024) {
                alert("Ảnh không được lớn hơn 2 MB.");
                event.target.value = "";
                return;
            }
            const reader = new FileReader();
            reader.onload = () => {
                pendingProductImage = String(reader.result || "");
                const oldPreview = document.getElementById("f_image_preview");
                const preview = document.createElement("img");
                preview.id = "f_image_preview";
                preview.className = "product-image-preview";
                preview.alt = "Ảnh sản phẩm";
                preview.src = pendingProductImage;
                oldPreview?.replaceWith(preview);
            };
            reader.readAsDataURL(file);
        }

        // Bỏ ảnh hiện tại; khi bấm Lưu, API cập nhật HinhAnh thành NULL.
        async function removeProductImage() {
            if (!await confirmDeletion("Ảnh sản phẩm sẽ bị gỡ khi bạn bấm Lưu dữ liệu. Bạn có chắc muốn xóa ảnh?", "Xóa ảnh sản phẩm?")) return;
            pendingProductImage = null;
            const oldPreview = document.getElementById("f_image_preview");
            if (!oldPreview) return;
            const empty = document.createElement("div");
            empty.id = "f_image_preview";
            empty.className = "product-image-empty";
            empty.innerHTML = '<i data-lucide="image"></i><span>Chưa có ảnh</span>';
            oldPreview.replaceWith(empty);
            document.getElementById("f_image_file").value = "";
            if (window.lucide) lucide.createIcons();
        }

        // Xóa sản phẩm khỏi danh sách và đồng bộ xuống database.
        async function deleteProduct(idx) {
            if (await confirmDeletion("Sản phẩm sẽ bị xóa khỏi cơ sở dữ liệu nếu chưa phát sinh chứng từ. Bạn có chắc muốn xóa?", "Xóa sản phẩm?")) {
                const product = productsData[idx];
                try {
                    const response = await fetch(`${API_BASE_URL}/api/san-pham/${encodeURIComponent(product.sku)}`, { method: "DELETE" });
                    if (!response.ok) {
                        const problem = await response.json().catch(() => null);
                        throw new Error(problem?.message || "Không xóa được sản phẩm.");
                    }
                    await loadAppState();
                    renderAll();
                    toast("Đã xóa sản phẩm thành công!");
                } catch (error) {
                    console.error(error);
                    alert(error.message || "Không xóa được sản phẩm trong cơ sở dữ liệu.");
                }
            }
        }

        // Mở form thêm loại sản phẩm riêng.
        function openAddProductType() {
            crudContext = { action: 'add', target: 'productType', editIndex: -1 };
            document.getElementById("crudModalTitle").textContent = "Thêm Loại Sản Phẩm";
            document.getElementById("crudModalFields").innerHTML = `
                <label>Mã loại</label>
                <input id="lt_id" class="input" value="${generateNextCode(productTypesData, 'id', 'LSP')}" readonly>
                <label>Tên loại</label>
                <input id="lt_name" class="input" placeholder="VD: Áo, Quần, Váy, Phụ kiện">
            `;
            document.getElementById("crudModalBg").classList.add("open");
        }

        // Mở form sửa tên loại sản phẩm.
        function openEditProductType(idx) {
            const type = productTypesData[idx];
            crudContext = { action: 'edit', target: 'productType', editIndex: idx };
            document.getElementById("crudModalTitle").textContent = "Cập Nhật Loại Sản Phẩm";
            document.getElementById("crudModalFields").innerHTML = `
                <label>Mã loại</label>
                <input id="lt_id" class="input" value="${type.id}" readonly>
                <label>Tên loại</label>
                <input id="lt_name" class="input" value="${type.name}" placeholder="Tên loại sản phẩm">
            `;
            document.getElementById("crudModalBg").classList.add("open");
        }

        // Xóa loại sản phẩm nếu không còn sản phẩm nào đang dùng loại đó.
        async function deleteProductType(idx) {
            const type = productTypesData[idx];
            const inUse = productsData.some(product => product.type === type.name);
            if (inUse) {
                alert("Không thể xóa loại sản phẩm đang được dùng bởi sản phẩm!");
                return;
            }
            if (!await confirmDeletion("Loại sản phẩm này sẽ bị xóa khỏi cơ sở dữ liệu. Bạn có chắc muốn xóa?", "Xóa loại sản phẩm?")) return;
            try {
                const response = await fetch(`${API_BASE_URL}/api/loai-san-pham/${encodeURIComponent(type.id)}`, { method: "DELETE" });
                if (!response.ok) throw new Error(await response.text());
                await loadAppState();
                renderAll();
                toast("Đã xóa loại sản phẩm");
            } catch (error) {
                console.error(error);
                alert("Không xóa được loại sản phẩm trong cơ sở dữ liệu.");
            }
        }

        // Sinh mã tự động dạng KH-001, SP-001... dựa trên mã lớn nhất hiện có.
        function generateNextCode(items, field, prefix) {
            const maxNumber = items.reduce((max, item) => {
                const value = String(item[field] || "");
                const match = value.match(new RegExp("^" + prefix + "-?(\\d+)$", "i"));
                return match ? Math.max(max, Number(match[1])) : max;
            }, 0);

            return `${prefix}-${String(maxNumber + 1).padStart(3, "0")}`;
        }

        // Mỗi nhóm đối tác có tiền tố mã riêng: KH, NCC, NV.
        function getPartnerPrefix(category) {
            if (category === "Nhà cung cấp") return "NCC";
            if (category === "Nhân viên") return "NV";
            return "KH";
        }

        // Sinh mã tự động cho khách hàng, nhà cung cấp hoặc nhân viên.
        function generatePartnerCode(category) {
            return generateNextCode(
                partnersData.filter(item => item.category === category),
                "id",
                getPartnerPrefix(category)
            );
        }

        // Trả về nhãn và placeholder đúng với từng loại đối tác để form không bị dùng chung lộn xộn.
        function getPartnerFormMeta(category) {
            const meta = {
                "Khách hàng": {
                    idLabel: "Mã khách hàng",
                    idPlaceholder: "VD: KH-001",
                    nameLabel: "Tên khách hàng",
                    namePlaceholder: "Nhập họ tên khách hàng",
                    phoneLabel: "Số điện thoại khách hàng",
                    phonePlaceholder: "Nhập số điện thoại tích điểm",
                    emailLabel: "Email khách hàng",
                    emailPlaceholder: "VD: khachhang@gmail.com",
                    addressLabel: "Địa chỉ khách hàng",
                    addressPlaceholder: "Nhập địa chỉ liên hệ",
                    roleLabel: "Điểm / hạng hội viên",
                    rolePlaceholder: "Hệ thống tự tính"
                },
                "Nhà cung cấp": {
                    idLabel: "Mã nhà cung cấp",
                    idPlaceholder: "VD: NCC-001",
                    nameLabel: "Tên nhà cung cấp / xưởng",
                    namePlaceholder: "Nhập tên xưởng hoặc công ty",
                    phoneLabel: "Số điện thoại liên hệ",
                    phonePlaceholder: "Nhập số điện thoại nhà cung cấp",
                    emailLabel: "Email nhà cung cấp",
                    emailPlaceholder: "VD: ncc@gmail.com",
                    addressLabel: "Địa chỉ nhà cung cấp",
                    addressPlaceholder: "Nhập địa chỉ xưởng / công ty"
                },
                "Nhân viên": {
                    idLabel: "Mã nhân viên",
                    idPlaceholder: "VD: NV-001",
                    nameLabel: "Họ tên nhân viên",
                    namePlaceholder: "Nhập họ tên nhân viên",
                    phoneLabel: "Số điện thoại nhân viên",
                    phonePlaceholder: "Nhập số điện thoại cá nhân",
                    emailLabel: "Email nhân viên",
                    emailPlaceholder: "VD: nhanvien@gmail.com",
                    addressLabel: "Địa chỉ nhân viên",
                    addressPlaceholder: "Nhập địa chỉ liên hệ",
                    roleLabel: "Quyền truy cập hệ thống",
                    rolePlaceholder: "Chọn quyền tài khoản"
                }
            };

            return meta[category] || meta["Nhà cung cấp"];
        }

        // Dựng các ô nhập của form đối tác theo nhóm đang chọn.
        function buildPartnerModalFields(category, data = {}) {
            const meta = getPartnerFormMeta(category);
            const idValue = data.id || (crudContext.action === 'add' ? generatePartnerCode(category) : '');
            const roleField = category === "Khách hàng"
                ? `<label>${meta.roleLabel}</label><input id="p_role" class="input" value="${getCustomerRank(data)} - ${getCustomerPoints(data)} điểm" readonly>`
                : category === "Nhân viên"
                    ? `<label>${meta.roleLabel}</label><select id="p_role" class="input">
                            <option value="Quản lý" ${(data.role || "Nhân viên bán hàng") === "Quản lý" ? "selected" : ""}>Quản lý</option>
                            <option value="Ca trưởng" ${data.role === "Ca trưởng" ? "selected" : ""}>Ca trưởng</option>
                            <option value="Thu ngân" ${data.role === "Thu ngân" ? "selected" : ""}>Thu ngân</option>
                            <option value="Nhân viên bán hàng" ${(data.role || "Nhân viên bán hàng") === "Nhân viên bán hàng" ? "selected" : ""}>Nhân viên bán hàng</option>
                            <option value="Nhân viên kho" ${data.role === "Nhân viên kho" ? "selected" : ""}>Nhân viên kho</option>
                        </select>`
                : "";
            const emailField = `<label>${meta.emailLabel}</label>
                    <input id="p_email" class="input" type="email" value="${data.email || ''}" placeholder="${meta.emailPlaceholder}">`;

            return `
                    <label>Phân loại nhóm</label>
                    <select id="p_category" class="input" onchange="refreshPartnerFields()" ${crudContext.action === 'edit' ? 'disabled' : ''}>
                        ${category === 'Khách hàng' ? '<option value="Khách hàng" selected>Khách hàng</option>' : ''}
                        <option value="Nhà cung cấp" ${category === 'Nhà cung cấp' ? 'selected' : ''}>Nhà cung cấp (Xưởng may)</option>
                        <option value="Nhân viên" ${category === 'Nhân viên' ? 'selected' : ''}>Nhân viên</option>
                    </select>
                    <label>${meta.idLabel}</label>
                    <input id="p_id" class="input" value="${idValue}" placeholder="${meta.idPlaceholder}" readonly>
                    <label>${meta.nameLabel}</label>
                    <input id="p_name" class="input" value="${data.name || ''}" placeholder="${meta.namePlaceholder}">
                    <label>${meta.phoneLabel}</label>
                    <input id="p_phone" class="input" inputmode="numeric" pattern="[0-9]*" value="${data.phone || ''}" placeholder="${meta.phonePlaceholder}" oninput="digitsOnly(this)" onkeydown="return allowPhoneKey(event)">
                    ${emailField}
                    <label>${meta.addressLabel}</label>
                    <input id="p_address" class="input" value="${data.address || ''}" placeholder="${meta.addressPlaceholder}">
                    ${roleField}
                `;
        }

        // Lấy tạm dữ liệu đang nhập để đổi nhóm mà không mất hết thông tin.
        function getCurrentPartnerFormData() {
            return {
                category: document.getElementById("p_category")?.value || currentPartnerCategory,
                id: document.getElementById("p_id")?.value || "",
                name: document.getElementById("p_name")?.value || "",
                phone: document.getElementById("p_phone")?.value || "",
                email: document.getElementById("p_email")?.value || "",
                address: document.getElementById("p_address")?.value || "",
                role: document.getElementById("p_role")?.value || ""
            };
        }

        // Khi chọn nhóm khác trong form đối tác, vẽ lại đúng bộ ô nhập của nhóm đó.
        function refreshPartnerFields() {
            const category = document.getElementById("p_category").value;
            const data = getCurrentPartnerFormData();
            if (data.category !== category) data.role = "";
            if (crudContext.action === 'add') data.id = generatePartnerCode(category);
            document.getElementById("crudModalFields").innerHTML = buildPartnerModalFields(category, data);
        }

        // Số điện thoại Việt Nam bắt đầu bằng 0 và có 10 hoặc 11 chữ số.
        function isPhoneValid(phone) {
            return /^0\d{9,10}$/.test(String(phone || "").trim());
        }

        // Chặn gõ chữ trong ô số điện thoại nhưng vẫn cho Backspace, Delete, mũi tên...
        function allowPhoneKey(event) {
            const allowedKeys = ["Backspace", "Delete", "Tab", "ArrowLeft", "ArrowRight", "Home", "End"];
            if (allowedKeys.includes(event.key) || event.ctrlKey || event.metaKey) return true;
            return /^\d$/.test(event.key);
        }

        // Dọn ký tự không phải số nếu người dùng dán nội dung vào ô điện thoại.
        function digitsOnly(input) {
            input.value = input.value.replace(/\D/g, "");
        }

        // Email phải có tên hộp thư, dấu @, tên miền và kết thúc bằng .com.
        function isEmailValid(email) {
            const value = String(email || "").trim().toLowerCase();
            return /^[^@\s]+@[^@\s]+\.com$/.test(value);
        }

        // Mở form thêm khách hàng/nhà cung cấp/nhân viên.
        function openAddPartner() {
            if (currentPartnerCategory === "Khách hàng") currentPartnerCategory = "Nhà cung cấp";
            crudContext = { action: 'add', target: 'partner', editIndex: -1 };
            document.getElementById("crudModalTitle").textContent = "Thêm Mới Đối Tác";
            document.getElementById("crudModalFields").innerHTML = buildPartnerModalFields(currentPartnerCategory);
            document.getElementById("crudModalBg").classList.add("open");
        }

        // Mở form sửa thông tin đối tác.
        function openEditPartner(idx) {
            crudContext = { action: 'edit', target: 'partner', editIndex: idx };
            const p = partnersData[idx];
            document.getElementById("crudModalTitle").textContent = p.category === "Khách hàng" ? "Cập Nhật Khách Hàng" : "Cập Nhật Đối Tác";
            document.getElementById("crudModalFields").innerHTML = buildPartnerModalFields(p.category, p);
            document.getElementById("crudModalBg").classList.add("open");
        }

        // Xóa đối tác sau khi người dùng xác nhận.
        async function deletePartner(idx) {
            const partner = partnersData[idx];
            if (!partner) return;
            const isEmployee = partner.category === "Nhân viên";
            const isCustomer = partner.category === "Khách hàng";
            const message = isEmployee
                ? "Nhân viên sẽ bị ngừng hoạt động và không thể đăng nhập. Các hóa đơn cũ vẫn giữ tên người lập để bảo toàn lịch sử. Bạn có muốn tiếp tục?"
                : isCustomer
                    ? "Khách hàng sẽ bị dừng hoạt động và không còn xuất hiện trong danh sách chọn tại POS. Hóa đơn và lịch sử mua hàng vẫn được giữ nguyên. Bạn có muốn tiếp tục?"
                    : "Nhà cung cấp sẽ bị dừng hoạt động và không còn xuất hiện khi tạo phiếu nhập kho. Bạn có muốn tiếp tục?";
            if (await confirmDeletion(message, `Dừng ${partner.category.toLowerCase()}?`)) {
                try {
                    const response = await fetch(`${API_BASE_URL}/api/doi-tac/${encodeURIComponent(partner.category)}/${encodeURIComponent(partner.id)}`, { method: "DELETE" });
                    if (!response.ok) throw new Error(await response.text());
                    await loadAppState();
                    renderAll();
                    toast(isEmployee ? "Đã ngừng hoạt động nhân viên; hóa đơn cũ vẫn được giữ nguyên."
                        : isCustomer ? "Đã dừng khách hàng; lịch sử mua hàng vẫn được giữ nguyên."
                        : "Đã dừng nhà cung cấp.");
                } catch (error) {
                    console.error(error);
                    alert(error.message || "Không xóa được đối tác trong cơ sở dữ liệu.");
                }
            }
        }

        // Dựng form nhập mã khuyến mãi: mã, %, ngày bắt đầu/kết thúc, điều kiện tối thiểu.
        function buildPromotionModalFields(data = {}) {
            return `
                <label>Mã khuyến mãi</label>
                <input id="km_code" class="input" value="${data.code || ""}" placeholder="VD: YODY10">
                <label>% khuyến mãi</label>
                <input id="km_percent" class="input" type="number" min="1" max="100" value="${data.percent || ""}" placeholder="VD: 10">
                <label>Ngày bắt đầu</label>
                <input id="km_start" class="input" type="date" value="${data.startDate || ""}">
                <label>Ngày kết thúc</label>
                <input id="km_end" class="input" type="date" value="${data.endDate || ""}">
                <label>Điều kiện khuyến mãi</label>
                <input id="km_min_order" class="input" type="number" min="0" value="${data.minOrder || 0}" placeholder="Giá trị đơn tối thiểu">
            `;
        }

        // Mở form thêm voucher mới.
        function openAddPromotion() {
            crudContext = { action: 'add', target: 'promotion', editIndex: -1 };
            document.getElementById("crudModalTitle").textContent = "Thêm Voucher / Khuyến Mãi";
            document.getElementById("crudModalFields").innerHTML = buildPromotionModalFields();
            document.getElementById("crudModalBg").classList.add("open");
        }

        // Mở form sửa voucher.
        function openEditPromotion(idx) {
            crudContext = { action: 'edit', target: 'promotion', editIndex: idx };
            document.getElementById("crudModalTitle").textContent = "Cập Nhật Voucher / Khuyến Mãi";
            document.getElementById("crudModalFields").innerHTML = buildPromotionModalFields(promotionsData[idx]);
            document.getElementById("crudModalBg").classList.add("open");
        }

        // Xóa voucher khỏi danh sách.
        async function deletePromotion(idx) {
            if (!await confirmDeletion("Mã khuyến mãi sẽ bị xóa và không thể tiếp tục áp dụng. Bạn có chắc muốn xóa?", "Xóa mã khuyến mãi?")) return;
            if (appliedPromotion && promotionsData[idx] && appliedPromotion.code === promotionsData[idx].code) {
                appliedPromotion = null;
            }
            try {
                const response = await fetch(`${API_BASE_URL}/api/khuyen-mai/${encodeURIComponent(promotionsData[idx].code)}`, { method: "DELETE" });
                if (!response.ok) throw new Error(await response.text());
                await loadAppState();
                renderAll();
                toast("Đã xóa mã khuyến mãi");
            } catch (error) {
                console.error(error);
                alert("Không xóa được mã khuyến mãi trong cơ sở dữ liệu.");
            }
        }

        function buildCityOptions() {
            return Object.keys(ADDRESS_OPTIONS).map(city => `<option value="${escapeHtml(city)}">${escapeHtml(city)}</option>`).join("");
        }

        // Địa chỉ giao hàng chỉ cho chọn thành phố và phường từ danh sách thống nhất.
        function initializeDeliveryAddressSelectors() {
            const city = document.getElementById("deliveryCity");
            if (!city) return;
            city.innerHTML = `<option value="">Chọn thành phố</option>${buildCityOptions()}`;
            updateDeliveryWards();
        }

        function updateDeliveryWards() {
            const city = document.getElementById("deliveryCity")?.value || "";
            const ward = document.getElementById("deliveryWard");
            if (!ward) return;
            ward.innerHTML = `<option value="">Chọn phường</option>` +
                (ADDRESS_OPTIONS[city] || []).map(item => `<option value="${escapeHtml(item)}">${escapeHtml(item)}</option>`).join("");
        }

        function getDeliveryAddress() {
            const detail = document.getElementById("deliveryAddressDetail")?.value.trim() || "";
            const ward = document.getElementById("deliveryWard")?.value || "";
            const city = document.getElementById("deliveryCity")?.value || "";
            return [detail, ward, city].filter(Boolean).join(", ");
        }

        function updateQuickCustomerWards() {
            const city = document.getElementById("quick_customer_city")?.value || "";
            const ward = document.getElementById("quick_customer_ward");
            if (!ward) return;
            const wards = ADDRESS_OPTIONS[city] || [];
            ward.innerHTML = `<option value="">Chọn phường / xã</option>` + wards.map(item => `<option value="${escapeHtml(item)}">${escapeHtml(item)}</option>`).join("");
        }

        // Form nhanh cho thu ngân: thêm đủ thông tin khách rồi quay lại luồng bán hàng.
        function openQuickCustomerModal() {
            crudContext = { action: 'add', target: 'quickCustomer', editIndex: -1 };
            const enteredPhone = document.getElementById("customerSearch")?.value.trim() || "";
            document.getElementById("crudModalTitle").textContent = "Thêm nhanh khách hàng";
            document.getElementById("crudModalFields").innerHTML = `
                <label>Tên khách hàng</label>
                <input id="quick_customer_name" class="input" placeholder="Nhập tên khách hàng">
                <label>Số điện thoại</label>
                <input id="quick_customer_phone" class="input" inputmode="numeric" maxlength="11" pattern="[0-9]*" value="${escapeHtml(enteredPhone)}" placeholder="Ví dụ: 0901234567" oninput="digitsOnly(this)" onkeydown="return allowPhoneKey(event)">
                <label>Email</label>
                <input id="quick_customer_email" class="input" type="email" placeholder="VD: khachhang@gmail.com">
                <label>Tỉnh / thành phố</label>
                <select id="quick_customer_city" class="input" onchange="updateQuickCustomerWards()"><option value="">Chọn tỉnh / thành phố</option>${buildCityOptions()}</select>
                <label>Phường / xã</label>
                <select id="quick_customer_ward" class="input"><option value="">Chọn phường / xã</option></select>
                <label>Địa chỉ chi tiết</label>
                <input id="quick_customer_address" class="input" placeholder="Số nhà, tên đường">
            `;
            document.getElementById("crudModalBg").classList.add("open");
            setTimeout(() => document.getElementById("quick_customer_name")?.focus(), 50);
        }

        // Nút Lưu của modal chung. Dựa vào crudContext để biết đang lưu sản phẩm, đối tác hay voucher.
        async function saveCrudData() {
            if (crudContext.target === 'product') {
                const sku = document.getElementById("f_sku").value.trim();
                const productId = document.getElementById("f_product_id").value.trim();
                const name = document.getElementById("f_name").value.trim();
                const type = document.getElementById("f_type").value.trim();
                const color = document.getElementById("f_color").value.trim();
                const size = document.getElementById("f_size").value.trim();
                const price = Number(document.getElementById("f_price").value) || 0;
                const stock = Number(document.getElementById("f_stock").value) || 0;

                if (!productId || !sku || !name || !type || !color || !size) {
                    alert("Vui lòng điền đủ Mã sản phẩm, Tên sản phẩm, Loại sản phẩm, Màu sắc và Size!");
                    return;
                }

                if (price <= 0) {
                    alert("Giá bán phải lớn hơn 0!");
                    return;
                }

                if (!Number.isInteger(stock) || stock < 0) {
                    alert("Số lượng tồn phải là số nguyên không âm!");
                    return;
                }

                try {
                    const response = await fetch(`${API_BASE_URL}/api/san-pham`, {
                        method: "POST",
                        headers: { "Content-Type": "application/json" },
                        body: JSON.stringify({ productId, sku, name, type, color, size, price, stock, image: pendingProductImage })
                    });
                    if (!response.ok) throw new Error(await response.text());
                    await loadAppState();
                    renderAll();
                    toast(crudContext.action === 'add' ? "Đã thêm sản phẩm mới!" : "Đã cập nhật sản phẩm!");
                } catch (error) {
                    console.error(error);
                    alert("Không lưu được sản phẩm vào cơ sở dữ liệu.\nChi tiết: " + (error.message || error));
                    return;
                }
            } else if (crudContext.target === 'productType') {
                const id = document.getElementById("lt_id").value.trim();
                const name = document.getElementById("lt_name").value.trim();

                if (!id || !name) {
                    alert("Vui lòng nhập đủ Mã loại và Tên loại!");
                    return;
                }

                const duplicated = productTypesData.some((item, idx) =>
                    item.name.toLowerCase() === name.toLowerCase() && idx !== crudContext.editIndex
                );
                if (duplicated) {
                    alert("Tên loại sản phẩm này đã tồn tại!");
                    return;
                }

                try {
                    const response = await fetch(`${API_BASE_URL}/api/loai-san-pham`, {
                        method: "POST",
                        headers: { "Content-Type": "application/json" },
                        body: JSON.stringify({ id, name, description: "", active: true })
                    });
                    if (!response.ok) throw new Error(await response.text());
                    await loadAppState();
                    renderAll();
                    toast(crudContext.action === 'add' ? "Đã thêm loại sản phẩm!" : "Đã cập nhật loại sản phẩm!");
                } catch (error) {
                    console.error(error);
                    alert("Không lưu được loại sản phẩm vào cơ sở dữ liệu.");
                    return;
                }
            } else if (crudContext.target === 'importRequest') {
                const supplierId = document.getElementById("import_supplier").value;
                const sku = document.getElementById("import_sku").value;
                const qty = Number(document.getElementById("import_qty").value) || 0;
                const note = document.getElementById("import_note").value.trim();
                const supplier = partnersData.find(item => item.category === "Nhà cung cấp" && item.id === supplierId);
                const product = productsData.find(item => item.sku === sku);

                if (!supplier) {
                    alert("Vui lòng chọn nhà cung cấp!");
                    return;
                }

                if (!product) {
                    alert("Vui lòng chọn sản phẩm cần nhập!");
                    return;
                }

                if (!Number.isInteger(qty) || qty <= 0) {
                    alert("Số lượng nhập phải là số nguyên lớn hơn 0!");
                    return;
                }

                let createdId = generateNextCode(importRequests, "id", "PN");
                try {
                    const response = await fetch(`${API_BASE_URL}/api/phieu-nhap-kho`, {
                        method: "POST",
                        headers: { "Content-Type": "application/json" },
                        body: JSON.stringify({ supplierId, sku, qty, note, employeeId: getCurrentEmployee()?.id || null })
                    });
                    if (!response.ok) throw new Error(await response.text());
                    const result = await response.json();
                    createdId = result.id || createdId;
                } catch (error) {
                    alert("Không lưu được phiếu nhập kho lên database. Vui lòng kiểm tra API đang chạy.");
                    console.error(error);
                    return;
                }

                await loadAppState();
                renderAll();
                toast("Đã tạo phiếu nhập kho chờ duyệt");
            } else if (crudContext.target === 'quickCustomer') {
                const name = document.getElementById("quick_customer_name").value.trim();
                const phone = document.getElementById("quick_customer_phone").value.trim();
                const email = document.getElementById("quick_customer_email").value.trim();
                const city = document.getElementById("quick_customer_city").value;
                const ward = document.getElementById("quick_customer_ward").value;
                const addressDetail = document.getElementById("quick_customer_address").value.trim();
                const address = [addressDetail, ward, city].filter(Boolean).join(", ");

                if (!name) {
                    alert("Vui lòng nhập tên khách hàng!");
                    return;
                }

                if (!isPhoneValid(phone)) {
                    alert("Số điện thoại phải bắt đầu bằng 0 và gồm 10 đến 11 chữ số!");
                    return;
                }

                if (!isEmailValid(email)) {
                    alert("Email phải đúng định dạng, ví dụ: ten@gmail.com!");
                    return;
                }

                if (!city || !ward || !addressDetail) {
                    alert("Vui lòng chọn tỉnh/thành phố, phường/xã và nhập địa chỉ chi tiết!");
                    return;
                }

                const existed = partnersData.find(item => item.category === "Khách hàng" && item.phone === phone);
                if (existed) {
                    document.getElementById("customerSearch").value = phone;
                    handleCustomerPhoneInput();
                    toast("Khách hàng đã có trong hệ thống");
                } else {
                    try {
                        const response = await fetch(`${API_BASE_URL}/api/doi-tac`, {
                            method: "POST",
                            headers: { "Content-Type": "application/json" },
                            body: JSON.stringify({
                                category: "Khách hàng",
                                id: generatePartnerCode("Khách hàng"),
                                name,
                                phone,
                                email,
                                address,
                                role: ""
                            })
                        });
                        if (!response.ok) throw new Error(await response.text());
                        await loadAppState();
                        document.getElementById("customerSearch").value = phone;
                        handleCustomerPhoneInput();
                        toast("Đã thêm nhanh khách hàng");
                    } catch (error) {
                        console.error(error);
                        alert("Không lưu được khách hàng vào cơ sở dữ liệu.");
                        return;
                    }
                }
            } else if (crudContext.target === 'partner') {
                const categoryInput = document.getElementById("p_category");
                const category = categoryInput ? categoryInput.value : currentPartnerCategory;
                const id = document.getElementById("p_id").value.trim();
                const name = document.getElementById("p_name").value.trim();
                const phone = document.getElementById("p_phone").value.trim();
                const email = document.getElementById("p_email")?.value.trim() || "";
                const address = document.getElementById("p_address").value.trim();
                const role = category === "Nhân viên" ? document.getElementById("p_role")?.value.trim() || "" : "";

                if (!id || !name) {
                    alert("Vui lòng nhập đủ Mã và Tên đối tác!");
                    return;
                }

                if (category === "Khách hàng" && crudContext.action === 'add') {
                    alert("Khách hàng chỉ được thêm nhanh trong POS bán hàng!");
                    return;
                }

                if (!isPhoneValid(phone)) {
                    alert("Số điện thoại là bắt buộc và chỉ được nhập số!");
                    return;
                }

                if (!isEmailValid(email)) {
                    alert("Email phải đúng định dạng, ví dụ: ten@gmail.com!");
                    return;
                }

                try {
                    const response = await fetch(`${API_BASE_URL}/api/doi-tac`, {
                        method: "POST",
                        headers: { "Content-Type": "application/json" },
                        body: JSON.stringify({ category, id, name, phone, email, address, role })
                    });
                    if (!response.ok) throw new Error(await response.text());
                    await loadAppState();
                    renderAll();
                    toast(crudContext.action === 'add' ? "Đã thêm đối tác mới!" : "Đã cập nhật thông tin đối tác!");
                } catch (error) {
                    console.error(error);
                    alert("Không lưu được đối tác vào cơ sở dữ liệu.");
                    return;
                }
            } else if (crudContext.target === 'promotion') {
                const code = document.getElementById("km_code").value.trim().toUpperCase();
                const percent = Number(document.getElementById("km_percent").value);
                const startDate = document.getElementById("km_start").value;
                const endDate = document.getElementById("km_end").value;
                const minOrder = Number(document.getElementById("km_min_order").value) || 0;

                if (!code || !percent || !startDate || !endDate) {
                    alert("Vui lòng nhập đủ mã khuyến mãi, % giảm, ngày bắt đầu và ngày kết thúc!");
                    return;
                }

                if (percent <= 0 || percent > 100) {
                    alert("% khuyến mãi phải từ 1 đến 100!");
                    return;
                }

                if (parseLocalDate(endDate) < parseLocalDate(startDate)) {
                    alert("Ngày kết thúc phải sau hoặc bằng ngày bắt đầu!");
                    return;
                }

                if (minOrder < 0) {
                    alert("Điều kiện đơn tối thiểu không được âm!");
                    return;
                }

                const duplicated = promotionsData.some((item, idx) =>
                    item.code.toUpperCase() === code && idx !== crudContext.editIndex
                );
                if (duplicated) {
                    alert("Mã khuyến mãi này đã tồn tại!");
                    return;
                }

                try {
                    const response = await fetch(`${API_BASE_URL}/api/khuyen-mai`, {
                        method: "POST",
                        headers: { "Content-Type": "application/json" },
                        body: JSON.stringify({ code, percent, startDate, endDate, minOrder })
                    });
                    if (!response.ok) throw new Error(await response.text());
                    await loadAppState();
                    renderAll();
                    toast(crudContext.action === 'add' ? "Đã thêm mã khuyến mãi!" : "Đã cập nhật mã khuyến mãi!");
                } catch (error) {
                    console.error(error);
                    alert("Không lưu được mã khuyến mãi vào cơ sở dữ liệu.");
                    return;
                }
            }
            closeCrudModal();
        }

        // Đóng modal thêm/sửa chung.
        function closeCrudModal() {
            document.getElementById("crudModalBg").classList.remove("open");
        }

        // Kích hoạt chọn file Excel/CSV để nhập nhiều đối tác cùng lúc.
        function openPartnerImport() {
            document.getElementById("partnerImportFile").click();
        }

        // Kích hoạt chọn file Excel/CSV để nhập nhiều sản phẩm cùng lúc.
        function openProductImport() {
            document.getElementById("productImportFile").click();
        }

        // Chuẩn hóa tên cột file import để đọc được cả tiếng Việt có dấu/không dấu.
        function normalizeKey(key) {
            return String(key || "")
                .trim()
                .toLowerCase()
                .normalize("NFD")
                .replace(/[\u0300-\u036f]/g, "")
                .replace(/\s+/g, "");
        }

        // Lấy dữ liệu trong một dòng import theo nhiều tên cột có thể có.
        function getRowValue(row, keys) {
            const normalized = {};
            Object.keys(row).forEach(key => {
                normalized[normalizeKey(key)] = row[key];
            });

            for (const key of keys) {
                const value = normalized[normalizeKey(key)];
                if (value !== undefined && value !== null) return String(value).trim();
            }
            return "";
        }

        // Đưa tên nhóm trong file import về đúng 3 nhóm hệ thống đang dùng.
        function normalizePartnerCategory(value) {
            const text = String(value || currentPartnerCategory).trim().toLowerCase();
            if (text.includes("ncc") || text.includes("cung") || text.includes("xưởng") || text.includes("xuong")) return "Nhà cung cấp";
            if (text.includes("nv") || text.includes("nhân") || text.includes("nhan")) return "Nhân viên";
            return "Nhà cung cấp";
        }

        // Đọc file CSV đơn giản thành danh sách object theo dòng.
        function readCsvRows(text) {
            const lines = String(text || "").replace(/^\uFEFF/, "").split(/\r?\n/).filter(line => line.trim());
            if (lines.length < 2) return [];
            const countDelimiter = (line, delimiter) => {
                let quoted = false;
                let count = 0;
                for (let index = 0; index < line.length; index += 1) {
                    if (line[index] === '"') {
                        if (quoted && line[index + 1] === '"') index += 1;
                        else quoted = !quoted;
                    } else if (!quoted && line[index] === delimiter) count += 1;
                }
                return count;
            };
            const delimiter = countDelimiter(lines[0], ";") > countDelimiter(lines[0], ",") ? ";" : ",";
            const splitLine = line => {
                const cells = [];
                let value = "";
                let quoted = false;
                for (let index = 0; index < line.length; index += 1) {
                    const char = line[index];
                    if (char === '"') {
                        if (quoted && line[index + 1] === '"') {
                            value += '"';
                            index += 1;
                        } else quoted = !quoted;
                    } else if (char === delimiter && !quoted) {
                        cells.push(value.trim());
                        value = "";
                    } else value += char;
                }
                cells.push(value.trim());
                return cells;
            };
            const headers = splitLine(lines[0]);
            return lines.slice(1).map(line => {
                const values = splitLine(line);
                return headers.reduce((row, header, index) => {
                    row[header] = values[index] || "";
                    return row;
                }, {});
            });
        }

        // Chuyển một dòng Excel/CSV thành một đối tác và gom lỗi nếu thiếu dữ liệu.
        function buildPartnerFromImportRow(row, index) {
            const category = normalizePartnerCategory(getRowValue(row, ["nhom", "loai", "phanloai", "category", "nhóm", "phân loại"]));
            const name = getRowValue(row, ["ten", "hoten", "tendoitac", "tenkhachhang", "tennhacungcap", "tennhanvien", "name", "tên"]);
            const phone = getRowValue(row, ["sdt", "sodienthoai", "phone", "điện thoại", "số điện thoại"]);
            const email = getRowValue(row, ["email", "mail"]);
            const address = getRowValue(row, ["diachi", "address", "địa chỉ"]);
            const role = category === "Khách hàng" ? "" : getRowValue(row, ["chucvu", "vaitro", "role", "chức vụ"]);
            const points = Number(getRowValue(row, ["diem", "diemtichluy", "points", "điểm"])) || 0;
            const totalSpent = Number(getRowValue(row, ["tongchitieu", "totalspent", "chi tiêu", "tong tien"])) || 0;
            const errors = [];
            const employeeRoles = ["Quản lý", "Ca trưởng", "Thu ngân", "Nhân viên bán hàng", "Nhân viên kho"];

            if (!name) errors.push("thiếu tên");
            if (!isPhoneValid(phone)) errors.push("số điện thoại phải là số và không được trống");
            if (!isEmailValid(email)) errors.push("email phải đúng định dạng và kết thúc bằng .com");
            if (category === "Khách hàng") errors.push("khách hàng chỉ thêm nhanh ở POS");
            if (category === "Nhân viên" && role && !employeeRoles.includes(role)) errors.push("chức vụ nhân viên không hợp lệ");

            return {
                partner: {
                    category,
                    id: generatePartnerCodeForList(category, partnersData),
                    name,
                    phone,
                    email,
                    address,
                    role,
                    points: category === "Khách hàng" ? points : undefined,
                    totalSpent: category === "Khách hàng" ? totalSpent : undefined
                },
                errors: errors.length ? `Dòng ${index + 2}: ${errors.join(", ")}` : ""
            };
        }

        // Sinh mã đối tác khi import nhiều dòng, tránh trùng mã với dữ liệu đã có.
        function generatePartnerCodeForList(category, list) {
            return generateNextCode(
                list.filter(item => item.category === category),
                "id",
                getPartnerPrefix(category)
            );
        }

        // Import nhiều đối tác; nếu có dòng lỗi thì dừng để người dùng sửa file.
        function importPartnerRows(rows) {
            const imported = [];
            const errors = [];

            rows.forEach((row, index) => {
                const result = buildPartnerFromImportRow(row, index);
                if (result.errors) {
                    errors.push(result.errors);
                    return;
                }
                imported.push(result.partner);
                partnersData.push(result.partner);
            });

            if (errors.length) {
                partnersData.splice(partnersData.length - imported.length, imported.length);
                alert("File có dữ liệu chưa hợp lệ:\n" + errors.slice(0, 8).join("\n") + (errors.length > 8 ? "\n..." : ""));
                return;
            }

            currentPartnerCategory = imported[0]?.category || currentPartnerCategory;
            document.querySelectorAll(".partner-tab").forEach(t => {
                t.classList.toggle("active", t.textContent.includes(currentPartnerCategory));
            });
            renderPartners();
            toast(`Đã nhập ${imported.length} dòng từ file`);
        }

        // Đọc file Excel/CSV đối tác từ máy rồi đưa vào hàm import.
        function importPartnersFile(event) {
            const file = event.target.files[0];
            event.target.value = "";
            if (!file) return;

            const extension = file.name.split(".").pop().toLowerCase();
            const reader = new FileReader();

            reader.onload = e => {
                try {
                    let rows = [];
                    if (extension === "csv") {
                        rows = readCsvRows(e.target.result);
                    } else {
                        if (!window.XLSX) {
                            alert("Chưa tải được thư viện đọc Excel. Bạn thử dùng file CSV trước nhé.");
                            return;
                        }
                        const workbook = XLSX.read(new Uint8Array(e.target.result), { type: "array" });
                        const sheet = workbook.Sheets[workbook.SheetNames[0]];
                        rows = XLSX.utils.sheet_to_json(sheet, { defval: "" });
                    }

                    if (!rows.length) {
                        alert("File không có dữ liệu để nhập.");
                        return;
                    }

                    importPartnerRows(rows);
                } catch (error) {
                    alert("Không đọc được file. Vui lòng kiểm tra định dạng CSV/XLSX.");
                }
            };

            if (extension === "csv") reader.readAsText(file, "UTF-8");
            else reader.readAsArrayBuffer(file);
        }

        // Chuyển một dòng Excel/CSV thành sản phẩm và kiểm tra SKU, giá, tồn kho.
        function buildProductFromImportRow(row, index, existingProducts = productsData) {
            const productId = getRowValue(row, ["masp", "productid", "mã sp", "mã sản phẩm"]);
            const sku = getRowValue(row, ["masku", "sku", "mã sku"]);
            const name = getRowValue(row, ["ten", "tensp", "tensanpham", "name", "tên", "tên sản phẩm"]);
            const type = getRowValue(row, ["loai", "loaisanpham", "category", "type", "loại", "loại sản phẩm"]);
            const color = getRowValue(row, ["mau", "mausac", "color", "màu", "màu sắc"]) || "Mặc định";
            const size = getRowValue(row, ["size", "kichco", "kích cỡ", "kíchco"]);
            const priceRaw = getRowValue(row, ["gia", "giaban", "price", "giá", "giá bán"]);
            const stockRaw = getRowValue(row, ["ton", "tonkho", "soluong", "stock", "qty", "tồn", "tồn kho", "số lượng"]);
            const price = Number(String(priceRaw).replace(/[^\d.-]/g, ""));
            const stock = Number(String(stockRaw).replace(/[^\d.-]/g, ""));
            const errors = [];

            if (!/^SP-\d{3,}$/i.test(productId)) errors.push("Mã SP phải có dạng SP-023");
            if (!name) errors.push("thiếu tên sản phẩm");
            if (!type) errors.push("thiếu loại sản phẩm");
            if (!size) errors.push("thiếu size");
            if (!priceRaw || !Number.isFinite(price) || price <= 0) errors.push("giá bán phải lớn hơn 0");
            if (!stockRaw || !Number.isInteger(stock) || stock < 0) errors.push("tồn kho phải là số nguyên không âm");

            if (errors.length) return { errors: `Dòng ${index + 2}: ${errors.join(", ")}` };

            return {
                product: {
                    productId: productId.toUpperCase(),
                    sku: sku ? sku.toUpperCase() : null,
                    name,
                    type,
                    color,
                    size,
                    price,
                    stock
                }
            };
        }

        // Nếu file import có loại sản phẩm mới thì tự thêm loại đó vào danh sách.
        function ensureProductType(name) {
            const existing = productTypesData.find(item => item.name.toLowerCase() === name.toLowerCase());
            if (existing) return existing;
            const created = {
                id: generateNextCode(productTypesData, "id", "LSP"),
                name
            };
            productTypesData.push(created);
            return created;
        }

        // Import nhiều sản phẩm; tất cả hợp lệ thì mới thêm vào danh sách.
        async function importProductRows(rows) {
            const imported = [];
            const errors = [];
            const knownProducts = [...productsData];

            rows.forEach((row, index) => {
                const result = buildProductFromImportRow(row, index, knownProducts);
                if (result.errors) {
                    errors.push(result.errors);
                    return;
                }
                const duplicateSku = result.product.sku && knownProducts.some(item =>
                    String(item.sku || "").toLowerCase() === result.product.sku.toLowerCase());
                const duplicateVariant = knownProducts.some(item =>
                    String(item.productId || "").toLowerCase() === result.product.productId.toLowerCase() &&
                    String(item.color || "").trim().toLowerCase() === result.product.color.trim().toLowerCase() &&
                    String(item.size || "").trim().toLowerCase() === result.product.size.trim().toLowerCase());
                if (duplicateSku) {
                    errors.push(`Dòng ${index + 2}: Mã SKU ${result.product.sku} đã tồn tại`);
                    return;
                }
                if (duplicateVariant) {
                    errors.push(`Dòng ${index + 2}: biến thể ${result.product.productId} - ${result.product.color} - ${result.product.size} đã tồn tại`);
                    return;
                }
                imported.push(result.product);
                knownProducts.push(result.product);
            });

            if (errors.length) {
                alert("File có dữ liệu sản phẩm chưa hợp lệ:\n" + errors.slice(0, 8).join("\n") + (errors.length > 8 ? "\n..." : ""));
                return;
            }

            try {
                const response = await fetch(`${API_BASE_URL}/api/san-pham/import`, {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify(imported)
                });
                if (!response.ok) {
                    const content = await response.text();
                    let message = content;
                    try { message = JSON.parse(content).detail || JSON.parse(content).message || content; } catch { }
                    throw new Error(message);
                }
                await loadAppState();
                renderAll();
                toast(`Đã nhập ${imported.length} sản phẩm vào cơ sở dữ liệu`);
            } catch (error) {
                console.error(error);
                alert("Không nhập được sản phẩm:\n" + (error.message || "Không kết nối được API."));
            }
        }

        // Đọc file Excel/CSV sản phẩm từ máy rồi đưa vào hàm import.
        function importProductsFile(event) {
            const file = event.target.files[0];
            event.target.value = "";
            if (!file) return;

            const extension = file.name.split(".").pop().toLowerCase();
            const reader = new FileReader();
            reader.onload = async e => {
                try {
                    let rows = [];
                    if (extension === "csv") {
                        rows = readCsvRows(e.target.result);
                    } else {
                        if (!window.XLSX) {
                            alert("Chưa tải được thư viện đọc Excel. Bạn thử dùng file CSV trước nhé.");
                            return;
                        }
                        const workbook = XLSX.read(new Uint8Array(e.target.result), { type: "array" });
                        const sheet = workbook.Sheets[workbook.SheetNames[0]];
                        rows = XLSX.utils.sheet_to_json(sheet, { defval: "" });
                    }
                    if (!rows.length) {
                        alert("File không có dữ liệu sản phẩm để nhập.");
                        return;
                    }
                    await importProductRows(rows);
                } catch (error) {
                    alert("Không đọc được file sản phẩm. Bạn kiểm tra lại định dạng CSV/Excel nhé.");
                }
            };

            if (extension === "csv") reader.readAsText(file, "UTF-8");
            else reader.readAsArrayBuffer(file);
        }

        // 4. POS BÁN HÀNG & GIỎ HÀNG
        // Nhóm này xử lý quét/nhập SKU, thêm giỏ, tính tiền, thanh toán, in hóa đơn và báo cáo.
        function money(n) { return new Intl.NumberFormat('vi-VN').format(Math.max(0, n)) + " ₫"; }

        // Vẽ lại giỏ hàng ở màn hình POS.
        function renderCart() {
            const body = document.getElementById('cartBody');
            const empty = document.getElementById('cartEmpty');
            if (!body || !empty) return;
            body.innerHTML = "";
            cart.forEach((item, i) => {
                body.insertAdjacentHTML("beforeend", `
                        <tr>
                            <td>${i + 1}</td>
                            <td><strong>${escapeHtml(item.sku)}</strong></td>
                            <td>${escapeHtml(item.name)}</td>
                            <td>${escapeHtml(item.color || "Mặc định")}</td>
                            <td>${escapeHtml(item.size || "Free size")}</td>
                            <td>${escapeHtml(item.unit || "Cái")}</td>
                            <td class="money-cell">${money(item.price)}</td>
                            <td>
                                <div class="stepper">
                                    <button onclick="changeQty(${i},-1)">−</button>
                                    <input class="quantity-input" type="number" min="1" max="${getAvailableStock(item.sku)}" value="${item.qty}" aria-label="Số lượng ${escapeHtml(item.name)}" onchange="updateCartQuantity(${i}, this.value)">
                                    <button onclick="changeQty(${i},1)">+</button>
                                </div>
                            </td>
                            <td class="money-cell"><strong>${money(item.price * item.qty)}</strong></td>
                            <td><button class="icon-button" onclick="removeCart(${i})"><i data-lucide="x" style="width:17px"></i></button></td>
                        </tr>
                    `);
            });
            empty.classList.toggle("hidden", cart.length > 0);
            updateCart();
            if (window.lucide) lucide.createIcons();
        }

        function getAvailableStock(sku) {
            const product = productsData.find(x => x.sku === sku);
            return product ? Number(product.stock) || 0 : 0;
        }

        // Thêm sản phẩm vào giỏ bằng SKU, đồng thời kiểm tra còn tồn kho hay không.
        function addToCartBySku(sku) {
            const p = productsData.find(x => x.sku === sku);
            if (!p) return;
            const stock = getAvailableStock(sku);
            if (stock <= 0) {
                toast("Sản phẩm đã hết hàng");
                return;
            }
            const f = cart.find(x => x.sku === sku);
            if (f) {
                if (f.qty >= stock) {
                    toast("Số lượng trong giỏ đã bằng tồn kho");
                    return;
                }
                f.qty++;
            } else {
                cart.push({ ...p, stock, qty: 1 });
            }
            renderCart();
            toast("Đã thêm " + p.name + " vào giỏ");
        }

        // Tăng/giảm số lượng một dòng trong giỏ hàng.
        function changeQty(i, n) {
            const item = cart[i];
            if (!item) return;
            const stock = getAvailableStock(item.sku);
            const nextQty = item.qty + n;
            if (nextQty < 1) {
                cart.splice(i, 1);
            } else if (stock <= 0) {
                cart.splice(i, 1);
                toast("Sản phẩm đã hết hàng nên đã xóa khỏi giỏ");
            } else if (nextQty > stock) {
                toast("Số lượng trong giỏ đã bằng tồn kho");
                return;
            } else {
                item.stock = stock;
                item.qty = nextQty;
            }
            renderCart();
        }

        // Cho phép thu ngân nhập trực tiếp số lượng nhưng không vượt quá tồn kho.
        function updateCartQuantity(index, rawValue) {
            const item = cart[index];
            if (!item) return;
            const quantity = Number(rawValue);
            const stock = getAvailableStock(item.sku);
            if (!Number.isInteger(quantity) || quantity < 1) {
                toast("Số lượng phải là số nguyên lớn hơn 0");
                renderCart();
                return;
            }
            if (quantity > stock) {
                toast(`Chỉ còn ${stock} sản phẩm trong kho`);
                renderCart();
                return;
            }
            item.qty = quantity;
            item.stock = stock;
            renderCart();
        }

        // Xóa một sản phẩm khỏi giỏ.
        async function removeCart(i) {
            if (!await confirmDeletion("Sản phẩm này sẽ bị xóa khỏi giỏ hàng hiện tại. Bạn có muốn tiếp tục?", "Xóa khỏi giỏ hàng?")) return;
            cart.splice(i, 1);
            renderCart();
            toast("Đã xóa khỏi giỏ hàng");
        }

        // Xóa toàn bộ giỏ và bỏ mã khuyến mãi đang áp dụng.
        async function clearCart(skipConfirmation = false) {
            if (!cart.length) {
                toast("Giỏ hàng đang trống");
                return;
            }
            if (!skipConfirmation && !await confirmDeletion("Toàn bộ sản phẩm và mã khuyến mãi đang áp dụng sẽ bị xóa khỏi giỏ. Bạn có chắc muốn tiếp tục?", "Xóa toàn bộ giỏ hàng?")) return;
            cart = [];
            appliedPromotion = null;
            const promoInput = document.getElementById("promoCodeInput");
            if (promoInput) promoInput.value = "";
            renderCart();
            toast("Đã xóa toàn bộ giỏ hàng");
        }

        // Tìm khách theo số đang nhập; để trống vẫn bán hàng bình thường.
        function getCurrentSaleCustomer() {
            return findCustomerByPhone(document.getElementById("customerSearch")?.value || "");
        }

        // Xóa thông tin khách sau khi hoàn tất hóa đơn, không tác động tới giỏ hàng.
        function resetSaleCustomer() {
            const phoneInput = document.getElementById("customerSearch");
            if (phoneInput) phoneInput.value = "";
            updateCustomerInfo();
        }

        // Dùng lại tên và số điện thoại của khách đã chọn; chỉ yêu cầu nhập tay khi không tìm thấy khách.
        function syncDeliveryCustomerFields() {
            const customer = getCurrentSaleCustomer();
            const summary = document.getElementById("deliveryCustomerSummary");
            const manualFields = document.getElementById("deliveryManualCustomerFields");
            const nameInput = document.getElementById("codReceiverName");
            const phoneInput = document.getElementById("codReceiverPhone");
            if (customer) {
                if (nameInput) nameInput.value = customer.name || "";
                if (phoneInput) phoneInput.value = customer.phone || "";
                if (document.getElementById("deliveryCustomerName")) document.getElementById("deliveryCustomerName").textContent = customer.name || "";
                if (document.getElementById("deliveryCustomerPhone")) document.getElementById("deliveryCustomerPhone").textContent = customer.phone || "";
                summary?.classList.remove("hidden");
                manualFields?.classList.add("hidden");
                deliveryCustomerAutoFilled = true;
            } else {
                if (deliveryCustomerAutoFilled) {
                    if (nameInput) nameInput.value = "";
                    if (phoneInput) phoneInput.value = "";
                }
                summary?.classList.add("hidden");
                manualFields?.classList.remove("hidden");
                deliveryCustomerAutoFilled = false;
            }
        }

        // Khi số điện thoại khớp, hiện ngay tên và số của khách; không khớp thì vẫn cho bán.
        function handleCustomerPhoneInput() {
            const phone = document.getElementById("customerSearch")?.value.trim() || "";
            const status = document.getElementById("customerStepStatus");
            const customer = findCustomerByPhone(phone);
            if (status) {
                status.classList.toggle("hidden", !customer);
                status.innerHTML = customer
                    ? `<strong>${escapeHtml(customer.name)}</strong><span>${escapeHtml(customer.phone)}</span>`
                    : "";
            }
            syncDeliveryCustomerFields();
            updateCart();
        }

        function handleCustomerPhoneKey(event) {
            if (event.key === "Enter") {
                event.preventDefault();
                document.getElementById("skuSearch")?.focus();
                return false;
            }
            return allowPhoneKey(event);
        }

        // Hiện khách đang gắn với hóa đơn, điểm và hạng ngay trên POS.
        function updateCustomerInfo() {
            const info = document.getElementById("customerInfo");
            const status = document.getElementById("customerStepStatus");
            const customer = getCurrentSaleCustomer();
            if (customer) {
                const rank = getCustomerRank(customer);
                const discount = getCustomerDiscountRate(customer);
                if (info) info.innerHTML = `<div><strong>${escapeHtml(customer.name)}</strong><small>${escapeHtml(customer.phone)}</small></div><div class="customer-actions"><span>${rank}${discount ? " -" + discount + "%" : ""}</span></div>`;
                if (status) {
                    status.classList.remove("hidden");
                    status.innerHTML = `<strong>${escapeHtml(customer.name)}</strong><span>${escapeHtml(customer.phone)}</span>`;
                }
            } else {
                if (info) info.innerHTML = "";
                if (status) {
                    status.classList.add("hidden");
                    status.innerHTML = "";
                }
            }
            syncDeliveryCustomerFields();
            updateCart();
            if (window.lucide) lucide.createIcons();
        }

        // Tính tổng tiền: tiền hàng, giảm theo hạng, giảm voucher và số cần thanh toán.
        function calculateCartSummary() {
            const subtotal = cart.reduce((s, x) => s + x.price * x.qty, 0);
            const customer = getCurrentSaleCustomer();
            const memberRate = customer ? getCustomerDiscountRate(customer) : 0;
            const memberDiscount = Math.round(subtotal * memberRate / 100);
            let promoDiscount = 0;
            let promo = null;
            if (appliedPromotion && isPromotionActive(appliedPromotion, subtotal)) {
                promo = appliedPromotion;
                promoDiscount = Math.round(subtotal * (Number(promo.percent) || 0) / 100);
            }
            const discount = memberDiscount + promoDiscount;
            const taxableAmount = Math.max(0, subtotal - discount);
            const vatAmount = Math.round(taxableAmount * VAT_RATE / 100);
            const shippingFee = getDeliveryMode() ? Math.max(0, Number(document.getElementById("shippingFee")?.value) || 0) : 0;
            const total = taxableAmount + vatAmount + shippingFee;
            return { subtotal, discount, memberDiscount, memberRate, promoDiscount, taxableAmount, vatAmount, shippingFee, total, promo, customer };
        }

        // Đẩy kết quả tính tiền lên giao diện POS.
        function updateCart() {
            if (!document.getElementById("subtotal")) return;
            const { subtotal, discount, memberDiscount, memberRate, vatAmount, shippingFee, total, promo, customer } = calculateCartSummary();
            const cash = Number(document.getElementById("cashInput").value) || 0;

            document.getElementById("subtotal").textContent = money(subtotal);
            const discountParts = [];
            if (memberDiscount > 0) discountParts.push(`Hạng ${getCustomerRank(customer)} ${memberRate}%`);
            if (promo) discountParts.push(`${promo.code} ${promo.percent}%`);
            document.getElementById("discountLabel").textContent = discountParts.length ? discountParts.join(" + ") : "Khuyến mãi";
            document.getElementById("discount").textContent = discount > 0 ? "- " + money(discount) : "0 ₫";
            document.getElementById("vatAmount").textContent = money(vatAmount);
            if (document.getElementById("shippingAmount")) document.getElementById("shippingAmount").textContent = money(shippingFee);
            document.getElementById("grandTotal").textContent = money(total);
            document.getElementById("change").textContent = money(cash - total);
        }

        // Điền nhanh số tiền khách đưa.
        function setCash(x) { document.getElementById("cashInput").value = x; updateCart(); }
        // Điền đúng số tiền cần thanh toán.
        function setExact() {
            const { total } = calculateCartSummary();
            document.getElementById("cashInput").value = total;
            updateCart();
        }

        // Áp mã khuyến mãi vào giỏ nếu mã còn hạn và đủ điều kiện.
        function applyPromotionCode() {
            const input = document.getElementById("promoCodeInput");
            const code = input.value.trim().toUpperCase();
            const subtotal = cart.reduce((s, x) => s + x.price * x.qty, 0);
            if (!cart.length) {
                toast("Giỏ hàng đang trống!");
                return;
            }
            if (!code) {
                appliedPromotion = null;
                updateCart();
                toast("Đã bỏ mã khuyến mãi");
                return;
            }
            const promo = promotionsData.find(item => item.code.toUpperCase() === code);
            if (!promo) {
                toast("Mã khuyến mãi không tồn tại");
                return;
            }
            if (!isPromotionActive(promo, subtotal)) {
                toast("Mã chưa hợp lệ, hết hạn hoặc chưa đủ điều kiện");
                return;
            }
            appliedPromotion = promo;
            updateCart();
            toast("Đã áp dụng mã " + promo.code);
        }

        function getDeliveryMode() {
            if (!document.getElementById("deliveryToggle")?.checked) return "";
            return document.querySelector('input[name="deliveryMode"]:checked')?.value || "DELIVERY";
        }

        // Chỉ hiện thông tin giao nhận khi khách yêu cầu giao tận nơi.
        function toggleShip() {
            const enabled = document.getElementById("deliveryToggle")?.checked || false;
            document.getElementById("shipFields")?.classList.toggle("open", enabled);
            if (enabled && !document.querySelector('input[name="deliveryMode"]:checked')) {
                const first = document.querySelector('input[name="deliveryMode"][value="DELIVERY"]');
                if (first) first.checked = true;
            }
            if (enabled) syncDeliveryCustomerFields();
            updateDeliveryMode();
        }

        function updateDeliveryMode() {
            const mode = getDeliveryMode();
            const isCod = mode === "COD";
            document.getElementById("paymentSection")?.classList.toggle("hidden", isCod);
            document.getElementById("cashSection")?.classList.toggle("hidden", isCod);
            const buttonLabel = document.getElementById("checkoutButtonLabel");
            if (buttonLabel) buttonLabel.textContent = isCod ? "XÁC NHẬN ĐƠN COD" : mode === "DELIVERY" ? "THANH TOÁN & TẠO ĐƠN GIAO" : "THANH TOÁN & XUẤT HÓA ĐƠN";
            if (isCod) {
                const cashInput = document.getElementById("cashInput");
                if (cashInput) cashInput.value = 0;
            } else {
                handlePaymentChange();
            }
            updateCart();
        }

        // Lấy phương thức thanh toán nhân viên đang chọn.
        function getSelectedPaymentMethod() {
            return document.querySelector('input[name="paymentMethod"]:checked')?.value || "Tiền mặt";
        }

        // Lấy đúng nhân viên đang đăng nhập và kiểm tra người đó vẫn còn hoạt động trong hệ thống.
        function getCurrentEmployee() {
            const session = window.currentEmployee || {};
            const employee = partnersData.find(item =>
                item.category === "Nhân viên" && item.id === session.id
            );
            return employee ? { id: employee.id, name: employee.name } : null;
        }

        // Đổi cách nhập tiền khi chọn tiền mặt, QR hoặc thẻ.
        function handlePaymentChange() {
            const paymentMethod = getSelectedPaymentMethod();
            const cashInput = document.getElementById("cashInput");
            if (paymentMethod === "Tiền mặt") {
                cashInput.disabled = false;
            } else {
                setExact();
                cashInput.disabled = true;
            }
            updateCart();
        }

        // Lấy thông tin khách để đưa lên hóa đơn.
        function parseReceiptCustomer() {
            const raw = document.getElementById("customerSearch").value.trim();
            const found = findCustomerByPhone(raw);
            if (found) {
                return {
                    name: found.name,
                    phone: found.phone,
                    rank: getCustomerRank(found),
                    points: getCustomerPoints(found),
                    totalSpent: getCustomerTotalSpent(found)
                };
            }
            return {
                name: raw ? "Khách lẻ (" + raw + ")" : "Khách lẻ",
                phone: raw,
                rank: "Khách vãng lai",
                points: 0
            };
        }

        // Che bớt số điện thoại khi in hóa đơn.
        function maskPhone(phone) {
            const value = String(phone || "").replace(/\D/g, "");
            if (value.length < 7) return value || "-";
            return value.slice(0, 4) + "***" + value.slice(-3);
        }

        // Sinh mã hóa đơn theo ngày và vài số cuối thời gian hiện tại.
        function makeInvoiceId() {
            const d = new Date();
            const ymd = String(d.getFullYear()).slice(-2)
                + String(d.getMonth() + 1).padStart(2, "0")
                + String(d.getDate()).padStart(2, "0");
            return "HD" + ymd + "-" + String(Date.now()).slice(-4);
        }

        // Định dạng ngày giờ kiểu dễ đọc trên hóa đơn.
        function formatDateTime(date) {
            return new Intl.DateTimeFormat("vi-VN", {
                day: "2-digit",
                month: "2-digit",
                year: "numeric",
                hour: "2-digit",
                minute: "2-digit"
            }).format(date).replace(",", "");
        }

        // Tạo khóa ngày yyyy-mm-dd để gom doanh thu theo từng ngày.
        function getDateKey(date = new Date()) {
            return date.getFullYear() + "-"
                + String(date.getMonth() + 1).padStart(2, "0") + "-"
                + String(date.getDate()).padStart(2, "0");
        }

        // Lấy danh sách 30 ngày gần nhất cho trang báo cáo.
        function getLast30DateKeys() {
            return Array.from({ length: 30 }, (_, idx) => {
                const date = new Date();
                date.setHours(0, 0, 0, 0);
                date.setDate(date.getDate() - (29 - idx));
                return getDateKey(date);
            });
        }

        // Chỉ giữ doanh thu trong 30 ngày gần nhất như yêu cầu báo cáo.
        function pruneSalesHistory() {
            const allowed = new Set(getLast30DateKeys());
            salesHistory = salesHistory.filter(item => allowed.has(item.dateKey));
        }

        // Đưa bộ lọc báo cáo về ngày hôm nay.
        function setReportToday() {
            const input = document.getElementById("reportDate");
            if (input) input.value = getDateKey();
            renderReport();
        }

        // Di chuyển nhanh giữa các ngày trong phạm vi 30 ngày báo cáo.
        function changeReportDate(offset) {
            const input = document.getElementById("reportDate");
            if (!input) return;
            const dates = getLast30DateKeys();
            const current = dates.indexOf(input.value || getDateKey());
            const next = Math.min(dates.length - 1, Math.max(0, current + offset));
            input.value = dates[next];
            renderReport();
        }

        // Ghép ngày và giờ để in lên phiếu kết ca.
        function formatReportDateTime(dateKey, timeText) {
            const [year, month, day] = dateKey.split("-");
            return `${day}/${month}/${year} ${timeText}`;
        }

        // Sinh mã ca bán hàng theo ngày.
        function makeShiftId(dateKey) {
            const compact = dateKey.replaceAll("-", "").slice(2);
            return `CA${compact}-01`;
        }

        // Lấy các hóa đơn đã ghi nhận trong một ngày.
        function getSalesByDate(dateKey) {
            pruneSalesHistory();
            return salesHistory.filter(item => item.dateKey === dateKey);
        }

        // Gom phương thức thanh toán về 3 nhóm báo cáo: tiền mặt, QR, thẻ.
        function getPaymentBucket(paymentMethod) {
            if (paymentMethod === "Quét mã QR") return "qr";
            if (paymentMethod === "Thẻ") return "card";
            return "cash";
        }

        // Tính toàn bộ số liệu cần in trên phiếu kết ca.
        function createShiftSummary(dateKey) {
            const sales = getSalesByDate(dateKey);
            const openingCash = 2000000;
            const cashExpense = 0;
            const payments = {
                cash: { label: "Tiền mặt", orders: 0, total: 0 },
                qr: { label: "Chuyển khoản VietQR", orders: 0, total: 0 },
                card: { label: "Quẹt thẻ POS", orders: 0, total: 0 }
            };

            sales.forEach(sale => {
                const bucket = getPaymentBucket(sale.paymentMethod);
                payments[bucket].orders += 1;
                payments[bucket].total += sale.total;
            });

            const totalRevenue = sales.reduce((sum, sale) => sum + sale.total, 0);
            const cashSales = payments.cash.total;
            const expectedCash = openingCash + cashSales - cashExpense;

            return {
                shiftId: makeShiftId(dateKey),
                dateKey,
                cashier: getCurrentEmployee()?.name || "Nhân viên",
                openedAt: formatReportDateTime(dateKey, "14:00"),
                closedAt: formatReportDateTime(dateKey, dateKey === getDateKey() ? new Intl.DateTimeFormat("vi-VN", { hour: "2-digit", minute: "2-digit" }).format(new Date()) : "22:00"),
                orderCount: sales.length,
                payments,
                totalRevenue,
                openingCash,
                cashSales,
                cashExpense,
                expectedCash,
                countedCash: expectedCash,
                cashDiff: 0
            };
        }

        // Lưu lại phiếu kết ca của ngày đã in để có thể mở lại sau.
        function saveShiftReport(summary) {
            const index = shiftReports.findIndex(item => item.dateKey === summary.dateKey);
            if (index >= 0) {
                shiftReports[index] = summary;
            } else {
                shiftReports.push(summary);
            }
        }

        // Ghi một hóa đơn đã thanh toán vào lịch sử doanh thu.
        // COD chỉ gọi hàm này khi đơn đã giao thành công.
        function recordPaidReceipt(receipt, source = "POS") {
            if (!receipt || receipt.reportRecorded) return;
            const now = new Date();
            const itemCount = receipt.items.reduce((sum, item) => sum + item.qty, 0);
            salesHistory.push({
                id: receipt.id,
                dateKey: getDateKey(now),
                time: new Intl.DateTimeFormat("vi-VN", { hour: "2-digit", minute: "2-digit" }).format(now),
                customerName: receipt.customer?.name || "Khách lẻ",
                paymentMethod: receipt.paymentMethod,
                source,
                itemCount,
                total: Number(receipt.total) || 0,
                items: receipt.items.map(item => ({
                    sku: item.sku,
                    name: item.name,
                    qty: item.qty,
                    total: item.total
                }))
            });
            receipt.reportRecorded = true;
            pruneSalesHistory();
            renderReport();
        }

        // Định dạng tiền không kèm ký hiệu, dùng trong giấy in.
        function moneyPlain(n) {
            return new Intl.NumberFormat("vi-VN").format(Math.max(0, Number(n) || 0));
        }

        // Chống lỗi vỡ HTML khi tên khách/sản phẩm có ký tự đặc biệt.
        function escapeHtml(value) {
            return String(value ?? "")
                .replace(/&/g, "&amp;")
                .replace(/</g, "&lt;")
                .replace(/>/g, "&gt;")
                .replace(/"/g, "&quot;")
                .replace(/'/g, "&#039;");
        }

        // Chụp lại thông tin hóa đơn tại thời điểm thanh toán để sau này in không bị lệch dữ liệu.
        function buildReceiptSnapshot(paymentMethod, deliveryMode) {
            const { subtotal, discount, memberDiscount, memberRate, promoDiscount, vatAmount, shippingFee, total, promo } = calculateCartSummary();
            const isCod = deliveryMode === "COD";
            const cash = isCod ? 0 : paymentMethod === "Tiền mặt" ? Number(document.getElementById("cashInput").value) || 0 : total;
            const customer = parseReceiptCustomer();
            const employee = getCurrentEmployee();
            const itemCount = cart.reduce((s, x) => s + x.qty, 0);

            return {
                id: makeInvoiceId(),
                printedAt: formatDateTime(new Date()),
                employeeId: employee?.id || "",
                cashier: employee?.name || "Nhân viên",
                customer,
                items: cart.map(item => ({
                    sku: item.sku,
                    name: item.name,
                    color: item.color || "Theo sản phẩm",
                    size: item.size || "Free size",
                    unit: item.unit || "Cái",
                    qty: item.qty,
                    price: Number(item.price) || 0,
                    total: (Number(item.price) || 0) * item.qty
                })),
                itemCount,
                subtotal,
                discount,
                memberDiscount,
                memberRate,
                promoDiscount,
                promoCode: promo ? promo.code : "",
                promoPercent: promo ? promo.percent : 0,
                vatRate: VAT_RATE,
                vatAmount,
                shippingFee,
                total,
                paymentMethod: isCod ? "COD / Thu hộ" : paymentMethod,
                paid: cash,
                earnedPoints: Math.floor(total / 10000),
                isCod,
                deliveryMode,
                source: "POS"
            };
        }

        // Tạo HTML hóa đơn bán hàng giống mẫu bill.
        function renderReceipt(receipt) {
            const itemsHtml = receipt.items.map(item => `
                <div class="receipt-item">
                    <div class="receipt-item-name">${escapeHtml(item.name)}</div>
                    <div class="receipt-muted">Mã: ${escapeHtml(item.sku)} | Màu sắc: ${escapeHtml(item.color)} | Kích cỡ: ${escapeHtml(item.size)}</div>
                    <div class="receipt-row"><span>${item.qty} ${escapeHtml(item.unit || "Cái")} x ${moneyPlain(item.price)}</span><span>${moneyPlain(item.total)}</span></div>
                </div>
            `).join("");

            return `
                <div class="receipt-logo">yo<span>dy</span></div>
                <div class="receipt-center"><strong>CỬA HÀNG YODY TÂN SƠN NHÌ</strong></div>
                <div class="receipt-center">366 Tân Sơn Nhì, P. Tân Sơn Nhì, Q. Tân Phú, TPHCM</div>
                <div class="receipt-center">Hotline: 1800 2086 - Web: yody.vn</div>
                <div class="receipt-line"></div>
                <div class="receipt-title">HÓA ĐƠN THANH TOÁN</div>
                <div class="receipt-row"><span>Số HĐ:</span><strong>${escapeHtml(receipt.id)}</strong></div>
                <div class="receipt-row"><span>Ngày lập:</span><span>${escapeHtml(receipt.printedAt)}</span></div>
                <div class="receipt-row"><span>Thu ngân:</span><span>${escapeHtml(receipt.cashier)}</span></div>
                <div class="receipt-row"><span>Khách hàng:</span><span>${escapeHtml(receipt.customer.name)} ${receipt.customer.phone ? "(" + escapeHtml(maskPhone(receipt.customer.phone)) + ")" : ""}</span></div>
                <div class="receipt-row"><span>Hạng thẻ:</span><span>${escapeHtml(receipt.customer.rank)} (Điểm tích: ${receipt.customer.points})</span></div>
                <div class="receipt-line"></div>
                ${itemsHtml}
                <div class="receipt-line"></div>
                <div class="receipt-row"><span>Tổng tiền hàng (${receipt.itemCount} SP):</span><span>${moneyPlain(receipt.subtotal)}</span></div>
                ${receipt.memberDiscount !== undefined ? `<div class="receipt-row"><span>Chiết khấu hạng thẻ ${receipt.memberRate ? "(" + receipt.memberRate + "%)" : ""}:</span><span>${receipt.memberDiscount > 0 ? "-" + moneyPlain(receipt.memberDiscount) : "0"}</span></div><div class="receipt-row"><span>Voucher ${receipt.promoCode ? receipt.promoCode + " (" + (receipt.promoPercent || 0) + "%)" : "chưa áp dụng"}:</span><span>${receipt.promoDiscount > 0 ? "-" + moneyPlain(receipt.promoDiscount) : "0"}</span></div>` : `<div class="receipt-row"><span>Chiết khấu${receipt.promoCode ? " (" + escapeHtml(receipt.promoCode) + ")" : ""}:</span><span>${receipt.discount > 0 ? "-" + moneyPlain(receipt.discount) : "0"}</span></div>`}
                <div class="receipt-row"><span>Thuế VAT (${receipt.vatRate || VAT_RATE}%):</span><span>${moneyPlain(receipt.vatAmount)}</span></div>
                ${receipt.deliveryMode ? `<div class="receipt-row"><span>Phí giao hàng:</span><span>${moneyPlain(receipt.shippingFee || 0)}</span></div>` : ""}
                <div class="receipt-solid"></div>
                <div class="receipt-row receipt-total"><span>TỔNG THANH TOÁN:</span><span>${moneyPlain(receipt.total)} VNĐ</span></div>
                <div class="receipt-line"></div>
                <div class="receipt-row"><span>Phương thức:</span><strong>${escapeHtml(receipt.paymentMethod)}</strong></div>
                ${receipt.cardLast4 ? `<div class="receipt-row"><span>Thông tin thẻ:</span><span>${escapeHtml(receipt.cardBrand)} •••• ${escapeHtml(receipt.cardLast4)}</span></div>` : ""}
                ${receipt.cardTransactionId ? `<div class="receipt-row"><span>Mã giao dịch:</span><span>${escapeHtml(receipt.cardTransactionId)}</span></div>` : ""}
                <div class="receipt-row"><span>Tiền khách trả:</span><span>${moneyPlain(receipt.paid)}</span></div>
                <div class="receipt-row"><span>Điểm tích lũy cộng thêm:</span><span>+${receipt.earnedPoints} điểm</span></div>
                <div class="receipt-line"></div>
                <div class="receipt-barcode">||| || ||||| |||| |</div>
                <div class="receipt-center">${escapeHtml(receipt.id.replace("-", ""))}</div>
                <div class="receipt-note">
                    Quý khách vui lòng giữ hóa đơn để đổi sản phẩm trong 15 ngày.<br>
                    Hàng đổi phải còn nguyên tem mác và chưa qua sử dụng.
                </div>
            `;
        }

        // Vẽ KPI và biểu đồ doanh thu của ngày đang chọn trong phạm vi 30 ngày.
        function renderReport() {
            const dateInput = document.getElementById("reportDate");
            if (!dateInput) return;

            const allowedDates = getLast30DateKeys();
            const todayKey = getDateKey();
            if (!dateInput.value) dateInput.value = todayKey;
            if (!allowedDates.includes(dateInput.value)) dateInput.value = todayKey;

            pruneSalesHistory();
            const selectedDate = dateInput.value;
            const daySales = salesHistory.filter(item => item.dateKey === selectedDate);
            const monthSales = salesHistory.filter(item => allowedDates.includes(item.dateKey));
            const dayRevenue = daySales.reduce((sum, item) => sum + item.total, 0);
            const monthRevenue = monthSales.reduce((sum, item) => sum + item.total, 0);
            const dayItems = daySales.reduce((sum, item) => sum + item.itemCount, 0);
            const dayCod = daySales.filter(item => item.source === "COD").length;

            document.getElementById("reportDayRevenue").textContent = money(dayRevenue);
            document.getElementById("reportDayOrders").textContent = daySales.length + " hóa đơn";
            document.getElementById("reportDayItems").textContent = dayItems + " SP";
            document.getElementById("reportDayCod").textContent = dayCod + " đơn";
            document.getElementById("reportMonthRevenue").textContent = money(monthRevenue);
            document.getElementById("reportEmptyState")?.classList.toggle("hidden", daySales.length > 0);

            if (window.Chart) {
                const dailyRevenue = allowedDates.map(dateKey =>
                    monthSales.filter(item => item.dateKey === dateKey).reduce((sum, item) => sum + item.total, 0)
                );
                const revenueCtx = document.getElementById("revenueChart");
                if (revenueCtx) {
                    if (revenueChartInstance) revenueChartInstance.destroy();
                    revenueChartInstance = new Chart(revenueCtx, {
                        type: "bar",
                        data: {
                            labels: allowedDates.map(date => date.slice(5)),
                            datasets: [{
                                label: "Doanh thu",
                                data: dailyRevenue,
                                backgroundColor: "#2563eb",
                                borderRadius: 6
                            }]
                        },
                        options: {
                            responsive: true,
                            maintainAspectRatio: false,
                            plugins: { legend: { display: false } },
                            scales: { y: { ticks: { callback: value => money(value) } } }
                        }
                    });
                }

                const topMap = new Map();
                daySales.forEach(sale => {
                    sale.items.forEach(item => {
                        const current = topMap.get(item.sku) || { name: item.name, qty: 0 };
                        current.qty += item.qty;
                        topMap.set(item.sku, current);
                    });
                });
                const topItems = [...topMap.values()].sort((a, b) => b.qty - a.qty).slice(0, 5);
                const topCtx = document.getElementById("topChart");
                if (topCtx) {
                    if (topChartInstance) topChartInstance.destroy();
                    topChartInstance = new Chart(topCtx, {
                        type: "doughnut",
                        data: {
                            labels: topItems.length ? topItems.map(item => item.name) : ["Chưa có dữ liệu"],
                            datasets: [{
                                data: topItems.length ? topItems.map(item => item.qty) : [1],
                                backgroundColor: ["#2563eb", "#16a34a", "#f59e0b", "#ef4444", "#7c3aed"]
                            }]
                        },
                        options: {
                            responsive: true,
                            maintainAspectRatio: false,
                            plugins: { legend: { position: "bottom" } }
                        }
                    });
                }
            }
        }

        // Tạo HTML phiếu kết ca bán hàng theo mẫu Z-Report.
        function renderShiftReport(summary) {
            const cashStatus = summary.cashDiff === 0 ? "Khớp" : (summary.cashDiff > 0 ? "Thừa" : "Thiếu");
            return `
                <div class="receipt-logo">yo<span>dy</span></div>
                <div class="receipt-center"><strong>YODY TÂN SƠN NHÌ</strong></div>
                <div class="receipt-center">366 Tân Sơn Nhì, Q. Tân Phú, TPHCM</div>
                <div class="receipt-line"></div>
                <div class="receipt-title">PHIẾU KẾT CA BÁN HÀNG</div>
                <div class="receipt-center">(Báo cáo Z-Report)</div>
                <div class="receipt-spacer"></div>
                <div class="receipt-row"><span>Mã ca:</span><strong>${escapeHtml(summary.shiftId)}</strong></div>
                <div class="receipt-row"><span>Thu ngân:</span><span>${escapeHtml(summary.cashier)}</span></div>
                <div class="receipt-row"><span>Thời gian mở:</span><span>${escapeHtml(summary.openedAt)}</span></div>
                <div class="receipt-row"><span>Thời gian chốt:</span><span>${escapeHtml(summary.closedAt)}</span></div>
                <div class="receipt-row"><span>Tổng đơn hoàn tất:</span><span>${summary.orderCount} đơn</span></div>
                <div class="receipt-line"></div>
                <div class="receipt-section-title">1. DOANH THU THEO THANH TOÁN</div>
                <div class="receipt-row"><span>- Tiền mặt (${summary.payments.cash.orders} đơn):</span><span>${moneyPlain(summary.payments.cash.total)}</span></div>
                <div class="receipt-row"><span>- Chuyển khoản VietQR (${summary.payments.qr.orders} đơn):</span><span>${moneyPlain(summary.payments.qr.total)}</span></div>
                <div class="receipt-row"><span>- Quẹt thẻ POS (${summary.payments.card.orders} đơn):</span><span>${moneyPlain(summary.payments.card.total)}</span></div>
                <div class="receipt-solid"></div>
                <div class="receipt-row receipt-total"><span>TỔNG DOANH THU CA:</span><span>${moneyPlain(summary.totalRevenue)} đ</span></div>
                <div class="receipt-line"></div>
                <div class="receipt-section-title">2. KIỂM KÊ TIỀN MẶT TẠI KÉT</div>
                <div class="receipt-row"><span>(+) Quỹ thối đầu ca:</span><span>${moneyPlain(summary.openingCash)}</span></div>
                <div class="receipt-row"><span>(+) Bán hàng tiền mặt:</span><span>${moneyPlain(summary.cashSales)}</span></div>
                <div class="receipt-row"><span>(-) Chi tiền mặt tại ca:</span><span>-${moneyPlain(summary.cashExpense)}</span></div>
                <div class="receipt-solid"></div>
                <div class="receipt-row receipt-total"><span>TIỀN PHẢI NỘP:</span><span>${moneyPlain(summary.expectedCash)} đ</span></div>
                <div class="receipt-row receipt-total"><span>TIỀN THỰC ĐẾM TRONG KÉT:</span><span>${moneyPlain(summary.countedCash)} đ</span></div>
                <div class="receipt-row"><span>Chênh lệch két:</span><span>${moneyPlain(Math.abs(summary.cashDiff))} đ (${cashStatus})</span></div>
                <div class="receipt-solid"></div>
                <div class="receipt-signatures">
                    <div><strong>Thu ngân kết ca</strong><br><span>(Ký nộp đủ tiền)</span></div>
                    <div><strong>Quản lý nhận tiền</strong><br><span>(Ký niêm phong)</span></div>
                </div>
                <div class="receipt-sign-line">....................</div>
            `;
        }

        // Mở phiếu kết ca cho ngày đang chọn trong báo cáo.
        function openShiftReportModal() {
            const dateKey = document.getElementById("reportDate")?.value || getDateKey();
            const summary = createShiftSummary(dateKey);
            saveShiftReport(summary);
            document.getElementById("shiftReportContent").innerHTML = renderShiftReport(summary);
            document.getElementById("shiftReportModalBg").classList.add("open");
            if (window.lucide) lucide.createIcons();
        }

        // Đóng phiếu kết ca.
        function closeShiftReportModal() {
            document.getElementById("shiftReportModalBg").classList.remove("open");
        }

        // In riêng phiếu kết ca, không in lẫn hóa đơn.
        function printShiftReport() {
            setPrintMode("printing-shift");
            window.print();
        }

        // Mở modal xem và in hóa đơn vừa thanh toán.
        function openReceiptModal(receipt) {
            lastReceipt = receipt;
            document.getElementById("receiptContent").innerHTML = renderReceipt(receipt);
            document.getElementById("receiptModalBg").classList.add("open");
            if (window.lucide) lucide.createIcons();
        }

        // Mở danh sách hóa đơn lấy trực tiếp từ SQL Server.
        function openInvoiceHistory() {
            document.getElementById("invoiceHistoryModalBg").classList.add("open");
            const search = document.getElementById("invoiceHistorySearch");
            if (search) search.value = "";
            loadInvoiceHistory();
        }

        function closeInvoiceHistory() {
            document.getElementById("invoiceHistoryModalBg").classList.remove("open");
            clearTimeout(invoiceSearchTimer);
            invoiceSearchRequestId += 1;
        }

        // Chờ người dùng ngừng nhập một chút rồi tự tìm hóa đơn theo số điện thoại.
        function scheduleInvoiceHistorySearch() {
            clearTimeout(invoiceSearchTimer);
            invoiceSearchTimer = setTimeout(loadInvoiceHistory, 300);
        }

        async function loadInvoiceHistory() {
            const body = document.getElementById("invoiceHistoryBody");
            if (!body) return;
            const requestId = ++invoiceSearchRequestId;
            body.innerHTML = `<tr><td colspan="8" class="table-message">Đang tải hóa đơn...</td></tr>`;
            const keyword = document.getElementById("invoiceHistorySearch")?.value.trim() || "";
            try {
                const response = await fetch(`${API_BASE_URL}/api/hoa-don?q=${encodeURIComponent(keyword)}`, { cache: "no-store" });
                if (!response.ok) throw new Error(await response.text());
                const invoices = await response.json();
                if (requestId !== invoiceSearchRequestId) return;
                body.innerHTML = invoices.length ? invoices.map(invoice => `
                    <tr>
                        <td><strong>${escapeHtml(invoice.id)}</strong></td>
                        <td>${escapeHtml(invoice.createdAt)}</td>
                        <td>${escapeHtml(invoice.customerName)}${invoice.phone ? `<small class="cell-note">${escapeHtml(invoice.phone)}</small>` : ""}</td>
                        <td>${escapeHtml(invoice.cashier)}</td>
                        <td>${escapeHtml(invoice.paymentMethod)}</td>
                        <td>${invoice.itemCount}</td>
                        <td class="money-cell"><strong>${money(invoice.total)}</strong></td>
                        <td class="row-actions"><button class="icon-button" type="button" title="Xem hóa đơn" onclick="openStoredInvoice('${escapeHtml(invoice.id)}', false)"><i data-lucide="eye"></i></button><button class="icon-button" type="button" title="In lại hóa đơn" onclick="openStoredInvoice('${escapeHtml(invoice.id)}', true)"><i data-lucide="printer"></i></button></td>
                    </tr>`).join("") : `<tr><td colspan="8" class="table-message">Không tìm thấy hóa đơn phù hợp.</td></tr>`;
                if (window.lucide) lucide.createIcons();
            } catch (error) {
                if (requestId !== invoiceSearchRequestId) return;
                console.error(error);
                body.innerHTML = `<tr><td colspan="8" class="table-message error-text">Không tải được lịch sử hóa đơn từ cơ sở dữ liệu.</td></tr>`;
            }
        }

        async function openStoredInvoice(invoiceId, printAfterOpen) {
            try {
                const response = await fetch(`${API_BASE_URL}/api/hoa-don/${encodeURIComponent(invoiceId)}`, { cache: "no-store" });
                if (!response.ok) throw new Error(await response.text());
                const data = await response.json();
                const receipt = {
                    ...data,
                    customer: { name: data.customerName, phone: data.customerPhone, rank: data.customerRank, points: data.customerPoints },
                    promoPercent: 0,
                    isReprint: true
                };
                closeInvoiceHistory();
                openReceiptModal(receipt);
                if (printAfterOpen) setTimeout(printReceipt, 120);
            } catch (error) {
                console.error(error);
                toast("Không mở được chi tiết hóa đơn");
            }
        }

        // Vẽ danh sách sản phẩm còn trong kho để nhân viên chọn vào giỏ.
        function renderStockPicker() {
            const tbody = document.getElementById("stockPickerBody");
            if (!tbody) return;
            const keyword = (document.getElementById("stockPickerSearch")?.value || "").trim().toLowerCase();
            const items = productsData.filter(p => {
                const haystack = `${p.sku || ""} ${p.name || ""}`.toLowerCase();
                return haystack.includes(keyword);
            });

            if (items.length === 0) {
                tbody.innerHTML = `<tr><td colspan="8" style="text-align:center; padding:24px; color:#94a3b8;">Chưa có sản phẩm phù hợp trong kho.</td></tr>`;
                return;
            }

            tbody.innerHTML = items.map(p => {
                const originalIndex = productsData.indexOf(p);
                return `
                <tr>
                    <td><strong>${escapeHtml(p.sku)}</strong></td>
                    <td>${escapeHtml(p.name)}</td>
                    <td>${escapeHtml(p.color || "Mặc định")}</td>
                    <td>${escapeHtml(p.size || "Free size")}</td>
                    <td>${escapeHtml(p.unit || "Cái")}</td>
                    <td class="money-cell">${money(Number(p.price) || 0)}</td>
                    <td><span class="badge ${p.stock < 10 ? 'red' : 'green'}">${p.stock}</span></td>
                    <td>
                        <button class="btn ${Number(p.stock) <= 0 ? "btn-secondary" : "btn-primary"}" type="button" onclick="addStockPickerItem(${originalIndex})">
                            ${Number(p.stock) <= 0 ? "Hết hàng" : "Thêm vào giỏ"}
                        </button>
                    </td>
                </tr>
            `;
            }).join("");
        }

        // Mở cửa sổ chọn sản phẩm từ kho.
        function openStockPicker() {
            document.getElementById("stockPickerModalBg").classList.add("open");
            const search = document.getElementById("stockPickerSearch");
            if (search) search.value = "";
            renderStockPicker();
            setTimeout(() => search?.focus(), 50);
            if (window.lucide) lucide.createIcons();
        }

        // Đóng cửa sổ chọn sản phẩm từ kho.
        function closeStockPicker() {
            document.getElementById("stockPickerModalBg").classList.remove("open");
        }

        // Thêm sản phẩm được chọn từ bảng kho vào giỏ hàng.
        function addStockPickerItem(index) {
            const product = productsData[index];
            if (!product) return;
            if (getAvailableStock(product.sku) <= 0) {
                toast("Sản phẩm đã hết hàng");
                return;
            }
            addToCartBySku(product.sku);
            renderStockPicker();
        }

        // Nhập SKU rồi Enter để thêm nhanh sản phẩm vào giỏ.
        function addSkuSearchToCart() {
            const input = document.getElementById("skuSearch");
            const value = input.value.trim();
            if (!value) {
                openStockPicker();
                return;
            }
            const product = productsData.find(p => String(p.sku || "").toLowerCase() === value.toLowerCase());
            if (!product) {
                toast("Không tìm thấy sản phẩm trong kho");
                return;
            }
            addToCartBySku(product.sku);
            input.value = "";
        }

        // Đóng modal hóa đơn.
        function closeReceiptModal() {
            document.getElementById("receiptModalBg").classList.remove("open");
        }

        // Đánh dấu đang in loại phiếu nào để CSS chỉ in đúng vùng đó.
        function setPrintMode(mode) {
            document.body.classList.remove("printing-receipt", "printing-shift", "printing-return");
            document.body.classList.add(mode);
        }

        // Xóa chế độ in sau khi in xong.
        function clearPrintMode() {
            document.body.classList.remove("printing-receipt", "printing-shift", "printing-return");
        }

        // In riêng hóa đơn thanh toán.
        function printReceipt() {
            if (!lastReceipt) return;
            setPrintMode("printing-receipt");
            window.print();
        }

        // Tạo link VietQR đúng số tiền và nội dung chuyển khoản của hóa đơn.
        function buildVietQrUrl(receipt) {
            const bankBin = "970423";
            const accountNo = "00006027155";
            const accountName = "VUONG ANH VU";
            const amount = Math.round(receipt.total);
            const addInfo = receipt.id.replace("-", "");
            return `https://img.vietqr.io/image/${bankBin}-${accountNo}-compact2.png?amount=${amount}&addInfo=${encodeURIComponent(addInfo)}&accountName=${encodeURIComponent(accountName)}`;
        }

        // Hiện mã QR để khách quét thanh toán.
        function openQrPaymentModal(receipt) {
            pendingQrReceipt = receipt;
            const qrImage = document.getElementById("qrPaymentImage");
            const qrFallback = document.getElementById("qrFallback");
            qrImage.style.display = "block";
            qrFallback.style.display = "none";
            qrImage.src = buildVietQrUrl(receipt);
            document.getElementById("qrInvoiceId").textContent = receipt.id;
            document.getElementById("qrAmount").textContent = money(receipt.total);
            document.getElementById("qrContent").textContent = receipt.id.replace("-", "");
            document.getElementById("qrPaymentModalBg").classList.add("open");
            if (window.lucide) lucide.createIcons();
        }

        // Hủy bước thanh toán QR nếu khách chưa chuyển khoản.
        function cancelQrPayment() {
            pendingQrReceipt = null;
            document.getElementById("qrPaymentModalBg").classList.remove("open");
            toast("Đã hủy thanh toán QR");
        }

        // Nhân viên xác nhận đã nhận tiền QR, sau đó mới hoàn tất hóa đơn.
        function confirmQrPayment() {
            if (!pendingQrReceipt) return;
            document.getElementById("qrPaymentModalBg").classList.remove("open");
            completeCheckout(pendingQrReceipt);
            pendingQrReceipt = null;
            toast("Đã xác nhận thanh toán QR");
        }

        function formatCardNumberInput(input) {
            const digits = input.value.replace(/\D/g, "").slice(0, 19);
            input.value = digits.replace(/(.{4})/g, "$1 ").trim();
            document.getElementById("cardPaymentError").textContent = "";
        }

        function isValidCardNumber(number) {
            return /^\d{13,19}$/.test(number);
        }

        function initializeCardExpiryOptions() {
            const yearSelect = document.getElementById("cardExpiryYear");
            if (!yearSelect || yearSelect.options.length > 1) return;
            const currentYear = new Date().getFullYear();
            for (let year = currentYear; year <= currentYear + 15; year += 1) {
                yearSelect.add(new Option(String(year), String(year)));
            }
        }

        function isValidCardExpiry(month, year) {
            month = Number(month);
            year = Number(year);
            if (month < 1 || month > 12 || !year) return false;
            const expiry = new Date(year, month, 1);
            const currentMonth = new Date();
            currentMonth.setDate(1);
            currentMonth.setHours(0, 0, 0, 0);
            return expiry > currentMonth;
        }

        function detectCardBrand(number) {
            if (/^4/.test(number)) return "Visa";
            if (/^(5[1-5]|2[2-7])/.test(number)) return "Mastercard";
            if (/^35/.test(number)) return "JCB";
            if (/^3[47]/.test(number)) return "American Express";
            return "Thẻ ngân hàng";
        }

        function openCardPaymentModal(receipt) {
            pendingCardReceipt = receipt;
            document.getElementById("cardPaymentAmount").textContent = money(receipt.total);
            document.getElementById("cardNumber").value = "";
            initializeCardExpiryOptions();
            document.getElementById("cardExpiryMonth").value = "";
            document.getElementById("cardExpiryYear").value = "";
            document.getElementById("cardCvv").value = "";
            document.getElementById("cardPaymentError").textContent = "";
            document.getElementById("cardPaymentModalBg").classList.add("open");
            document.getElementById("cardNumber").focus();
            if (window.lucide) lucide.createIcons();
        }

        function cancelCardPayment() {
            pendingCardReceipt = null;
            document.getElementById("cardNumber").value = "";
            document.getElementById("cardExpiryMonth").value = "";
            document.getElementById("cardExpiryYear").value = "";
            document.getElementById("cardCvv").value = "";
            document.getElementById("cardPaymentModalBg").classList.remove("open");
            toast("Đã hủy thanh toán thẻ");
        }

        async function confirmCardPayment() {
            if (!pendingCardReceipt) return;
            const cardNumber = document.getElementById("cardNumber").value.replace(/\D/g, "");
            const expiryMonth = document.getElementById("cardExpiryMonth").value;
            const expiryYear = document.getElementById("cardExpiryYear").value;
            const cvv = document.getElementById("cardCvv").value.trim();
            const error = document.getElementById("cardPaymentError");

            if (!isValidCardNumber(cardNumber)) {
                error.textContent = "Số thẻ không hợp lệ.";
                document.getElementById("cardNumber").focus();
                return;
            }
            if (!isValidCardExpiry(expiryMonth, expiryYear)) {
                error.textContent = "Ngày hết hạn không hợp lệ hoặc thẻ đã hết hạn.";
                document.getElementById("cardExpiryMonth").focus();
                return;
            }
            if (!/^\d{3,4}$/.test(cvv)) {
                error.textContent = "CVV phải gồm 3 hoặc 4 chữ số.";
                document.getElementById("cardCvv").focus();
                return;
            }

            const receipt = pendingCardReceipt;
            receipt.cardBrand = detectCardBrand(cardNumber);
            receipt.cardLast4 = cardNumber.slice(-4);
            receipt.cardTransactionId = `CARD-${Date.now().toString(36).toUpperCase()}`;
            pendingCardReceipt = null;

            document.getElementById("cardNumber").value = "";
            document.getElementById("cardExpiryMonth").value = "";
            document.getElementById("cardExpiryYear").value = "";
            document.getElementById("cardCvv").value = "";
            document.getElementById("cardPaymentModalBg").classList.remove("open");
            await completeCheckout(receipt);
        }

        // Hoàn tất thanh toán: tạo COD hoặc ghi nhận doanh thu, trừ kho, tích điểm, mở hóa đơn.
        async function completeCheckout(receipt) {
            const receiverName = document.getElementById("codReceiverName")?.value.trim() || "";
            const receiverPhone = document.getElementById("codReceiverPhone")?.value.trim() || "";
            const receiverAddress = getDeliveryAddress();
            const deliveryNote = document.getElementById("deliveryNote")?.value.trim() || "";
            receipt.deliveryNote = deliveryNote;
            try {
                const response = await fetch(`${API_BASE_URL}/api/hoa-don`, {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({
                        ...receipt,
                        receiverName,
                        receiverPhone,
                        receiverAddress,
                        employeeId: receipt.employeeId,
                        deliveryMode: receipt.deliveryMode,
                        shippingFee: receipt.shippingFee,
                        source: "POS",
                        deliveryNote
                    })
                });
                if (!response.ok) throw new Error(await response.text());
            } catch (error) {
                console.error(error);
                toast("Không lưu được hóa đơn vào cơ sở dữ liệu");
                return;
            }
            if (receipt.isCod) {
                codOrders.push({
                    id: receipt.id,
                    name: receiverName || receipt.customer.name || "Khách giao COD",
                    phone: receiverPhone || receipt.customer.phone || "",
                    address: receiverAddress || "Chi nhánh Tân Phú",
                    note: deliveryNote,
                    amountToCollect: receipt.total,
                    shippingFee: receipt.shippingFee || 0,
                    status: "Vừa xác nhận đơn",
                    source: "POS",
                    receipt
                });
                toast("Đã tạo đơn giao hàng COD thành công!");
            } else {
                deductReceiptStock(receipt);
                applyCustomerLoyalty(receipt);
                recordPaidReceipt(receipt, receipt.deliveryMode === "DELIVERY" ? "Giao hàng" : "POS");
                if (receipt.deliveryMode === "DELIVERY") {
                    deliveryOrders.push({
                        id: receipt.id,
                        name: receiverName || receipt.customer.name || "Khách giao hàng",
                        phone: receiverPhone || receipt.customer.phone || "",
                        address: receiverAddress,
                        note: deliveryNote,
                        shippingFee: receipt.shippingFee || 0,
                        status: "Vừa xác nhận đơn",
                        source: "POS",
                        receipt
                    });
                    toast("Đã thanh toán và chuyển đơn sang Giao hàng");
                } else {
                    toast("Thanh toán đơn hàng thành công!");
                }
            }
            cart = [];
            renderCart();
            renderProducts();
            renderPartners();
            renderDelivery();
            renderCod();
            updateInventoryKpi();
            document.getElementById("cashInput").value = 0;
            document.getElementById("promoCodeInput").value = "";
            document.getElementById("codReceiverName").value = "";
            document.getElementById("codReceiverPhone").value = "";
            if (document.getElementById("deliveryAddressDetail")) document.getElementById("deliveryAddressDetail").value = "";
            if (document.getElementById("deliveryCity")) document.getElementById("deliveryCity").value = "";
            updateDeliveryWards();
            if (document.getElementById("deliveryNote")) document.getElementById("deliveryNote").value = "";
            appliedPromotion = null;
            document.querySelector('input[name="paymentMethod"][value="Tiền mặt"]').checked = true;
            document.getElementById("cashInput").disabled = false;
            document.getElementById("deliveryToggle").checked = false;
            const deliveryOption = document.querySelector('input[name="deliveryMode"][value="DELIVERY"]');
            if (deliveryOption) deliveryOption.checked = true;
            if (document.getElementById("shippingFee")) document.getElementById("shippingFee").value = 0;
            toggleShip();
            openReceiptModal(receipt);
            resetSaleCustomer();
        }

        // Nút thanh toán chính của POS, kiểm tra giỏ và điều hướng theo phương thức thanh toán.
        function checkout() {
            if (!cart.length) {
                toast("Giỏ hàng đang trống!");
                return;
            }
            if (!getCurrentEmployee()) {
                toast("Tài khoản đăng nhập không còn trong danh sách nhân viên. Không thể lập hóa đơn.");
                return;
            }
            const soldOut = cart.find(item => getAvailableStock(item.sku) <= 0);
            if (soldOut) {
                toast(soldOut.name + " đã hết hàng, không thể thanh toán!");
                return;
            }
            const overStock = cart.find(item => item.qty > getAvailableStock(item.sku));
            if (overStock) {
                toast("Số lượng " + overStock.name + " vượt quá tồn kho!");
                return;
            }
            const deliveryMode = getDeliveryMode();
            const isCod = deliveryMode === "COD";
            const paymentMethod = isCod ? "COD / Thu hộ" : getSelectedPaymentMethod();
            if (deliveryMode) {
                const receiverName = document.getElementById("codReceiverName")?.value.trim() || "";
                const receiverPhone = document.getElementById("codReceiverPhone")?.value.trim() || "";
                const city = document.getElementById("deliveryCity")?.value || "";
                const ward = document.getElementById("deliveryWard")?.value || "";
                const addressDetail = document.getElementById("deliveryAddressDetail")?.value.trim() || "";
                if (!receiverName || !receiverPhone || !city || !ward || !addressDetail) {
                    toast("Vui lòng nhập người nhận và chọn đầy đủ thành phố, phường, số nhà / tên đường");
                    return;
                }
                if (!/^0\d{9,10}$/.test(receiverPhone)) {
                    toast("Số điện thoại người nhận không hợp lệ");
                    return;
                }
            }
            const { total: payable } = calculateCartSummary();
            const cash = Number(document.getElementById("cashInput").value) || 0;
            if (!isCod && paymentMethod === "Tiền mặt" && cash < payable) {
                toast("Tiền khách đưa chưa đủ!");
                return;
            }
            const receipt = buildReceiptSnapshot(paymentMethod, deliveryMode);
            if (!isCod && paymentMethod === "Quét mã QR") {
                openQrPaymentModal(receipt);
                return;
            }
            if (!isCod && paymentMethod === "Thẻ") {
                openCardPaymentModal(receipt);
                return;
            }
            completeCheckout(receipt);
        }

        // 5. ĐỔI TRẢ SẢN PHẨM
        function scheduleReturnSearch() {
            clearTimeout(returnSearchTimer);
            returnSearchTimer = setTimeout(searchReturnInvoices, 280);
        }

        async function getApiErrorMessage(response, fallback) {
            const problem = await response.json().catch(() => null);
            return problem?.message || problem?.detail || fallback;
        }

        async function searchReturnInvoices() {
            const body = document.getElementById("returnSearchBody");
            const keyword = document.getElementById("returnSearchInput")?.value.trim() || "";
            if (!body) return;
            if (!keyword) {
                body.innerHTML = `<tr><td colspan="6" class="table-message">Nhập thông tin để tra cứu hóa đơn.</td></tr>`;
                clearReturnInvoice();
                return;
            }

            const requestId = ++returnSearchRequestId;
            body.innerHTML = `<tr><td colspan="6" class="table-message">Đang tìm hóa đơn...</td></tr>`;
            try {
                const response = await fetch(`${API_BASE_URL}/api/doi-tra/tra-cuu?q=${encodeURIComponent(keyword)}`, { cache: "no-store" });
                if (!response.ok) throw new Error(await getApiErrorMessage(response, "Không tra cứu được hóa đơn."));
                const invoices = await response.json();
                if (requestId !== returnSearchRequestId) return;
                body.innerHTML = invoices.length ? invoices.map(invoice => `
                    <tr>
                        <td><strong>${escapeHtml(invoice.id)}</strong></td>
                        <td>${escapeHtml(invoice.createdAt)}</td>
                        <td>${escapeHtml(invoice.customerName)}${invoice.phone ? `<small class="cell-note">${escapeHtml(invoice.phone)}</small>` : ""}</td>
                        <td>${escapeHtml(invoice.source)}</td>
                        <td><span class="badge ${invoice.isEligible ? "green" : "red"}">${invoice.isEligible ? "Còn hạn" : "Không đủ điều kiện"}</span><small class="cell-note">${escapeHtml(invoice.eligibilityMessage)}</small></td>
                        <td><button class="btn btn-secondary btn-compact" type="button" onclick="selectReturnInvoice('${escapeHtml(invoice.id)}')">Chọn</button></td>
                    </tr>`).join("") : `<tr><td colspan="6" class="table-message">Không tìm thấy hóa đơn phù hợp.</td></tr>`;
                if (window.lucide) lucide.createIcons();
            } catch (error) {
                if (requestId !== returnSearchRequestId) return;
                console.error(error);
                body.innerHTML = `<tr><td colspan="6" class="table-message error-text">${escapeHtml(error.message)}</td></tr>`;
            }
        }

        async function selectReturnInvoice(invoiceId) {
            try {
                const response = await fetch(`${API_BASE_URL}/api/doi-tra/hoa-don/${encodeURIComponent(invoiceId)}`, { cache: "no-store" });
                if (!response.ok) throw new Error(await getApiErrorMessage(response, "Không mở được hóa đơn."));
                selectedReturnInvoice = await response.json();
                selectedReturnSku = "";
                renderSelectedReturnInvoice();
            } catch (error) {
                console.error(error);
                toast(error.message);
            }
        }

        function renderSelectedReturnInvoice() {
            const invoice = selectedReturnInvoice;
            const panel = document.getElementById("returnInvoicePanel");
            if (!invoice || !panel) return;
            panel.classList.remove("hidden");
            document.getElementById("returnInvoiceId").textContent = invoice.id;
            document.getElementById("returnInvoiceMeta").innerHTML = `
                <div><span>Khách hàng</span><strong>${escapeHtml(invoice.customerName)}</strong></div>
                <div><span>Số điện thoại</span><strong>${escapeHtml(invoice.phone || "Không cung cấp")}</strong></div>
                <div><span>Ngày mua</span><strong>${escapeHtml(invoice.createdAt)}</strong></div>
                <div><span>Nguồn đơn</span><strong>${escapeHtml(invoice.source)}</strong></div>
                <div><span>Mốc tính 15 ngày</span><strong>${escapeHtml(invoice.eligibilityDate)}</strong></div>
                <div><span>Hạn cuối</span><strong>${escapeHtml(invoice.deadline)}</strong></div>`;
            const eligibility = document.getElementById("returnEligibility");
            eligibility.className = `return-eligibility ${invoice.isEligible ? "valid" : "invalid"}`;
            eligibility.innerHTML = `<i data-lucide="${invoice.isEligible ? "badge-check" : "circle-x"}"></i><span>${escapeHtml(invoice.eligibilityMessage)}</span>`;

            const body = document.getElementById("returnInvoiceItems");
            body.innerHTML = invoice.items.length ? invoice.items.map(item => `
                <tr class="${item.remainingQty <= 0 ? "disabled-row" : ""}">
                    <td><input type="radio" name="returnItem" value="${escapeHtml(item.sku)}" ${item.remainingQty <= 0 || !invoice.isEligible ? "disabled" : ""} onchange="selectReturnItem('${escapeHtml(item.sku)}')"></td>
                    <td><strong>${escapeHtml(item.sku)}</strong></td>
                    <td>${escapeHtml(item.name)}</td>
                    <td>${escapeHtml(item.color)}</td>
                    <td>${escapeHtml(item.size)}</td>
                    <td>${item.purchasedQty}</td>
                    <td><strong>${item.remainingQty}</strong>${item.returnedQty ? `<small class="cell-note">Đã đổi: ${item.returnedQty}</small>` : ""}</td>
                    <td class="money-cell">${money(item.price)}</td>
                </tr>`).join("") : `<tr><td colspan="8" class="table-message">Hóa đơn không có chi tiết sản phẩm.</td></tr>`;
            buildReturnReplacementOptions();
            resetReturnControls();
            if (window.lucide) lucide.createIcons();
            panel.scrollIntoView({ behavior: "smooth", block: "start" });
        }

        function clearReturnInvoice() {
            selectedReturnInvoice = null;
            selectedReturnSku = "";
            document.getElementById("returnInvoicePanel")?.classList.add("hidden");
        }

        function selectReturnItem(sku) {
            selectedReturnSku = sku;
            const item = selectedReturnInvoice?.items.find(line => line.sku === sku);
            const qty = document.getElementById("returnQty");
            if (qty && item) {
                qty.value = "1";
                qty.max = String(item.remainingQty);
            }
            updateReturnCalculation();
        }

        function buildReturnReplacementOptions() {
            const select = document.getElementById("returnReplacementSku");
            if (!select) return;
            const options = productsData.filter(product => Number(product.stock) > 0);
            select.innerHTML = `<option value="">Chọn sản phẩm đổi mới</option>` + options.map(product =>
                `<option value="${escapeHtml(product.sku)}">${escapeHtml(product.sku)} - ${escapeHtml(product.name)} | ${escapeHtml(product.color || "Mặc định")} | ${escapeHtml(product.size || "-")} | Tồn ${product.stock} | ${money(product.price)}</option>`
            ).join("");
        }

        function resetReturnControls() {
            selectedReturnSku = "";
            const qty = document.getElementById("returnQty");
            const reason = document.getElementById("returnReason");
            const resolution = document.getElementById("returnResolution");
            const replacement = document.getElementById("returnReplacementSku");
            if (qty) { qty.value = "1"; qty.max = "1"; }
            if (reason) reason.value = "";
            if (resolution) resolution.value = "";
            if (replacement) replacement.value = "";
            updateReturnCalculation();
        }

        function updateReturnCalculation() {
            const item = selectedReturnInvoice?.items.find(line => line.sku === selectedReturnSku) || null;
            const qtyInput = document.getElementById("returnQty");
            const reason = document.getElementById("returnReason")?.value || "";
            const resolution = document.getElementById("returnResolution")?.value || "";
            const replacementField = document.getElementById("returnReplacementField");
            const replacementSku = document.getElementById("returnReplacementSku")?.value || "";
            const replacement = productsData.find(product => product.sku === replacementSku) || null;
            replacementField?.classList.toggle("hidden", resolution !== "DOI_SAN_PHAM");

            let qty = Math.max(1, Number.parseInt(qtyInput?.value || "1", 10) || 1);
            if (item && qty > item.remainingQty) qty = item.remainingQty;
            if (qtyInput) qtyInput.value = String(Math.max(1, qty));
            const returnValue = item ? item.price * qty : 0;
            const replacementValue = resolution === "DOI_SAN_PHAM" && replacement ? replacement.price * qty : 0;
            const difference = replacementValue - returnValue;

            document.getElementById("returnOldValue").textContent = money(returnValue);
            document.getElementById("returnNewValue").textContent = money(replacementValue);
            const label = document.getElementById("returnBalanceLabel");
            const balance = document.getElementById("returnBalance");
            if (difference > 0) {
                label.textContent = "Khách trả thêm";
                balance.textContent = money(difference);
                balance.className = "amount-positive";
            } else {
                label.textContent = "Tiền hoàn cho khách";
                balance.textContent = money(Math.abs(difference));
                balance.className = difference < 0 ? "amount-refund" : "";
            }

            let message = "";
            if (!selectedReturnInvoice?.isEligible) message = selectedReturnInvoice?.eligibilityMessage || "Hóa đơn không đủ điều kiện.";
            else if (!item) message = "Chọn sản phẩm cần đổi trả.";
            else if (item.remainingQty <= 0) message = "Sản phẩm này không còn số lượng được đổi trả.";
            else if (qty < 1 || qty > item.remainingQty) message = "Số lượng không được vượt số lượng còn được đổi.";
            else if (!reason) message = "Chọn lỗi sản phẩm.";
            else if (!resolution) message = "Chọn đổi sản phẩm hoặc hoàn tiền.";
            else if (resolution === "DOI_SAN_PHAM" && !replacement) message = "Chọn sản phẩm đổi mới.";
            else if (resolution === "DOI_SAN_PHAM" && Number(replacement.stock) < qty) message = "Sản phẩm đổi mới không đủ tồn kho.";

            const validation = document.getElementById("returnValidationMessage");
            if (validation) validation.textContent = message || "Thông tin hợp lệ, có thể tạo phiếu.";
            const button = document.getElementById("createReturnButton");
            if (button) button.disabled = Boolean(message);
        }

        async function createReturnRequest() {
            const employee = getCurrentEmployee();
            if (!employee) {
                toast("Không xác định được nhân viên đang đăng nhập");
                return;
            }
            const payload = {
                invoiceId: selectedReturnInvoice?.id || "",
                returnSku: selectedReturnSku,
                qty: Number.parseInt(document.getElementById("returnQty")?.value || "0", 10),
                reason: document.getElementById("returnReason")?.value || "",
                resolution: document.getElementById("returnResolution")?.value || "",
                replacementSku: document.getElementById("returnReplacementSku")?.value || null,
                employeeId: employee.id
            };
            const button = document.getElementById("createReturnButton");
            if (button) button.disabled = true;
            try {
                const response = await fetch(`${API_BASE_URL}/api/doi-tra`, {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify(payload)
                });
                if (!response.ok) throw new Error(await getApiErrorMessage(response, "Không tạo được phiếu đổi trả."));
                const receipt = await response.json();
                await loadAppState();
                await loadReturnHistory();
                clearReturnInvoice();
                const search = document.getElementById("returnSearchInput");
                if (search) search.value = "";
                const searchBody = document.getElementById("returnSearchBody");
                if (searchBody) searchBody.innerHTML = `<tr><td colspan="6" class="table-message">Nhập thông tin để tra cứu hóa đơn.</td></tr>`;
                openReturnReceipt(receipt);
                toast("Đã tạo phiếu đổi trả " + receipt.id);
            } catch (error) {
                console.error(error);
                toast(error.message);
                updateReturnCalculation();
            }
        }

        async function loadReturnHistory() {
            const body = document.getElementById("returnHistoryBody");
            if (!body) return;
            body.innerHTML = `<tr><td colspan="9" class="table-message">Đang tải dữ liệu...</td></tr>`;
            try {
                const response = await fetch(`${API_BASE_URL}/api/doi-tra`, { cache: "no-store" });
                if (!response.ok) throw new Error(await getApiErrorMessage(response, "Không tải được phiếu đổi trả."));
                const items = await response.json();
                body.innerHTML = items.length ? items.map(item => {
                    const amount = item.extraAmount > 0 ? `Thu ${money(item.extraAmount)}` : `Hoàn ${money(item.refundAmount)}`;
                    return `<tr>
                        <td><strong>${escapeHtml(item.id)}</strong></td><td>${escapeHtml(item.createdAt)}</td>
                        <td>${escapeHtml(item.invoiceId)}</td><td>${escapeHtml(item.customerName)}${item.phone ? `<small class="cell-note">${escapeHtml(item.phone)}</small>` : ""}</td>
                        <td>${escapeHtml(item.resolution)}</td><td>${escapeHtml(item.returnProduct)}</td><td>${item.qty}</td>
                        <td class="money-cell">${amount}</td>
                        <td><button class="icon-button" type="button" title="Xem phiếu" onclick="openStoredReturn('${escapeHtml(item.id)}', false)"><i data-lucide="eye"></i></button><button class="icon-button" type="button" title="In lại phiếu" onclick="openStoredReturn('${escapeHtml(item.id)}', true)"><i data-lucide="printer"></i></button></td>
                    </tr>`;
                }).join("") : `<tr><td colspan="9" class="table-message">Chưa có phiếu đổi trả.</td></tr>`;
                if (window.lucide) lucide.createIcons();
            } catch (error) {
                console.error(error);
                body.innerHTML = `<tr><td colspan="9" class="table-message error-text">${escapeHtml(error.message)}</td></tr>`;
            }
        }

        async function openStoredReturn(returnId, printAfterOpen) {
            try {
                const response = await fetch(`${API_BASE_URL}/api/doi-tra/${encodeURIComponent(returnId)}`, { cache: "no-store" });
                if (!response.ok) throw new Error(await getApiErrorMessage(response, "Không mở được phiếu đổi trả."));
                openReturnReceipt(await response.json());
                if (printAfterOpen) setTimeout(printReturnReceipt, 120);
            } catch (error) {
                console.error(error);
                toast(error.message);
            }
        }

        function renderReturnReceipt(receipt) {
            const replacementRow = receipt.replacementSku ? `
                <tr><td>2</td><td>Đổi mới</td><td>${escapeHtml(receipt.replacementSku)}</td><td>${escapeHtml(receipt.replacementProduct || "")} (${escapeHtml(receipt.replacementSize || "-")}/${escapeHtml(receipt.replacementColor || "-")})</td><td>${receipt.qty}</td><td>Xuất đổi cho khách</td></tr>` : "";
            const balanceText = receipt.extraAmount > 0
                ? `Khách trả thêm: ${money(receipt.extraAmount)}`
                : `Hoàn cho khách: ${money(receipt.refundAmount)}`;
            return `
                <div class="return-slip-header">
                    <div class="return-slip-brand"><div class="receipt-logo">yo<span>dy</span></div></div>
                    <div><strong>CÔNG TY CỔ PHẦN THỜI TRANG YODY</strong><span>366 Tân Sơn Nhì, P. Tân Sơn Nhì, Q. Tân Phú, TPHCM</span><span>Bộ phận: Chăm sóc khách hàng & Thu ngân</span></div>
                    <div><strong>Mẫu số: BM-03</strong><span>Số phiếu: ${escapeHtml(receipt.id)}</span><span>Ngày: ${escapeHtml(receipt.createdAt)}</span></div>
                </div>
                <div class="return-slip-title">PHIẾU ĐỔI TRẢ SẢN PHẨM</div>
                <div class="return-slip-info">
                    <div>Họ tên khách hàng: <strong>${escapeHtml(receipt.customerName)}</strong></div><div>SĐT: <strong>${escapeHtml(receipt.phone || "Không cung cấp")}</strong></div>
                    <div>Thuộc hóa đơn số: <strong>${escapeHtml(receipt.invoiceId)}</strong></div><div>Ngày mua ban đầu: <strong>${escapeHtml(receipt.originalPurchaseDate)}</strong></div>
                    <div class="full">Lý do đổi/trả: <strong>${escapeHtml(receipt.reason)}</strong></div>
                    <div class="full">Hình thức xử lý: <strong>${escapeHtml(receipt.resolution)}</strong></div>
                </div>
                <table class="return-slip-table"><thead><tr><th>STT</th><th>Loại</th><th>Mã SKU</th><th>Tên sản phẩm (Size/Màu)</th><th>SL</th><th>Tình trạng</th></tr></thead><tbody>
                    <tr><td>1</td><td>Trả lại</td><td>${escapeHtml(receipt.returnSku)}</td><td>${escapeHtml(receipt.returnProduct)} (${escapeHtml(receipt.returnSize)}/${escapeHtml(receipt.returnColor)})</td><td>${receipt.qty}</td><td>${escapeHtml(receipt.reason)}</td></tr>
                    ${replacementRow}
                </tbody></table>
                <div class="return-slip-totals">
                    <div><span>Giá trị hàng nhận lại:</span><strong>${money(receipt.returnValue)}</strong></div>
                    <div><span>Giá trị hàng đổi mới:</span><strong>${money(receipt.replacementValue)}</strong></div>
                    <div><span>Chênh lệch thu/hoàn:</span><strong>${balanceText}</strong></div>
                </div>
                <div class="return-slip-signatures"><div><strong>Khách hàng</strong><span>(Ký, ghi rõ họ tên)</span></div><div><strong>Nhân viên tiếp nhận</strong><span>${escapeHtml(receipt.employeeName)}</span></div></div>`;
        }

        function openReturnReceipt(receipt) {
            lastReturnReceipt = receipt;
            document.getElementById("returnReceiptContent").innerHTML = renderReturnReceipt(receipt);
            document.getElementById("returnReceiptModalBg").classList.add("open");
        }

        function closeReturnReceipt() {
            document.getElementById("returnReceiptModalBg")?.classList.remove("open");
        }

        function printReturnReceipt() {
            if (!lastReturnReceipt) return;
            setPrintMode("printing-return");
            window.print();
        }

        // 6. ĐIỀU HƯỚNG & ĐĂNG XUẤT
        // Nhóm cuối này đổi màn hình trong app và xử lý đăng xuất.
        const viewPages = {
            report: "bao-cao.html",
            pos: "ban-hang.html",
            delivery: "giao-hang.html",
            cod: "giao-hang.html#cod",
            partners: "doi-tac.html",
            products: "san-pham.html",
            "product-types": "loai-san-pham.html",
            inventory: "ton-kho.html",
            promotions: "khuyen-mai.html",
            returns: "doi-tra.html"
        };

        // Kiểm tra quyền trước khi mở một form.
        function canOpenView(view) {
            if (!Array.isArray(window.allowedViews) || window.allowedViews.length === 0) return true;
            return window.allowedViews.includes(view);
        }

        function showView(view) {
            if (!view) return;
            if (!canOpenView(view)) {
                toast(window.roleDeniedMessage || "Bạn không có quyền mở chức năng này");
                const fallback = Array.isArray(window.allowedViews) && window.allowedViews.length ? window.allowedViews[0] : "pos";
                if (view !== fallback) showView(fallback);
                return;
            }

            const target = document.getElementById("view-" + view);
            if (!target && viewPages[view]) {
                saveAppState();
                window.location.href = viewPages[view];
                return;
            }

            document.querySelectorAll(".view").forEach(el => el.classList.remove("active"));
            if (target) target.classList.add("active");

            document.querySelectorAll(".nav-btn[data-view]").forEach(btn => {
                btn.classList.toggle("active", btn.getAttribute("data-view") === view);
            });

            if (view === "products") renderProducts();
            if (view === "product-types") renderProductTypes();
            if (view === "partners") renderPartners();
            if (view === "delivery") {
                renderDelivery();
                renderCod();
            }
            if (view === "cod") {
                setShipmentTab("cod");
                return;
            }
            if (view === "pos") renderCart();
            if (view === "inventory") updateInventoryKpi();
            if (view === "promotions") renderPromotions();
            if (view === "report") renderReport();
        }

        // Gửi tín hiệu đăng xuất về WinForms để quay lại form đăng nhập.
        function dangXuat() {
            if (confirm("Bạn có chắc chắn muốn đăng xuất tài khoản?")) {
                if (window.chrome && window.chrome.webview) {
                    window.chrome.webview.postMessage("logout");
                }
            }
        }

        let toastTimer;
        // Hiện thông báo nhỏ góc màn hình thay cho alert ở các thao tác nhẹ.
        function toast(msg) {
            document.getElementById("toastText").textContent = msg;
            const t = document.getElementById("toast");
            t.classList.add("show");
            clearTimeout(toastTimer);
            toastTimer = setTimeout(() => t.classList.remove("show"), 2600);
        }

        document.addEventListener("DOMContentLoaded", async () => {
            await loadAppState();
            if (window.lucide) lucide.createIcons();
            document.querySelectorAll(".nav-btn[data-view]").forEach(btn => {
                btn.addEventListener("click", () => showView(btn.getAttribute("data-view")));
            });
            document.getElementById("skuSearch")?.addEventListener("keydown", event => {
                if (event.key === "Enter") {
                    event.preventDefault();
                    addSkuSearchToCart();
                }
            });
            document.getElementById("promoCodeInput")?.addEventListener("keydown", event => {
                if (event.key === "Enter") {
                    event.preventDefault();
                    applyPromotionCode();
                }
            });
            initializeDeliveryAddressSelectors();
            window.addEventListener("afterprint", clearPrintMode);
            window.addEventListener("beforeunload", saveAppState);
            const reportDate = document.getElementById("reportDate");
            if (reportDate) {
                const today = getDateKey();
                const minDate = new Date();
                minDate.setDate(minDate.getDate() - 29);
                reportDate.value = today;
                reportDate.max = today;
                reportDate.min = getDateKey(minDate);
            }
            document.addEventListener("keydown", event => {
                if (event.key === "F2") {
                    event.preventDefault();
                    document.getElementById("skuSearch")?.focus();
                }
            });
            showView(document.body.dataset.view || "pos");
            if (document.body.dataset.view === "delivery") {
                setShipmentTab(location.hash === "#cod" ? "cod" : "delivery");
            }
            if (document.getElementById("customerStep")) resetSaleCustomer();
            if (document.getElementById("returnHistoryBody")) loadReturnHistory();
        });




