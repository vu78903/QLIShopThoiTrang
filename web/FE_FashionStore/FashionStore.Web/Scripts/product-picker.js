/* Separate size/color choices still submit the exact existing selected-sku field. */
(function () {
    "use strict";
    var select = document.getElementById("sku-select");
    var picker = document.getElementById("sku-picker");
    if (!select || !picker) return;
    var variants = Array.prototype.filter.call(select.options, function (o) { return !!o.value; });
    var sizes = picker.querySelectorAll("[data-size-choice]");
    var colors = picker.querySelectorAll("[data-color-choice]");
    var form = select.form;
    var cart = form.querySelector(".detail-cart");
    var quantity = document.getElementById("detail-quantity");
    var price = document.getElementById("detail-price");
    var stock = document.getElementById("detail-stock");
    var label = document.getElementById("selected-sku-label");
    var colorLabel = document.getElementById("selected-color-label");
    var initialPrice = price.textContent;
    var size = "", color = "";
    var singleFreeSize = sizes.length === 1 && sizes[0].dataset.sizeChoice === "Free size";
    if (singleFreeSize) {
        size = "Free size";
        var sizeFieldset = sizes[0].closest("fieldset");
        if (sizeFieldset) sizeFieldset.hidden = true;
    }
    function available(v) { return Number(v.dataset.stock) > 0; }
    function matches(v) { return v.dataset.size === size && v.dataset.color === color && available(v); }
    function update() {
        colors.forEach(function (button) {
            button.disabled = !variants.some(function (v) {
                return available(v) && v.dataset.color === button.dataset.colorChoice;
            });
            button.setAttribute("aria-pressed", String(button.dataset.colorChoice === color));
        });
        sizes.forEach(function (button) {
            button.disabled = !variants.some(function (v) { return available(v) && v.dataset.size === button.dataset.sizeChoice && (!color || v.dataset.color === color); });
            button.setAttribute("aria-pressed", String(button.dataset.sizeChoice === size));
        });
        var variant = variants.find(matches);
        select.value = variant ? variant.value : "";
        cart.disabled = !variant;
        quantity.disabled = !variant;
        quantity.max = variant ? variant.dataset.stock : "1";
        quantity.value = String(Math.max(1, Math.min(Number(quantity.value) || 1, Number(quantity.max))));
        colorLabel.textContent = color || "Chưa chọn màu";
        label.textContent = variant ? (singleFreeSize ? color : size + " · " + color)
            : color ? (singleFreeSize ? "Màu này hiện đã hết hàng" : "Vui lòng chọn kích cỡ")
            : (singleFreeSize ? "Vui lòng chọn màu sắc" : "Vui lòng chọn màu sắc và kích cỡ");
        price.textContent = variant ? variant.dataset.displayPrice : initialPrice;
        stock.textContent = variant ? "● Còn " + variant.dataset.stock + " sản phẩm" : "Chọn phân loại để xem tồn kho";
        stock.classList.toggle("in-stock", !!variant);
        stock.classList.remove("out-of-stock");
    }
    sizes.forEach(function (button) {
        button.addEventListener("click", function () {
            size = button.dataset.sizeChoice;
            if (color && !variants.some(matches)) color = "";
            update();
        });
    });
    colors.forEach(function (button) {
        button.addEventListener("click", function () {
            color = button.dataset.colorChoice;
            if (size && !variants.some(matches)) size = "";
            update();
            var thumbnail = Array.prototype.find.call(document.querySelectorAll("[data-gallery-color]"), function (b) {
                return b.dataset.galleryColor === color;
            });
            if (thumbnail) thumbnail.click();
            else {
                var main = document.getElementById("detail-main-image");
                if (main) { main.src = button.dataset.colorImage; main.alt = button.querySelector("img").alt; }
            }
        });
    });
    form.addEventListener("submit", function (event) {
        if (!variants.some(function (v) { return v.value === select.value && matches(v); })) {
            event.preventDefault();
            label.textContent = singleFreeSize ? "Vui lòng chọn màu sắc còn hàng." : "Vui lòng chọn kích cỡ và màu sắc còn hàng.";
        }
    });
    select.required = false;
    document.getElementById("sku-fallback").hidden = true;
    picker.hidden = false;
    update();
    var firstColor = Array.prototype.find.call(colors, function (button) { return !button.disabled; });
    if (firstColor) firstColor.click();
}());
