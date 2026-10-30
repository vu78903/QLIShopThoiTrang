(function () {
    "use strict";

    if (!document.body.classList.contains("customer-site")) return;
    var menuButton = document.getElementById("menu-toggle");
    var nav = document.getElementById("main-nav");
    var searchButton = document.getElementById("search-toggle");
    var searchPanel = document.getElementById("search-panel");
    var searchInput = document.getElementById("site-search");
    var accountButton = document.getElementById("account-toggle");
    var accountPanel = document.getElementById("account-panel");
    var wide = window.matchMedia("(min-width: 1200px)");
    var panels = [
        { button: menuButton, panel: nav, menu: true },
        { button: searchButton, panel: searchPanel },
        { button: accountButton, panel: accountPanel }
    ];
    function setOpen(item, open, returnFocus) {
        if (!item.button || !item.panel) return;
        item.panel.hidden = !open;
        item.panel.classList.toggle("open", open);
        item.button.setAttribute("aria-expanded", String(open));
        if (returnFocus) item.button.focus();
    }
    function closeOthers(except) {
        panels.forEach(function (item) {
            if (item !== except && !(item.menu && wide.matches)) setOpen(item, false, false);
        });
    }
    panels.forEach(function (item) {
        if (!item.button || !item.panel) return;
        item.button.addEventListener("click", function () {
            var open = item.panel.hidden;
            closeOthers(item);
            setOpen(item, open, false);
            if (open && item === panels[1] && searchInput) searchInput.focus();
            else if (open) {
                var first = item.panel.querySelector("a, button, input");
                if (first) first.focus();
            }
        });
    });
    function syncNavigation() {
        if (!nav) return;
        var restore = !wide.matches && nav.contains(document.activeElement);
        setOpen(panels[0], wide.matches, restore);
    }
    syncNavigation();
    if (wide.addEventListener) wide.addEventListener("change", syncNavigation);
    else wide.addListener(syncNavigation);
    document.addEventListener("keydown", function (event) {
        if (event.key !== "Escape") return;
        panels.forEach(function (item) {
            if (item.panel && !item.panel.hidden && !(item.menu && wide.matches)) {
                setOpen(item, false, true);
            }
        });
    });
    document.addEventListener("click", function (event) {
        panels.forEach(function (item) {
            if (item.panel && item.button && !(item.menu && wide.matches) &&
                !item.panel.contains(event.target) && !item.button.contains(event.target)) setOpen(item, false, false);
        });
    });
    document.addEventListener("focusin", function (event) {
        panels.forEach(function (item) {
            if (item.panel && item.button && !(item.menu && wide.matches) &&
                !item.panel.contains(event.target) && !item.button.contains(event.target)) setOpen(item, false, false);
        });
    });
    // In-page anchors must not leave focus inside a hidden mobile navigation.
    if (nav) nav.addEventListener("click", function (event) {
        if (event.target.closest("a") && !wide.matches) setOpen(panels[0], false, true);
    });
    var accountNavButton = document.querySelector(".yody-account-nav-toggle");
    var accountNav = document.getElementById("customer-nav-links");
    var accountNarrow = window.matchMedia("(max-width: 767px)");
    if (accountNavButton && accountNav) {
        function syncAccountNav() {
            accountNavButton.hidden = !accountNarrow.matches;
            if (accountNarrow.matches && accountNav.contains(document.activeElement)) accountNavButton.focus();
            accountNav.hidden = accountNarrow.matches;
            accountNavButton.setAttribute("aria-expanded", String(!accountNav.hidden));
        }
        accountNavButton.addEventListener("click", function () {
            accountNav.hidden = !accountNav.hidden;
            accountNavButton.setAttribute("aria-expanded", String(!accountNav.hidden));
        });
        accountNav.addEventListener("keydown", function (event) {
            if (event.key === "Escape" && accountNarrow.matches) {
                accountNav.hidden = true;
                accountNavButton.setAttribute("aria-expanded", "false");
                accountNavButton.focus();
            }
        });
        syncAccountNav();
        if (accountNarrow.addEventListener) accountNarrow.addEventListener("change", syncAccountNav);
        else accountNarrow.addListener(syncAccountNav);
    }

    // Keep unavailable remote images from leaving broken-image icons.
    var placeholder = "data:image/svg+xml;charset=utf-8," + encodeURIComponent(
        '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 600 700">' +
        '<rect width="600" height="700" fill="#FAFAF9"/>' +
        '<path d="M275 280c0-38 52-38 52 0 0 20-27 28-27 48l120 72H180l120-72" fill="none" stroke="#44403C" stroke-width="6" stroke-linejoin="round"/>' +
        '<text x="300" y="452" text-anchor="middle" fill="#44403C" font-family="sans-serif" font-size="22">Ảnh đang cập nhật</text></svg>'
    );
    Array.prototype.forEach.call(document.querySelectorAll("img"), function (image) {
        function showPlaceholder() {
            image.removeEventListener("error", showPlaceholder);
            image.src = placeholder;
            image.alt = "Ảnh chưa tải được: " + (image.alt || "sản phẩm");
        }
        image.addEventListener("error", showPlaceholder);
        if (image.complete && image.naturalWidth === 0) showPlaceholder();
    });
    // Read-only details interactions: select a SKU and update its displayed price/stock.
    var variantRadios = document.querySelectorAll('input[name="selected-sku"]');
    var priceTarget = document.getElementById("detail-price");
    var stockTarget = document.getElementById("detail-stock");
    Array.prototype.forEach.call(variantRadios, function (radio) {
        radio.addEventListener("change", function () {
            if (!radio.checked) return;
            var quantity = document.getElementById("detail-quantity");
            var available = Number(radio.getAttribute("data-stock") || 0);
            if (quantity) { quantity.max = String(available); quantity.value = String(Math.max(1, Math.min(Number(quantity.value) || 1, available))); }
            var sku = document.getElementById("selected-sku-label");
            if(sku) sku.textContent = radio.value;
            if (priceTarget) priceTarget.textContent = radio.getAttribute("data-display-price") || "";
            if (stockTarget) {
                var stock = Number(radio.getAttribute("data-stock") || 0);
                stockTarget.textContent = stock > 0 ? "● Còn " + stock + " sản phẩm" : "● Tạm hết hàng";
                stockTarget.classList.toggle("in-stock", stock > 0);
                stockTarget.classList.toggle("out-of-stock", stock <= 0);
            }
        });
    });

    var galleryImage = document.getElementById("detail-main-image");
    var galleryButtons = document.querySelectorAll("[data-gallery-image]");
    Array.prototype.forEach.call(galleryButtons, function (button) {
        // The initial full-size image may differ from the first thumbnail.
        var selectedInitially = !!(galleryImage && button.getAttribute("data-gallery-image") === galleryImage.getAttribute("src"));
        button.classList.toggle("active", selectedInitially);
        button.setAttribute("aria-pressed", String(selectedInitially));
        button.addEventListener("click", function () {
            if (galleryImage) {
                galleryImage.src = button.getAttribute("data-gallery-image");
                var thumbnail = button.querySelector("img");
                if (thumbnail && thumbnail.alt) galleryImage.alt = thumbnail.alt;
            }
            Array.prototype.forEach.call(galleryButtons, function (other) {
                var active = other === button;
                other.classList.toggle("active", active);
                other.setAttribute("aria-pressed", String(active));
            });
        });
    });
}());

