import { test, expect } from "@playwright/test";
import { pathToFileURL } from "url";
import path from "path";

const root = path.join(process.cwd(), "Views");
const stateKey = "y_fashion_erp_state_v1";

function fileUrl(fileName) {
  return pathToFileURL(path.join(root, fileName)).href;
}

const emptyState = {
  productsData: [],
  productTypesData: [],
  partnersData: [],
  promotionsData: [],
  deliveryOrders: [],
  codOrders: [],
  cart: [],
  salesHistory: [],
  shiftReports: [],
  appliedPromotion: null
};

const seededState = {
  ...emptyState,
  productTypesData: [{ id: "LSP-001", name: "Áo" }],
  productsData: [
    { sku: "SP-001", name: "Áo thun test", type: "Áo", price: 100000, stock: 20 }
  ],
  partnersData: [
    {
      category: "Khách hàng",
      id: "KH-001",
      name: "Nguyễn Văn Test",
      phone: "0901234567",
      email: "khach@gmail.com",
      address: "Tân Phú",
      role: "",
      points: 0,
      totalSpent: 0
    }
  ],
  promotionsData: [
    {
      code: "SALE10",
      percent: 10,
      startDate: "2026-01-01",
      endDate: "2026-12-31",
      minOrder: 0,
      status: "Đang chạy"
    }
  ]
};

async function openPage(page, fileName, state = emptyState) {
  await page.addInitScript(
    ({ key, value }) => localStorage.setItem(key, JSON.stringify(value)),
    { key: stateKey, value: state }
  );
  await page.goto(fileUrl(fileName));
}

test.beforeEach(async ({ page }) => {
  page.on("dialog", dialog => dialog.accept());
});

test("chuyển qua lại các form chính bằng menu", async ({ page }) => {
  await openPage(page, "ban-hang.html");

  const targets = [
    ["delivery", "giao-hang.html"],
    ["products", "san-pham.html"],
    ["product-types", "loai-san-pham.html"],
    ["partners", "doi-tac.html"],
    ["inventory", "ton-kho.html"],
    ["promotions", "khuyen-mai.html"],
    ["report", "bao-cao.html"],
    ["pos", "ban-hang.html"]
  ];

  for (const [view, file] of targets) {
    await page.locator(`.nav-btn[data-view="${view}"]`).click({ force: true });
    await expect(page).toHaveURL(new RegExp(`${file.replace(".", "\\.")}$`));
    await expect(page.locator(`body[data-view="${view}"]`)).toBeVisible();
  }
});

test("thêm sản phẩm tự sinh MaSP và MaSKU theo biến thể", async ({ page }) => {
  const state = structuredClone(seededState);
  state.productsData[0] = {
    productId: "SP-001", sku: "AOTHUN-001-DEN-M", name: "Áo thun test",
    type: "Áo", color: "Đen", size: "M", price: 100000, stock: 20
  };
  await page.route("http://127.0.0.1:5000/api/**", async route => {
    const request = route.request();
    if (request.url().endsWith("/api/app-state")) return route.fulfill({ json: state });
    if (request.url().endsWith("/api/san-pham") && request.method() === "POST") {
      const savedProduct = request.postDataJSON();
      state.productsData.push(savedProduct);
      return route.fulfill({ json: savedProduct });
    }
    return route.fulfill({ status: 200, json: {} });
  });
  await openPage(page, "san-pham.html", state);

  await page.getByRole("button", { name: /Thêm Sản phẩm/i }).click({ force: true });
  await page.locator("#f_name").fill("Quần jean ống suông");
  await page.locator("#f_type").selectOption("Áo");
  await expect(page.locator("#f_size option")).toHaveText(["Chọn size sản phẩm", "28", "30", "32", "34", "36"]);
  await page.locator("#f_name").fill("Áo Hoodie");
  await page.locator("#f_type").selectOption("Áo");
  await page.locator("#f_color").selectOption("Đen");
  await page.locator("#f_size").selectOption("L");
  await expect(page.locator("#f_product_id")).toHaveValue("SP-002");
  await expect(page.locator("#f_sku")).toHaveValue("HD-002-DEN-L");
});

