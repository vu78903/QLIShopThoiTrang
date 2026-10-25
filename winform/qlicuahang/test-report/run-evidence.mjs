import { chromium } from "playwright";
import { mkdir, writeFile } from "node:fs/promises";
import path from "node:path";
import { pathToFileURL } from "node:url";

const appRoot = path.resolve(process.cwd());
const viewsRoot = path.join(appRoot, "Views");
const evidenceDir = path.join(appRoot, "test-report", "evidence");
await mkdir(evidenceDir, { recursive: true });

const state = {
  productsData: [
    { productId: "SP-001", sku: "AT-001-DEN-M", name: "Áo thun basic", type: "Áo", color: "Đen", size: "M", price: 199000, stock: 12 },
    { productId: "SP-001", sku: "AT-001-DEN-L", name: "Áo thun basic", type: "Áo", color: "Đen", size: "L", price: 199000, stock: 3 },
    { productId: "SP-002", sku: "QJ-002-XANH-32", name: "Quần jean ống suông", type: "Quần", color: "Xanh", size: "32", price: 499000, stock: 0 }
  ],
  productTypesData: [
    { id: "LSP-001", name: "Áo", description: "Áo thời trang", active: true },
    { id: "LSP-002", name: "Quần", description: "Quần thời trang", active: true }
  ],
  partnersData: [
    { category: "Khách hàng", id: "KH-001", name: "Nguyễn Khách Test", phone: "0900000000", email: "khach@example.com", address: "Tân Phú", role: "", points: 120, totalSpent: 1200000 },
    { category: "Nhà cung cấp", id: "NCC-001", name: "Nhà cung cấp Test", phone: "0911000000", email: "ncc@example.com", address: "TP HCM", role: "0101234567" },
    { category: "Nhân viên", id: "NV-001", name: "Nguyễn Quản Lý", phone: "0922000000", email: "ql@example.com", address: "TP HCM", role: "Quản lý" },
    { category: "Nhân viên", id: "NV-004", name: "Phạm Thu Ngân", phone: "0933000000", email: "tn@example.com", address: "TP HCM", role: "Thu ngân" }
  ],
  promotionsData: [
    { code: "KM10", percent: 10, startDate: "2026-10-01", endDate: "2026-12-31", minOrder: 100000, status: "Đang chạy" },
    { code: "FUTURE", percent: 20, startDate: "2026-12-01", endDate: "2026-12-31", minOrder: 0, status: "Sắp diễn ra" }
  ],
  deliveryOrders: [
    { id: "HD-GH-001", name: "Nguyễn Khách Test", phone: "0900000000", address: "366 Tân Sơn Nhì", note: "Gọi trước khi giao", shippingFee: 25000, status: "Vừa xác nhận đơn", source: "POS", total: 239920 }
  ],
  codOrders: [
    { id: "HD-COD-001", name: "Khách COD", phone: "0901111222", address: "Quận Tân Phú", note: "", amountToCollect: 539920, shippingFee: 25000, status: "Hàng đang được vận chuyển", source: "POS" }
  ],
  importRequests: [
    { id: "PN-001", supplierId: "NCC-001", supplierName: "Nhà cung cấp Test", sku: "AT-001-DEN-M", productName: "Áo thun basic", qty: 10, unitPrice: 199000, total: 1990000, status: "Chờ duyệt", employeeId: "NV-001", createdAt: "23/10/2026 10:00" }
  ],
  cart: [],
  salesHistory: [
    { id: "HD-TEST-001", dateKey: "2026-10-23", createdAt: "23/10/2026 10:30", source: "POS", paymentMethod: "Tiền mặt", total: 214920, subtotal: 199000, discount: 0, vat: 15920, shippingFee: 0, customer: { name: "Khách lẻ", phone: "" }, items: [{ sku: "AT-001-DEN-M", name: "Áo thun basic", qty: 1, price: 199000 }] }
  ],
  shiftReports: [],
  appliedPromotion: null
};

const invoiceSummary = { id: "HD-TEST-001", createdAt: "23/10/2026 10:30", cashier: "Nguyễn Quản Lý", customerName: "Khách lẻ", phone: "", paymentMethod: "Tiền mặt", itemCount: 1, total: 214920, source: "POS", isEligible: true, eligibilityMessage: "Còn trong thời hạn đổi trả" };
const invoiceDetail = { ...invoiceSummary, subtotal: 199000, discount: 0, vat: 15920, shippingFee: 0, customer: { name: "Khách lẻ", phone: "" }, items: [{ sku: "AT-001-DEN-M", name: "Áo thun basic", color: "Đen", size: "M", qty: 1, price: 199000, remainingQty: 1 }], eligibilityDate: "23/10/2026", deadline: "07/11/2026" };

const browser = await chromium.launch({ headless: true });
const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, deviceScaleFactor: 1 });
const page = await context.newPage();
const log = [];
await page.addInitScript(() => {
  window.currentEmployee = { id: "NV-001", name: "Nguyễn Quản Lý" };
  window.allowedViews = ["report", "pos", "delivery", "returns", "partners", "products", "product-types", "inventory", "promotions"];
});
page.on("console", message => { if (message.type() === "error") log.push({ type: "console", text: message.text() }); });
page.on("pageerror", error => log.push({ type: "pageerror", text: error.message }));
page.on("dialog", async dialog => { log.push({ type: "dialog", text: dialog.message() }); await dialog.dismiss(); });

