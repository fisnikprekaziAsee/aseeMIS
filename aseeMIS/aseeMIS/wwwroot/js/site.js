
// ===== Toast helpers (global, used by <ejs-toast>) =====
function onAppToastCreated() {
    // 'this' is the EJ2 Toast instance
    window.appToast = this;
    this.target = document.body; // float above everything
}

function getToast() {
    if (window.appToast && typeof window.appToast.show === "function") return window.appToast;
    const inst = document.getElementById("appToast")?.ej2_instances?.[0];
    if (inst) window.appToast = inst;
    return inst || null;
}

// Toast types + notify API
(function () {
    const TYPES = {
        success: { css: "e-toast-success", icon: "toast-icon bi-check-circle", title: "Success!" },
        info: { css: "e-toast-info", icon: "toast-icon bi-info-circle", title: "Information!" },
        warning: { css: "e-toast-warning", icon: "toast-icon bi-exclamation-triangle", title: "Warning!" },
        error: { css: "e-toast-danger", icon: "toast-icon bi-x-circle", title: "Error!" }
    };

    function normalizePos(pos) {
        if (!pos) return { X: "Right", Y: "Top" };
        if (typeof pos === "string") {
            if (pos === "top-right") return { X: "Right", Y: "Top" };
            if (pos === "bottom") return { X: "Center", Y: "Bottom" };
            if (pos === "center-top") return { X: "Center", Y: "Top" };
        }
        return { X: pos.X || "Right", Y: pos.Y || "Top" };
    }

    function show(type, message, opts) {
        const t = getToast();
        if (!t) { console.warn("Toast not ready"); return; }

        const meta = TYPES[(type || "info").toLowerCase()] || TYPES.info;
        const title = (opts && opts.title) || meta.title;
        const css = meta.css + (opts?.cssClass ? " " + opts.cssClass : "");
        const icon = meta.icon;

        try { t.hide("All"); } catch (_) { }

        t.setProperties({
            position: normalizePos(opts?.position),
            timeOut: (typeof opts?.timeOut === "number") ? opts.timeOut : t.timeOut
        }, true);

        t.show({
            title: title,
            content: message,
            cssClass: css,
            icon: icon
        });
    }

    window.notify = {
        show,
        error: (msg, o) => show("error", msg, o),
        success: (msg, o) => show("success", msg, o),
        info: (msg, o) => show("info", msg, o),
        warning: (msg, o) => show("warning", msg, o)
    };

    window.showToastMessage = (type, msg, title) => show(type, msg, { title });
})();

// ===== Layout & menu logic =====

function normalizePath(path) {
    if (!path) return "";
    return path.toLowerCase().replace(/\/+$/, "");
}

function isTrueMenuValue(value) {
    return value === true || value === 1 || String(value).toLowerCase() === "true";
}

function createSidebarMenuItem(menu, isMenuType) {
    const item = document.createElement("li");
    const link = document.createElement("a");
    const formId = Number(menu.FormId ?? menu.formId);
    const title = String(menu.FormTitle ?? menu.formTitle ?? "").trim();
    const iconClass = String(menu.FormIcon ?? menu.formIcon ?? "").trim();
    const formHref = String(menu.FormHref ?? menu.formHref ?? "").trim();

    const fullTitle = Number.isFinite(formId) ? `${formId} - ${title}` : title;

    link.className = "dropdown-item sidebar-dynamic-menu-item";
    link.title = fullTitle;
    link.setAttribute("aria-label", fullTitle);
    link.href = isMenuType
        ? `/Common/AreaNavigation/IndexFavourites?FormId=${encodeURIComponent(formId)}`
        : (formHref || `/Common/AreaNavigation/IndexFavourites?FormId=${encodeURIComponent(formId)}`);

    if (iconClass) {
        const icon = document.createElement("i");
        icon.className = iconClass;
        icon.setAttribute("aria-hidden", "true");
        link.appendChild(icon);
    }

    if (Number.isFinite(formId)) {
        const menuNumber = document.createElement("span");
        menuNumber.className = "sidebar-menu-number";
        menuNumber.textContent = `${formId} -`;
        link.appendChild(menuNumber);
    }

    const label = document.createElement("span");
    label.className = "sidebar-menu-title";
    label.textContent = title;
    link.appendChild(label);
    item.appendChild(link);
    return item;
}

