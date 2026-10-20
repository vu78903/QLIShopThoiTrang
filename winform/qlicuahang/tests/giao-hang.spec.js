import { test, expect } from "@playwright/test";
import { pathToFileURL } from "url";
import path from "path";

const root = process.cwd();

const appState = {
  productsData: [{ sku: "SP-001", name: "Áo polo", type: "Áo", color: "Xanh", size: "L", unit: "Cái", price: 200000, stock: 12 }],
  productTypesData: [{ id: "LSP-001", name: "Áo" }],
  partnersData: [
    { category: "Nhân viên", id: "NV-001", name: "Nguyễn Thu Ngân", phone: "0900000000", email: "nv@yody.vn", address: "Tân Phú", role: "Thu ngân" },
    { category: "Khách hàng", id: "KH-001", name: "Nguyễn Văn An", phone: "0901234567", email: "an@gmail.com", address: "Tân Phú", role: "" }
  ],
  promotionsData: [], importRequests: [], salesHistory: [],
  deliveryOrders: [{ id: "WEB-001", name: "Khách Web", phone: "0901234567", address: "Tân Phú", note: "Gọi trước khi giao", shippingFee: 30000, status: "Vừa xác nhận đơn", source: "WEB", receipt: { id: "WEB-001", total: 246000, items: [] } }],
  codOrders: [{ id: "WEB-COD-001", name: "Khách COD", phone: "0907654321", address: "Quận 1", note: "Giao giờ hành chính", amountToCollect: 276000, shippingFee: 30000, status: "Vừa xác nhận đơn", source: "WEB", receipt: { id: "WEB-COD-001", total: 276000, items: [] } }]
};

async function mockApi(page) {
  await page.addInitScript(() => {
    window.currentEmployee = { id: "NV-001", name: "Nguyễn Thu Ngân" };
  });
  await page.route("https://**/*", route => route.fulfill({ status: 200, body: "" }));
  await page.route("http://127.0.0.1:5000/api/**", async route => {
    const url = new URL(route.request().url());
    if (url.pathname === "/api/app-state") {
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(appState) });
      return;
    }
    if (route.request().method() === "PUT") {
      await route.fulfill({ status: 204, body: "" });
      return;
    }
    await route.fulfill({ status: 200, contentType: "application/json", body: "{}" });
  });
}

test("gộp giao hàng và COD trong cùng một form", async ({ page }) => {
  await mockApi(page);
  await page.goto(pathToFileURL(path.join(root, "giao-hang.html")).href);
  await expect(page.locator("#deliveryBody")).toContainText("WEB-001");
  await expect(page.locator("#deliveryBody")).toContainText("0 ₫");
  await expect(page.locator("#deliveryBody")).toContainText("Gọi trước khi giao");
  await expect(page.locator("#deliveryBody")).not.toContainText("WEB-COD-001");

  await page.locator("#shipmentTabCod").click();
  await expect(page).toHaveURL(/giao-hang\.html#cod$/);
  await expect(page.locator("#codBody")).toContainText("WEB-COD-001");
  await expect(page.locator("#codBody")).toContainText("276.000 ₫");
  await expect(page.locator("#codBody")).toContainText("Giao giờ hành chính");
});

test("POS chỉ hiện hai lựa chọn khi bật giao tận nơi", async ({ page }) => {
  await mockApi(page);
  await page.goto(pathToFileURL(path.join(root, "ban-hang.html")).href);
  await expect(page.locator("#shipFields")).not.toHaveClass(/open/);
  await page.locator("#deliveryToggle").check();
  await expect(page.locator("#shipFields")).toHaveClass(/open/);
  await expect(page.locator(".delivery-mode-options")).toContainText("Giao hàng");
  await expect(page.locator(".delivery-mode-options")).toContainText("Giao hàng COD");
  await page.locator('input[name="deliveryMode"][value="COD"]').check();
  await expect(page.locator("#checkoutButtonLabel")).toHaveText("XÁC NHẬN ĐƠN COD");
  await expect(page.locator("#paymentSection")).toHaveClass(/hidden/);
});

test("dùng lại khách đã nhập và chỉ yêu cầu địa chỉ cùng ghi chú", async ({ page }) => {
  await mockApi(page);
  await page.goto(pathToFileURL(path.join(root, "ban-hang.html")).href);
  await page.locator("#customerSearch").fill("0901234567");
  await page.locator("#deliveryToggle").check();
  await expect(page.locator("#deliveryCustomerSummary")).toBeVisible();
  await expect(page.locator("#deliveryCustomerSummary")).toContainText("Nguyễn Văn An");
  await expect(page.locator("#deliveryCustomerSummary")).toContainText("0901234567");
  await expect(page.locator("#deliveryManualCustomerFields")).toBeHidden();
  await expect(page.locator("#codReceiverName")).toHaveValue("Nguyễn Văn An");
  await expect(page.locator("#codReceiverPhone")).toHaveValue("0901234567");
  await expect(page.locator("#deliveryCity")).toBeVisible();
  await expect(page.locator("#deliveryWard")).toBeVisible();
  await expect(page.locator("#deliveryAddressDetail")).toBeVisible();
  await expect(page.locator("#deliveryNote")).toBeVisible();
});