test("tìm sản phẩm theo MaSP, MaSKU và tên không dấu", async ({ page }) => {
  const state = structuredClone(seededState);
  state.productsData = [
    { productId: "SP-001", sku: "AT-001-DEN-M", name: "Áo thun basic", type: "Áo", color: "Đen", size: "M", price: 199000, stock: 10 },
    { productId: "SP-002", sku: "QJ-002-XANH-32", name: "Quần jean ống suông", type: "Quần", color: "Xanh", size: "32", price: 499000, stock: 10 },
    { productId: "SP-003", sku: "HD-003-DO-L", name: "Áo hoodie", type: "Áo", color: "Đỏ", size: "L", price: 599000, stock: 10 }
  ];
  await page.route("http://127.0.0.1:5000/api/**", route => route.fulfill({ json: state }));
  await openPage(page, "san-pham.html", state);

  const search = page.locator("#productSearchInput");
  const rows = page.locator("#view-products tbody tr");
  await expect(rows).toHaveCount(3);

  await search.fill("sp-002");
  await page.waitForTimeout(350);
  expect(await rows.count()).toBe(1);
  await expect(rows.first()).toContainText("Quần jean ống suông");

  await search.fill("hd 003 do");
  await expect(rows).toHaveCount(1);
  await expect(rows.first()).toContainText("HD-003-DO-L");

  await search.fill("ao thun basic");
  await expect(rows).toHaveCount(1);
  await expect(rows.first()).toContainText("Áo thun basic");

  await search.fill("không tồn tại");
  await expect(rows).toHaveCount(1);
  await expect(rows.first()).toContainText("Không tìm thấy sản phẩm");
  await expect(page.locator("#productSearchSummary")).toHaveText("Tìm thấy 0/3 SKU");

  await page.locator("#productSearchClear").click();
  await expect(rows).toHaveCount(3);
});

test("đọc file CSV demo theo MaSP và tự sinh MaSKU khi import", async ({ page }) => {
  const state = structuredClone(seededState);
  let importedRows = null;
  await page.route("http://127.0.0.1:5000/api/**", async route => {
    const request = route.request();
    if (request.url().endsWith("/api/app-state")) return route.fulfill({ json: state });
    if (request.url().endsWith("/api/san-pham/import") && request.method() === "POST") {
      importedRows = request.postDataJSON();
      return route.fulfill({ json: { count: importedRows.length } });
    }
    return route.fulfill({ status: 200, json: {} });
  });
  await openPage(page, "san-pham.html", state);

  const demoFile = path.join(process.cwd(), "..", "..", "database", "Demo_Import_SanPham.csv");
  await page.locator("#productImportFile").setInputFiles(demoFile);
  await expect.poll(() => importedRows?.length || 0).toBe(12);
  expect(importedRows[0]).toMatchObject({
    productId: "SP-023",
    sku: null,
    name: "Áo thun thể thao",
    color: "Đen",
    size: "M"
  });
});

test("thêm khách hàng và voucher", async ({ page }) => {
  await openPage(page, "doi-tac.html");

  await page.getByRole("button", { name: /Thêm mới đối tác/i }).click({ force: true });
  await page.locator("#p_name").fill("Khách Test");
  await page.locator("#p_phone").fill("0901111222");
  await page.locator("#p_email").fill("khach@gmail.com");
  await page.locator("#p_address").fill("Tân Phú");
  await page.getByRole("button", { name: /Lưu dữ liệu/i }).click({ force: true });
  await expect(page.locator("#partnerBody")).toContainText("Khách Test");

  await page.locator(".nav-btn[data-view='promotions']").click({ force: true });
  await page.getByRole("button", { name: /Thêm voucher/i }).click({ force: true });
  await page.locator("#km_code").fill("TEST10");
  await page.locator("#km_percent").fill("10");
  await page.locator("#km_start").fill("2026-01-01");
  await page.locator("#km_end").fill("2026-12-31");
  await page.locator("#km_min_order").fill("0");
  await page.getByRole("button", { name: /Lưu dữ liệu/i }).click({ force: true });

  await expect(page.locator("#voucherBody")).toContainText("TEST10");
});

