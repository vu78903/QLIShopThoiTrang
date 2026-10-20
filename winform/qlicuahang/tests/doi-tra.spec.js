import { test, expect } from "@playwright/test";
import { pathToFileURL } from "url";
import path from "path";

const invoice = {
  id: "HD-TEST-001",
  createdAt: "17/10/2026 09:30",
  customerName: "Nguyễn Văn An",
  phone: "0901234567",
  source: "Tại cửa hàng",
  eligibilityDate: "17/10/2026 09:30",
  deadline: "01/11/2026 09:30",
  isEligible: true,
  eligibilityMessage: "Hóa đơn còn trong thời hạn đổi trả.",
  items: [{
    sku: "SP-001",
    name: "Áo thun",
    color: "Trắng",
    size: "M",
    unit: "Cái",
    purchasedQty: 2,
    returnedQty: 0,
    remainingQty: 2,
    price: 199000
  }]
};

const receipt = {
  id: "PDT-20261018-0001",
  createdAt: "18/10/2026 08:00",
  invoiceId: invoice.id,
  originalPurchaseDate: invoice.createdAt,
  customerName: invoice.customerName,
  phone: invoice.phone,
  employeeName: "Lê Thu Ngân",
  source: invoice.source,
  reason: "Hàng bị hư",
  resolution: "Đổi sản phẩm",
  returnSku: "SP-001",
  returnProduct: "Áo thun",
  returnColor: "Trắng",
  returnSize: "M",
  qty: 2,
  returnUnitPrice: 199000,
  returnValue: 398000,
  replacementSku: "SP-002",
  replacementProduct: "Áo polo",
  replacementColor: "Đen",
  replacementSize: "L",
  replacementUnitPrice: 299000,
  replacementValue: 598000,
  refundAmount: 0,
  extraAmount: 200000
};

test("nhân viên đổi hàng, giới hạn số lượng và tạo lại phiếu BM-03", async ({ page }) => {
  let created = false;
  let postedPayload;

  await page.addInitScript(() => {
    window.currentEmployee = { id: "NV-001", name: "Lê Thu Ngân" };
    window.allowedViews = ["returns"];
  });

  await page.route("https://**/*", route => route.fulfill({ status: 200, body: "" }));
  await page.route("http://127.0.0.1:5000/api/**", async route => {
    const request = route.request();
    const url = new URL(request.url());
    const pathname = url.pathname;
    const headers = { "access-control-allow-origin": "*" };

    if (pathname === "/api/app-state") {
      return route.fulfill({
        headers,
        json: {
          productsData: [
            { sku: "SP-001", name: "Áo thun", type: "Áo", color: "Trắng", size: "M", unit: "Cái", price: 199000, stock: 20 },
            { sku: "SP-002", name: "Áo polo", type: "Áo", color: "Đen", size: "L", unit: "Cái", price: 299000, stock: 10 }
          ],
          productTypesData: [],
          partnersData: [{ category: "Nhân viên", id: "NV-001", name: "Lê Thu Ngân", phone: "0911111111" }],
          promotionsData: [],
          importRequests: [],
          salesHistory: [],
          codOrders: []
        }
      });
    }
    if (pathname === "/api/doi-tra/tra-cuu") {
      return route.fulfill({ headers, json: [invoice] });
    }
    if (pathname === `/api/doi-tra/hoa-don/${invoice.id}`) {
      return route.fulfill({ headers, json: invoice });
    }
    if (pathname === `/api/doi-tra/${receipt.id}`) {
      return route.fulfill({ headers, json: receipt });
    }
    if (pathname === "/api/doi-tra" && request.method() === "POST") {
      postedPayload = request.postDataJSON();
      created = true;
      return route.fulfill({ headers, json: receipt });
    }
    if (pathname === "/api/doi-tra") {
      return route.fulfill({
        headers,
        json: created ? [{
          id: receipt.id,
          createdAt: receipt.createdAt,
          invoiceId: receipt.invoiceId,
          customerName: receipt.customerName,
          phone: receipt.phone,
          resolution: receipt.resolution,
          returnProduct: receipt.returnProduct,
          qty: receipt.qty,
          refundAmount: receipt.refundAmount,
          extraAmount: receipt.extraAmount
        }] : []
      });
    }
    return route.fulfill({ status: 404, headers, json: { detail: "API test không tồn tại" } });
  });

  await page.goto(pathToFileURL(path.join(process.cwd(), "doi-tra.html")).href);
  await page.locator("#returnSearchInput").fill(invoice.phone);
  await expect(page.locator("#returnSearchBody")).toContainText(invoice.id);
  await page.locator("#returnSearchBody button").click();
  await expect(page.locator("#returnInvoicePanel")).toBeVisible();
  await expect(page.locator("#returnInvoiceItems")).toContainText("Áo thun");

  await page.locator('input[name="returnItem"]').check();
  await page.locator("#returnQty").fill("9");
  await expect(page.locator("#returnQty")).toHaveValue("2");
  await page.locator("#returnReason").selectOption({ label: "Hàng bị hư" });
  await page.locator("#returnResolution").selectOption("DOI_SAN_PHAM");
  await page.locator("#returnReplacementSku").selectOption("SP-002");

  await expect(page.locator("#returnOldValue")).toContainText("398.000");
  await expect(page.locator("#returnNewValue")).toContainText("598.000");
  await expect(page.locator("#returnBalanceLabel")).toHaveText("Khách trả thêm");
  await expect(page.locator("#returnBalance")).toContainText("200.000");
  await expect(page.locator("#createReturnButton")).toBeEnabled();

  await page.locator("#createReturnButton").click();
  await expect(page.locator("#returnReceiptModalBg")).toHaveClass(/open/);
  await expect(page.locator("#returnReceiptContent")).toContainText("PHIẾU ĐỔI TRẢ SẢN PHẨM");
  await expect(page.locator("#returnReceiptContent")).toContainText(receipt.id);
  await expect(page.locator("#returnReceiptContent")).not.toContainText("Cửa hàng trưởng");
  await expect(page.locator("#returnReceiptContent .receipt-logo")).toHaveCSS("color", "rgb(36, 72, 153)");
  await expect(page.locator("#returnReceiptContent .receipt-logo span")).toHaveCSS("color", "rgb(245, 158, 11)");
  await expect(page.locator("#returnHistoryBody")).toContainText(receipt.id);

  expect(postedPayload).toEqual({
    invoiceId: invoice.id,
    returnSku: "SP-001",
    qty: 2,
    reason: "Hàng bị hư",
    resolution: "DOI_SAN_PHAM",
    replacementSku: "SP-002",
    employeeId: "NV-001"
  });
});