(function(){
 "use strict";
 if (!document.body.classList.contains("customer-site")) return;
 var badge=document.getElementById("cart-count");
 if(badge && window.fetch) fetch(badge.getAttribute("data-url"),{credentials:"same-origin",headers:{"Accept":"application/json"}}).then(function(r){if(!r.ok)throw new Error();return r.json();}).then(function(data){if(typeof data.count==="number"){badge.textContent=data.count>99?"99+":String(data.count);badge.hidden=data.count===0;}}).catch(function(){badge.hidden=true;});
 document.querySelectorAll(".account-nav a").forEach(function(a){if(a.pathname.toLowerCase()===location.pathname.toLowerCase())a.setAttribute("aria-current","page");});
 document.addEventListener("submit",function(e){
  var form=e.target;var submitter=e.submitter;
  if(submitter&&submitter.hasAttribute("data-confirm")&&!window.confirm(submitter.getAttribute("data-confirm"))){e.preventDefault();return;}
  if(window.jQuery&&window.jQuery.validator&&window.jQuery(form).data("validator")&&!window.jQuery(form).valid())return;
  // Keep submitter name/value available for MVC (voucher preview uses intent).
  if(!e.defaultPrevented && form.method.toLowerCase()==="post"){
   form.setAttribute("aria-busy","true");setTimeout(function(){form.querySelectorAll('button[type="submit"]').forEach(function(b){b.disabled=true;});},0);
   setTimeout(function(){form.removeAttribute("aria-busy");form.querySelectorAll('button[type="submit"]').forEach(function(b){b.disabled=false;});},15000);
  }
 });
}());

/* YODY Phase 3: progressive enhancement for the catalog filter and gallery. */
(function () {
    "use strict";
    if (!document.body.classList.contains("customer-site")) return;
    var filter = document.getElementById("yody-catalog-filters");
    if (filter && window.matchMedia) {
        var desktop = window.matchMedia("(min-width: 1024px)");
        function syncFilter(event) {
            // Desktop shows the filter sidebar; mobile begins collapsed.
            // No-JS fallback remains open for full access to form fields.
            filter.open = event.matches;
        }
        syncFilter(desktop);
        if (desktop.addEventListener) desktop.addEventListener("change", syncFilter);
        else if (desktop.addListener) desktop.addListener(syncFilter);
    }
    // Product gallery aria-pressed is managed alongside the main image above.
}());

