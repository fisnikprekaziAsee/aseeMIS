function getLocalizedText(stringType, key) {
    const entry = JSON.parse(localStorage.getItem(stringType) || "[]")
        .find(item => item.stringType === stringType && item.stringKey === key);
    return entry ? entry.stringValue : '';
}

function getAreaLocalizedText(localStorageArea, key) {
    const entry = JSON.parse(localStorage.getItem(localStorageArea) || "[]")
        .find(item => item.StringKey === (key || '').toUpperCase());
    return entry ? entry.StringValue : '';
}

function getLocalizedTextFast(area, key) {
    const data = JSON.parse(localStorage.getItem(area) || "{}");
    return data[(key || '').toUpperCase()] || '';
}

// apply localized headers and refresh the header only
function applyGridHeadersFromLS(gridObj, localStorageArea) {
    const cols = (typeof gridObj.getColumns === "function")
        ? gridObj.getColumns()
        : (gridObj.columns || []);
    cols.forEach(col => {

        const key = (col.headerText || col.field || "").toString();
        const localized = getAreaLocalizedText(localStorageArea, key.toUpperCase());
        if (localized) col.headerText = localized;
    });
    if (typeof gridObj.refreshHeader === "function") gridObj.refreshHeader();
    else gridObj.refresh();
}

function gridDataBound(gridObj, localStorageArea) {

    var localDatabaseVersion = localStorage.getItem("LanguageStringsVersion") || "0";
    var localStorageAreaVersion = getAreaLocalizedText(localStorageArea, "LocalStorageAreaVersion");

    if (localDatabaseVersion !== localStorageAreaVersion) {
        $.ajax({
            url: '/Home/GetLanguageStringsByStringType',
            type: 'GET',
            data: { stringType: localStorageArea },
            success: function (response) {
                response.push({
                    StringKey: "LocalStorageAreaVersion",
                    StringValue: localDatabaseVersion
                });
                localStorage.setItem(localStorageArea, JSON.stringify(response));
                applyGridHeadersFromLS(gridObj, localStorageArea);
            },
            error: function (xhr, status, error) {
                applyGridHeadersFromLS(gridObj, localStorageArea);
            }
        });
    } else {
        applyGridHeadersFromLS(gridObj, localStorageArea);
    }
}
function applyFormLocalizationFromLS(localStorageArea) {
    // Load translations from localStorage
    const translations = JSON.parse(localStorage.getItem(localStorageArea) || "[]");

    if (!Array.isArray(translations)) return;

    document.querySelectorAll("[ejs-for]").forEach(el => {
        const ej = el.ej2_instances?.[0];
        if (!ej) return;

        const fieldKey = el.id || el.getAttribute("name");
        if (!fieldKey) return;

        // Try to find translation (case-insensitive)
        const entry = translations.find(
            t => (t.StringKey || "").toUpperCase() === fieldKey.toUpperCase()
        );

        if (entry && entry.StringValue) {
            const localized = entry.StringValue.trim();

            // Apply to placeholder (and float label)
            if ("placeholder" in ej) {
                ej.placeholder = localized;
                ej.floatLabelType = "Always";
            }

            // Some Syncfusion components (like dropdowns) need dataBind() to re-render
            if (typeof ej.dataBind === "function") ej.dataBind();
        }
    });
}

function formLocalizationDataBound(localStorageArea) {
    const localVersion = localStorage.getItem("LanguageStringsVersion") || "0";
    const storedVersion = getAreaLocalizedText(localStorageArea, "LocalStorageAreaVersion");

    if (localVersion !== storedVersion) {
        $.ajax({
            url: '/Home/GetLanguageStringsByStringType',
            type: 'GET',
            data: { stringType: localStorageArea },
            success: function (response) {
                response.push({
                    StringKey: "LocalStorageAreaVersion",
                    StringValue: localVersion
                });
                localStorage.setItem(localStorageArea, JSON.stringify(response));
                applyFormLocalizationFromLS(localStorageArea);
            },
            error: function () {
                applyFormLocalizationFromLS(localStorageArea);
            }
        });
    } else {
        applyFormLocalizationFromLS(localStorageArea);
    }
}