test("thêm sản phẩm khuyến mãi nhiều SKU có giờ phút và tải lại không lỗi", async ({ page }) => {
  const state = structuredClone(emptyState);
  state.productsData = [
    { productId: "SP-023", sku: "1-023-DEN-S", name: "Boxer nam thể thao", type: "Đồ lót nam", color: "Đen", size: "S", price: 189000, stock: 10 },
    { productId: "SP-021", sku: "A-021-DEN-S", name: "Boxer nam cotton", type: "Đồ lót nam", color: "Đen", size: "S", price: 129000, stock: 10 }
  ];
  state.promotionsData = [{
    code: "VOUCHER10", percent: 10, minOrder: 200000,
    startDate: "2026-10-01T00:00", endDate: "2026-12-31T23:59", products: []
  }];
  let savedPayload = null;
  const dialogMessages = [];
  const promotionRequests = [];
  page.on("dialog", dialog => dialogMessages.push(dialog.message()));
  page.on("request", request => {
    if (request.url().includes("/api/khuyen-mai")) promotionRequests.push(`${request.method()} ${request.url()}`);
  });
  await page.route("http://127.0.0.1:5000/api/**", async route => {
    const request = route.request();
    if (request.url().endsWith("/api/app-state")) return route.fulfill({ json: state });
    if (request.url().endsWith("/api/khuyen-mai") && request.method() === "POST") {
      savedPayload = request.postDataJSON();
      state.promotionsData.push({
        ...savedPayload,
        products: savedPayload.products.map(item => {
          const product = state.productsData.find(candidate => candidate.sku === item.sku);
          return {
            ...item,
            productId: product.productId,
            productName: product.name,
            color: product.color,
            size: product.size,
            originalPrice: product.price,
            salePrice: Math.round(product.price * (100 - item.percent) / 100)
          };
        })
      });
      return route.fulfill({ json: savedPayload });
    }
    return route.fulfill({ status: 200, json: {} });
  });
  await openPage(page, "khuyen-mai.html", state);

  await expect(page.locator("#productPromotionPanel")).toBeVisible();
  await expect(page.locator("#voucherPanel")).toBeHidden();
  await page.getByRole("tab", { name: "Voucher" }).click();
  await expect(page.locator("#voucherPanel")).toBeVisible();
  await expect(page.locator("#voucherBody")).toContainText("VOUCHER10");
  await expect(page.locator("#productPromotionPanel")).toBeHidden();
  await page.getByRole("tab", { name: "Sản phẩm khuyến mãi" }).click();
  await expect(page.locator("#productPromotionPanel")).toBeVisible();
  await expect(page.locator("#promotionAddButton")).toHaveText("Thêm sản phẩm khuyến mãi");

  expect(await page.evaluate(() => {
    const date = parseLocalDate("2026-10-27T13:39");
    return [date.getFullYear(), date.getMonth() + 1, date.getDate(), date.getHours(), date.getMinutes()];
  })).toEqual([2026, 10, 27, 13, 39]);

  await page.getByRole("button", { name: "Thêm sản phẩm khuyến mãi" }).click();
  await page.locator("#kmsp_start").fill("2026-10-27T13:39");
  await page.locator("#kmsp_end").fill("2027-01-13T13:39");
  await page.locator("#kmsp_percent").fill("25");
  await page.locator("#km_product_picker").selectOption(["1-023-DEN-S", "A-021-DEN-S"]);
  await page.getByRole("button", { name: "Thêm các mã SKU đã chọn" }).click();
  await expect(page.locator("#km_product_body tr")).toHaveCount(2);
  await page.getByRole("button", { name: "Lưu dữ liệu" }).click();

  await expect.poll(() => promotionRequests).toContain("POST http://127.0.0.1:5000/api/khuyen-mai");
  await expect.poll(() => savedPayload?.products?.length || 0).toBe(2);
  await expect(page.locator("#productPromotionBody")).toContainText(savedPayload.code);
  await expect(page.locator("#productPromotionBody")).toContainText("27/10/2026 13:39");
  expect(dialogMessages.filter(message => /invalid time value|không lưu được/i.test(message))).toEqual([]);
});

test("bán hàng tiền mặt và xuất hóa đơn", async ({ page }) => {
  await openPage(page, "ban-hang.html", seededState);

  await page.locator("#skuSearch").fill("SP-001");
  await page.evaluate(() => addSkuSearchToCart());
  await expect(page.locator("#cartBody")).toContainText("Áo thun test");

  await page.locator("#customerSearch").fill("0901234567");
  await page.locator("#promoCodeInput").fill("SALE10");
  await page.getByRole("button", { name: /Áp dụng/i }).click({ force: true });
  await page.getByRole("button", { name: /Đủ tiền/i }).click({ force: true });
  await page.getByRole("button", { name: /THANH TOÁN/i }).click({ force: true });

  await expect(page.locator("#receiptModalBg")).toHaveClass(/open/);
  await expect(page.locator("#receiptContent")).toContainText("HÓA ĐƠN THANH TOÁN");
  await expect(page.locator("#receiptContent")).toContainText("Áo thun test");
});