window.renderSidebarFavoriteMenus = function () {
    const favoritesHost = document.getElementById("favorites-menu");
    const reportsHost = document.getElementById("reports-menu");
    if (!favoritesHost || !reportsHost) return;

    let menuData = [];
    try {
        menuData = JSON.parse(localStorage.getItem("formHierarchy") || "[]");
    } catch (error) {
        console.warn("Invalid formHierarchy menu data.", error);
    }

    favoritesHost.replaceChildren();
    reportsHost.replaceChildren();

    if (!Array.isArray(menuData)) return;

    menuData
        .filter(menu => isTrueMenuValue(menu.IsFavorite ?? menu.isFavorite))
        .forEach(menu => {
            const formType = String(menu.FormType ?? menu.formType ?? "").trim();
            const isMenuType = formType.toUpperCase() === "MENU";
            const target = isMenuType ? reportsHost : favoritesHost;
            target.appendChild(createSidebarMenuItem(menu, isMenuType));
        });
};

// Sidebar + search + title
document.addEventListener("DOMContentLoaded", function () {
    // ---- Sidebar ----
    var sidebar = new ej.navigations.Sidebar({
        width: "250px",
        enableGestures: false,
        target: ".wrapper",
        type: "Auto",
        closeOnDocumentClick: false
    });

    sidebar.appendTo("#sidebar");

    if (localStorage.getItem("sidebarOpen") === "true") {
        sidebar.show();
        document.getElementById("main-content").style.marginLeft = "250px";
    } else {
        sidebar.hide();
        document.getElementById("main-content").style.marginLeft = "0px";
    }

    function setSidebarState() {
        var content = document.getElementById("main-content");
        if (sidebar.isOpen) {
            content.style.marginLeft = "250px";
            localStorage.setItem("sidebarOpen", "true");
        } else {
            content.style.marginLeft = "0px";
            localStorage.setItem("sidebarOpen", "false");
        }
    }

    document.getElementById("toggle-sidebar")?.addEventListener("click", function () {
        sidebar.toggle();
        setSidebarState();
    });

    document.getElementById("close-sidebar")?.addEventListener("click", function () {
        sidebar.hide();
        setSidebarState();
    });

    document.querySelectorAll(".sidebar-item").forEach(item => {
        item.addEventListener("click", function () {
            document.querySelectorAll(".sidebar-item").forEach(el => el.classList.remove("active"));
            this.classList.add("active");
            localStorage.setItem("sidebarOpen", "true");
        });
    });

    //// ---- Page title from sessionStorage ----
    //const savedTitle = sessionStorage.getItem("PageTitle");
    //if (savedTitle) {
    //    const pageTitleSpan = document.getElementById("page-title-text");
    //    if (pageTitleSpan) pageTitleSpan.textContent = savedTitle;
    //}

    // ---- Title & search data from menuData ----
    const menuData = JSON.parse(localStorage.getItem("formHierarchy")) || [];
    window.renderSidebarFavoriteMenus();

    // canonical current path: "/common/crud/index/1301"
    let currentPath = window.location.pathname.toLowerCase().replace(/\/+$/, "");
    const id = new URLSearchParams(window.location.search).get("id"); // reads ?id=1301
    if (id) currentPath += "/" + id;

    const isHomePage =
        currentPath === "" ||
        currentPath === "/home" ||
        currentPath === "/home/index";

    const pageTitle = document.getElementById("page-title");
    if (isHomePage) {
        // The landing page has its own Welcome content. Do not render a menu
        // breadcrumb, title, or icon until the user opens an actual form.
        if (pageTitle) pageTitle.style.display = "none";
    }

    const matchingMenu = isHomePage
        ? null
        : menuData.find(menu =>
            Boolean(menu.FormHref) &&
            normalizePath(menu.FormHref) === currentPath);

    if (matchingMenu) {
        const fullTitle = matchingMenu.FullPathTitleParentToLeaf || matchingMenu.FullPathTitle;

        //sessionStorage.setItem("PageTitle", fullTitle);
        document.title = matchingMenu.FormTitle || fullTitle;

        const pageTitleSpan = document.getElementById("page-title-text");
        if (pageTitleSpan) pageTitleSpan.textContent =  matchingMenu.FormTitle;

        const pageTitlePathSpan = document.getElementById("page-title-path");

        //if (pageTitlePathSpan) pageTitlePathSpan.textContent = fullTitle;

        if (pageTitlePathSpan && fullTitle) {

            // Split and clean parts
            const rawParts = fullTitle
                .split("/")
                .map(p => p.trim())
                .filter(p => p.length > 0);

            const displayParts = [];

            rawParts.forEach((part, index) => {
                if (index === 0) {
                    // The application root is always MIS. Never display a technical
                    // form id such as "1210 MIS" in the breadcrumb.
                    displayParts.push("MIS");
                } else {
                    // OTHER PARTS: make them links
                    const url = `/Common/AreaNavigation/Index?FormId=${encodeURIComponent(part)}`;
                    displayParts.push(
                        `<a href="${url}" class="text-muted text-decoration-none">${part}</a>`
                    );
                }
            });

            pageTitlePathSpan.innerHTML = displayParts.join(" / ") + " / ";

            console.log("fullTitle", fullTitle);
        }



        // set the page icon from FormIcon
        const pageIcon = document.getElementById("page-icon");
        if (pageIcon && matchingMenu.FormIcon) {
            pageIcon.className = matchingMenu.FormIcon + " me-2"; // e.g. "bi bi-people-fill me-2"
        }
    } else if (!isHomePage) {
        // Routes that are not represented in the user's menu must have a stable,
        // human-readable fallback instead of an empty breadcrumb/title.
        document.title = "Home";

        const pageTitlePathSpan = document.getElementById("page-title-path");
        if (pageTitlePathSpan) pageTitlePathSpan.textContent = "";

        const pageTitleSpan = document.getElementById("page-title-text");
        if (pageTitleSpan) pageTitleSpan.textContent = "Home";
    }


    // ---- Search icon toggle ----
    var searchIcon = document.getElementById("search-icon");
    var searchBoxContainer = document.getElementById("search-box-container");

    const acElem = document.getElementById("topMenu");
    if (acElem && acElem.ej2_instances && acElem.ej2_instances[0]) {
        var searchInput = acElem.ej2_instances[0];
        searchInput.dataSource = menuData;
    }

    searchIcon?.addEventListener("click", function (event) {
        event.stopPropagation();
        if (!searchBoxContainer) return;

        searchBoxContainer.classList.toggle("show");
        if (searchBoxContainer.classList.contains("show")) {
            setTimeout(() => searchInput?.focusIn(), 100);
        }
    });

    document.addEventListener("click", function (event) {
        if (!searchBoxContainer) return;
        if (!searchBoxContainer.contains(event.target) && event.target !== searchIcon) {
            searchBoxContainer.classList.remove("show");
        }
    });
});

