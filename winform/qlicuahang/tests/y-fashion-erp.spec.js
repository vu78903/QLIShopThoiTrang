import { test, expect } from "@playwright/test";
import { pathToFileURL } from "url";
import path from "path";

const root = process.cwd();
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

test("thêm loại sản phẩm và sản phẩm mới", async ({ page }) => {
  await openPage(page, "loai-san-pham.html");

  await page.getByRole("button", { name: /Thêm loại/i }).click({ force: true });
  await page.locator("#lt_name").fill("Áo");
  await page.getByRole("button", { name: /Lưu dữ liệu/i }).click({ force: true });
  await expect(page.locator("#productTypeBody")).toContainText("Áo");

  await page.locator(".nav-btn[data-view='products']").click({ force: true });
  await page.getByRole("button", { name: /Thêm Sản phẩm/i }).click({ force: true });
  await page.locator("#f_name").fill("Áo Polo Test");
  await page.locator("#f_type").selectOption("Áo");
  await page.locator("#f_price").fill("199000");
  await page.locator("#f_stock").fill("15");
  await page.getByRole("button", { name: /Lưu dữ liệu/i }).click({ force: true });

  await expect(page.locator("#view-products tbody")).toContainText("Áo Polo Test");
  await expect(page.locator("#view-products tbody")).toContainText("199.000");
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

  await expect(page.locator("#promotionBody")).toContainText("TEST10");
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