await page.route("http://127.0.0.1:5000/api/**", async route => {
  const request = route.request();
  const url = request.url();
  if (url.endsWith("/api/app-state")) return route.fulfill({ json: state });
  if (url.includes("/api/hoa-don?") && request.method() === "GET") return route.fulfill({ json: [invoiceSummary] });
  if (url.endsWith("/api/hoa-don/HD-TEST-001") && request.method() === "GET") return route.fulfill({ json: invoiceDetail });
  if (url.includes("/api/doi-tra/tra-cuu")) return route.fulfill({ json: [invoiceSummary] });
  if (url.endsWith("/api/doi-tra/hoa-don/HD-TEST-001")) return route.fulfill({ json: invoiceDetail });
  if (url.endsWith("/api/doi-tra") && request.method() === "GET") return route.fulfill({ json: [] });
  return route.fulfill({ status: 200, json: {} });
});

async function open(file) {
  await page.addInitScript(({ key, value }) => localStorage.setItem(key, JSON.stringify(value)), { key: "y_fashion_erp_state_v1", value: state });
  await page.goto(pathToFileURL(path.join(viewsRoot, file)).href, { waitUntil: "domcontentloaded" });
  await page.waitForTimeout(1200);
}

async function shot(id, name, fullPage = false) {
  const file = `${id}-${name}.png`;
  await page.screenshot({ path: path.join(evidenceDir, file), fullPage });
  log.push({ type: "screenshot", id, file, url: page.url() });
}

await open("dang-nhap.html");
await shot("E01", "dang-nhap");
await page.locator("#password").fill("MatKhauThu123");
await shot("E02", "mat-khau-duoc-an");
await page.locator("#username").fill("");
await page.locator("#password").fill("");
await page.locator("#loginButton").click();
await shot("E03", "dang-nhap-bo-trong");

await open("ban-hang.html");
await shot("E04", "pos-ban-hang");
await page.getByRole("button", { name: "Chọn từ kho" }).click();
await shot("E05", "chon-san-pham-tu-kho");
await page.getByRole("button", { name: "Đóng", exact: true }).click();
await page.getByRole("button", { name: /Xem lại hóa đơn/i }).click();
await page.waitForTimeout(500);
await shot("E06", "lich-su-hoa-don");

await open("ban-hang.html");
await page.locator("#skuSearch").fill("AT-001-DEN-M");
await page.locator("#skuSearch").press("Enter");
await page.getByRole("button", { name: "Đủ tiền", exact: true }).click();
await page.getByRole("button", { name: /THANH TOÁN/i }).click();
await page.waitForTimeout(300);
await shot("E23", "thanh-toan-mo-hoa-don");

await open("san-pham.html");
await page.locator("#productSearchInput").fill("sp-001");
await page.waitForTimeout(450);
await shot("E07", "tim-kiem-san-pham");
await page.getByRole("button", { name: /Thêm Sản phẩm/i }).click();
await shot("E08", "them-san-pham-va-sku");

await open("loai-san-pham.html");
await shot("E09", "loai-san-pham");

await open("doi-tac.html");
await shot("E10", "doi-tac-khach-hang");
const supplierTab = page.getByRole("button", { name: /Nhà cung cấp/i });
if (await supplierTab.count()) await supplierTab.first().click();
await shot("E11", "doi-tac-nha-cung-cap");

await open("ton-kho.html");
await shot("E12", "ton-kho-va-phieu-nhap");
const importButton = page.getByRole("button", { name: /Tạo phiếu nhập/i });
if (await importButton.count()) { await importButton.first().click(); await shot("E13", "tao-phieu-nhap"); }

await open("khuyen-mai.html");
await shot("E14", "khuyen-mai");
await page.getByRole("button", { name: /Thêm voucher/i }).click();
await shot("E15", "them-voucher");

await open("giao-hang.html");
await shot("E16", "giao-hang");
await page.locator("#shipmentTabCod").click();
await shot("E17", "doi-soat-cod");

await open("doi-tra.html");
await shot("E18", "doi-tra");
await page.locator("#returnSearchInput").fill("HD-TEST-001");
await page.waitForTimeout(700);
const chooseReturn = page.locator("#returnSearchBody button");
if (await chooseReturn.count()) { await chooseReturn.first().click(); await page.waitForTimeout(300); }
await shot("E19", "tra-cuu-doi-tra");

await open("bao-cao.html");
await shot("E20", "bao-cao-thong-ke");
const shiftButton = page.getByRole("button", { name: /In kết ca/i });
if (await shiftButton.count()) { await shiftButton.first().click(); await shot("E21", "phieu-ket-ca"); }

await open("bao-cao.html");
await page.getByRole("button", { name: "Cài đặt" }).click();
await page.waitForTimeout(100);
await shot("E22", "cai-dat-chi-thong-bao");

const publicContext = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
const publicPage = await publicContext.newPage();
await publicPage.goto(pathToFileURL(path.join(viewsRoot, "san-pham.html")).href, { waitUntil: "domcontentloaded" });
await publicPage.waitForTimeout(1800);
await publicPage.screenshot({ path: path.join(evidenceDir, "E24-truy-cap-san-pham-khong-phien.png") });
log.push({ type: "screenshot", id: "E24", file: "E24-truy-cap-san-pham-khong-phien.png", url: publicPage.url() });
await publicPage.goto("http://127.0.0.1:5000/api/health", { waitUntil: "domcontentloaded" });
await publicPage.screenshot({ path: path.join(evidenceDir, "E25-api-health.png") });
log.push({ type: "screenshot", id: "E25", file: "E25-api-health.png", url: publicPage.url() });
await publicContext.close();

await writeFile(path.join(evidenceDir, "evidence-log.json"), JSON.stringify(log, null, 2), "utf8");
await browser.close();
console.log(JSON.stringify({ screenshots: log.filter(item => item.type === "screenshot").length, issues: log.filter(item => item.type !== "screenshot") }, null, 2));
