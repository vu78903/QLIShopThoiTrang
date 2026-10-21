import { test, expect } from "@playwright/test";
import { pathToFileURL } from "url";
import path from "path";

test("luồng khách hàng, giỏ hàng, VAT và xem lại hóa đơn", async ({ page }) => {
  await page.goto(pathToFileURL(path.join(process.cwd(), "Views", "ban-hang.html")).href);

  await expect(page.locator("#saleProductFieldset")).toBeDisabled();
  await page.getByRole("button", { name: "Bỏ qua, bán cho khách lẻ" }).click();
  await expect(page.locator("#saleProductFieldset")).toBeEnabled();
  await expect(page.locator("#customerInfo")).toContainText("Khách lẻ");

  await page.locator("#skuSearch").fill("SP-001");
  await page.locator("#skuSearch").press("Enter");
  await expect(page.locator("#cartBody")).toContainText("Áo thun Basic");
  await expect(page.locator("#cartBody")).toContainText("Trắng");
  await expect(page.locator("#cartBody")).toContainText("Cái");

  const quantity = page.locator("#cartBody .quantity-input");
  await quantity.fill("2");
  await quantity.blur();
  await expect(quantity).toHaveValue("2");
  await expect(page.locator("#vatAmount")).toContainText("31.840");
  await expect(page.locator("#grandTotal")).toContainText("429.840");

  await page.getByRole("button", { name: "Xem lại hóa đơn" }).click();
  await expect(page.locator("#invoiceHistoryBody tr").first()).toBeVisible();
  await page.locator("#invoiceHistoryBody button[title='Xem hóa đơn']").first().click();
  await expect(page.locator("#receiptModalBg")).toHaveClass(/open/);
  await expect(page.locator("#receiptContent")).toContainText("HÓA ĐƠN THANH TOÁN");
});