// ===== AutoComplete keyboard + handlers =====
let topMenuFirstMatch = null;

function onACActionComplete(args) {
    topMenuFirstMatch = (args && Array.isArray(args.result) && args.result.length)
        ? args.result[0]
        : null;

    const ac = document.getElementById("topMenu")?.ej2_instances?.[0];
    const firstLi = ac?.popupObj?.element?.querySelector(".e-list-item");
    if (firstLi) {
        ac?.popupObj?.element?.querySelectorAll(".e-active")
            ?.forEach(li => li.classList.remove("e-active"));
        firstLi.classList.add("e-active");
    }
}

window.addEventListener("load", () => {
    const ac = document.getElementById("topMenu")?.ej2_instances?.[0];
    if (!ac || !ac.inputElement) return;

    ac.inputElement.addEventListener("keydown", (e) => {
        if (e.key === "Enter" && topMenuFirstMatch) {
            e.preventDefault();
            ac.value = topMenuFirstMatch.FormTitle;
            ac.dataBind();
            onAutoCompleteSelect({ itemData: topMenuFirstMatch });
            ac.hidePopup();
        }
    });
});

function onAutoCompleteSelect(e) {
    if (e.itemData) {
        //const fullTitle = e.itemData.FullPathTitle ||  e.itemData.FullPathTitleParentToLeaf || e.itemData.FormTitle;
        //sessionStorage.setItem("PageTitle", fullTitle);

       // const pageTitleSpan = document.getElementById("page-title-text");
       // if (pageTitleSpan) pageTitleSpan.textContent = fullTitle;

        window.location.href = e.itemData.FormHref;
    }
}

function bindDirectReplicationStatusFilter(inputId, currentValue) {
    const input = document.getElementById(inputId);
    if (!input || input.ej2_instances?.length) return;

    const selected = currentValue || "notSync";
    new ej.dropdowns.DropDownList({
        dataSource: [
            { text: "Te gjitha", value: "all" },
            { text: "Not Sync", value: "notSync" }
        ],
        fields: { text: "text", value: "value" },
        value: selected,
        width: 150,
        change: function (e) {
            if (e.value === selected) return;
            const url = new URL(window.location.href);
            if (e.value === "all")
                url.searchParams.set("syncFilter", "all");
            else
                url.searchParams.delete("syncFilter");
            window.location.href = url.toString();
        }
    }).appendTo(input);
}