test("QR payOS tự động xác nhận, xuất hóa đơn và không lưu trùng", async ({ page }) => {
  test.setTimeout(60000);
  const state = structuredClone(seededState);
  state.partnersData.push({
    category: "Nhân viên", id: "NV-001", name: "Thu ngân kiểm thử",
    phone: "0900000001", email: "thungan@test.local", address: "Cửa hàng", role: "Thu ngân"
  });
  await page.addInitScript(() => {
    window.currentEmployee = { id: "NV-001", name: "Thu ngân kiểm thử" };
  });
  let createdPayment = null;
  let paymentChecks = 0;
  let directInvoicePosts = 0;

  await page.route("http://127.0.0.1:5000/api/**", async route => {
    const request = route.request();
    const url = request.url();
    if (url.endsWith("/api/app-state")) return route.fulfill({ json: state });
    if (url.endsWith("/api/thanh-toan-qr") && request.method() === "POST") {
      createdPayment = request.postDataJSON();
      return route.fulfill({ json: {
        orderCode: 1790000000000123,
        invoiceId: createdPayment.id,
        amount: createdPayment.total,
        description: "HDTEST001",
        bankBin: "970422",
        accountNumber: "0924020138",
        accountName: "VUONG ANH VU",
        paymentLinkId: "test-link",
        checkoutUrl: "https://pay.payos.vn/test",
        status: "PENDING"
      }});
    }
    if (/\/api\/thanh-toan-qr\/\d+$/.test(url) && request.method() === "GET") {
      paymentChecks += 1;
      return route.fulfill({ json: {
        orderCode: 1790000000000123,
        status: paymentChecks > 1 ? "PAID" : "PENDING",
        amount: createdPayment.total,
        amountPaid: paymentChecks > 1 ? createdPayment.total : 0,
        paid: paymentChecks > 1
      }});
    }
    if (url.endsWith("/api/hoa-don") && request.method() === "POST") {
      directInvoicePosts += 1;
      return route.fulfill({ status: 500, body: "Không được gọi lần hai" });
    }
    return route.fulfill({ status: 200, json: {} });
  });

  await openPage(page, "ban-hang.html", state);
  await page.locator("#skuSearch").fill("SP-001");
  await page.evaluate(() => addSkuSearchToCart());
  await page.locator('input[name="paymentMethod"]').nth(1).check({ force: true });
  await page.getByRole("button", { name: /THANH TOÁN/i }).click({ force: true });

  await expect(page.locator("#qrAccount")).toContainText("0924020138");
  await expect.poll(() => paymentChecks, { timeout: 10000 }).toBeGreaterThan(1);
  await expect(page.locator("#receiptModalBg")).toHaveClass(/open/);
  expect(createdPayment.paymentMethod).toBeTruthy();
  expect(directInvoicePosts).toBe(0);
  await expect(page.locator("#cartBody tr")).toHaveCount(0);
});

test("COD cập nhật tới giao thành công và được đưa vào báo cáo", async ({ page }) => {
  await openPage(page, "ban-hang.html", seededState);

  await page.locator("#skuSearch").fill("SP-001");
  await page.evaluate(() => addSkuSearchToCart());
  await page.locator("#deliveryToggle").check();
  await page.locator('input[name="deliveryMode"][value="COD"]').check();
  await page.locator("#codReceiverName").fill("Người nhận COD");
  await page.locator("#codReceiverPhone").fill("0909999888");
  await page.locator("#deliveryCity").selectOption("Thành phố Hồ Chí Minh");
  await page.locator("#deliveryWard").selectOption("Tân Sơn Nhì");
  await page.locator("#deliveryAddressDetail").fill("366 Tân Sơn Nhì");
  await page.getByRole("button", { name: /XÁC NHẬN ĐƠN COD/i }).click({ force: true });
  await expect(page.locator("#receiptModalBg")).toHaveClass(/open/);
  await page.locator("#receiptModalBg .btn-secondary").click({ force: true });

  await page.locator(".nav-btn[data-view='delivery']").click({ force: true });
  await page.locator("#shipmentTabCod").click({ force: true });
  await expect(page.locator("#codBody")).toContainText("Vừa xác nhận đơn");

  await page.getByRole("button", { name: "Xác nhận shipper lấy hàng" }).click({ force: true });
  await page.getByRole("button", { name: "Bắt đầu vận chuyển" }).click({ force: true });
  await page.getByRole("button", { name: "Xác nhận giao thành công" }).click({ force: true });
  await expect(page.locator("#codBody")).toContainText("Chưa có đơn hàng COD nào");

  await page.locator(".nav-btn[data-view='report']").click({ force: true });
  await expect(page.locator("#reportDayRevenue")).toContainText("100.000");
  await expect(page.locator("#reportDayCod")).toContainText("1 đơn");
});
