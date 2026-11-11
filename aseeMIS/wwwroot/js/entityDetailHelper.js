// ================================
// ds namespace (REAL global)
// ================================
var ds = window.ds = window.ds || {};
ds.entity = ds.entity || {};

// ================================
// DOM / EJ2 helpers
// ================================
ds.byId = function (id) {
    return document.getElementById(id);
};

ds.ej = function (id) {
    const el = document.getElementById(id);
    return el && el.ej2_instances && el.ej2_instances[0];
};

ds.entity.setSavingState = function (isSaving, message) {
    ds.entity.isSaving = isSaving;

    let overlay = document.getElementById("ds-save-overlay");
    if (isSaving) {
        if (!overlay) {
            overlay = document.createElement("div");
            overlay.id = "ds-save-overlay";
            overlay.className = "ds-save-overlay";
            overlay.setAttribute("role", "status");
            overlay.setAttribute("aria-live", "polite");
            overlay.innerHTML =
                '<div class="ds-save-overlay__content">' +
                '<span class="spinner-border text-primary" aria-hidden="true"></span>' +
                '<span class="ds-save-overlay__message"></span>' +
                '</div>';
            document.body.appendChild(overlay);
        }

        const messageElement = overlay.querySelector(".ds-save-overlay__message");
        if (messageElement) messageElement.textContent = message || "Saving, please wait...";

        overlay.hidden = false;
        document.body.setAttribute("aria-busy", "true");
        return;
    }

    if (overlay) overlay.hidden = true;
    document.body.removeAttribute("aria-busy");
};

// Safe set value into EJ2 + databind
ds.entity.setField = function (id, value) {
    const ej = ds.ej(id);
    const el = ds.byId(id);

    if (ej) {
        if (typeof ej.checked === "boolean") ej.checked = !!value; // checkbox/switch
        else ej.value = value;

        if (typeof ej.dataBind === "function") ej.dataBind();
    }
    if (el && "value" in el) el.value = value ?? "";
};

// ================================
// Value normalization
// ================================
ds.entity.normalizeValue = function (ej, el) {

    if (!ej && !el) return null;

    // Checkbox / Switch (EJ2)
    if (ej && typeof ej.checked === "boolean")
        return !!ej.checked;

    // EJ2 AutoComplete keeps the visible text in `value`, while the selected
    // record (and therefore the actual foreign-key value) is in `itemData`.
    // Posting `value` here would send e.g. "F260... | Framework ..." to a
    // long FrameworkId property.
    if (ej && typeof ej.getModuleName === "function" && ej.getModuleName() === "autocomplete") {
        const selectedItem = ej.itemData;

        if (selectedItem) {
            // `fields.value` is commonly configured as DisplayText because that is
            // what EJ2 must render in the input.  It is therefore not necessarily
            // the foreign-key field that must be posted.  Prefer the explicit
            // metadata and then the bound property/input id (e.g. FrameworkId).
            const valueFieldCandidates = [
                el?.dataset?.autocompleteValueField,
                ej.element?.dataset?.autocompleteValueField,
                el?.id,
                ej.element?.id,
                ej.fields?.value
            ].filter(Boolean);

            const itemKeys = Object.keys(selectedItem);
            for (const candidate of valueFieldCandidates) {
                const itemKey = itemKeys.find(key =>
                    key.localeCompare(candidate, undefined, { sensitivity: "accent" }) === 0);

                if (itemKey && selectedItem[itemKey] !== undefined)
                    return selectedItem[itemKey];
            }
        }

        // A typed but unselected label is not a valid foreign-key value.
        return null;
    }

    // Prefer EJ2 value, fallback to DOM input value
    let v = ej ? ej.value : (el ? el.value : null);

    if (v === "" || v === undefined)
        return null;

    // NumericTextBox
    if (ej && typeof ej.decimals === "number") {
        const n = Number(v);
        if (!Number.isFinite(n))
            return null;

        return ej.decimals === 0
            ? Math.trunc(n)
            : n;
    }

    // DatePicker / DateTimePicker
    if (ej && typeof ej.getModuleName === "function") {
        const moduleName = ej.getModuleName();

        if (moduleName === "datepicker" || moduleName === "datetimepicker") {
            if (!ej.value) return null;

            if (el && el.dataset && el.dataset.dateonly === "true") {
                const d = ej.value;
                return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
            }

            return ej.value;
        }
    }

    return v;
};

// ================================
// Collect model from ejs-for.
// Keeps the legacy id-based fields, and also builds nested objects from ejs-for paths.
// ================================
ds.entity.setValueByPath = function (model, path, value) {
    if (!model || !path || typeof path !== "string") return;

    const parts = path.split(".").filter(Boolean);
    if (parts.length === 0) return;

    let current = model;
    for (let i = 0; i < parts.length - 1; i++) {
        const part = parts[i];
        if (!current[part] || typeof current[part] !== "object" || Array.isArray(current[part])) {
            current[part] = {};
        }
        current = current[part];
    }

    current[parts[parts.length - 1]] = value;
};

ds.entity.collectModel = function (container) {
    const model = {};
    const scope = container || document;

    scope.querySelectorAll("[ejs-for]").forEach(el => {
        if (!el.id) return;
        const ej = el.ej2_instances?.[0] ?? null;
        const value = ds.entity.normalizeValue(ej, el);
        const input = el.matches?.("input,select,textarea")
            ? el
            : el.querySelector?.("input[name],select[name],textarea[name]");
        const ejsFor = el.getAttribute("ejs-for");
        const modelPath =
            ejsFor && ejsFor !== "true" && ejsFor !== "false"
                ? ejsFor
                : input?.getAttribute("name");

        model[el.id] = value;

        if (modelPath && modelPath !== el.id) {
            ds.entity.setValueByPath(model, modelPath, value);
        }
    });

    if (window.dsEntityDebug === true) {
        console.log("[ds.entity.collectModel]", model);
    }

    return model;
};

ds.entity.getOwnPropertyName = function (obj, name) {
    if (!obj || !name || typeof obj !== "object") return null;
    return Object.keys(obj).find(k => k.toLowerCase() === String(name).toLowerCase()) || null;
};

ds.entity.findValueInModel = function (obj, fieldName) {
    if (!obj || typeof obj !== "object") return undefined;

    const ownName = ds.entity.getOwnPropertyName(obj, fieldName);
    if (ownName) return obj[ownName];

    for (const key of Object.keys(obj)) {
        const value = obj[key];
        if (value && typeof value === "object" && !Array.isArray(value)) {
            const nested = ds.entity.findValueInModel(value, fieldName);
            if (nested !== undefined) return nested;
        }
    }

    return undefined;
};

ds.entity.setDisplayValueNearField = function (obj, fieldName, displayFieldName, displayValue) {
    if (!obj || typeof obj !== "object" || !displayFieldName) return false;

    const ownName = ds.entity.getOwnPropertyName(obj, fieldName);
    if (ownName) {
        const existingDisplayName = ds.entity.getOwnPropertyName(obj, displayFieldName) || displayFieldName;
        obj[existingDisplayName] = displayValue;
        return true;
    }

    for (const key of Object.keys(obj)) {
        const value = obj[key];
        if (value && typeof value === "object" && !Array.isArray(value)) {
            if (ds.entity.setDisplayValueNearField(value, fieldName, displayFieldName, displayValue)) {
                return true;
            }
        }
    }

    return false;
};

ds.entity.findSelectedFkRecord = function (rows, value) {
    if (!Array.isArray(rows) || value === null || value === undefined || value === "") return null;

    const valueText = String(value);
    return rows.find(row => {
        if (!row || typeof row !== "object") return false;

        const props = Object.keys(row);
        const firstProp = props[0];
        if (firstProp && String(row[firstProp]) === valueText) return true;

        return props.some(prop => String(row[prop]) === valueText);
    }) || null;
};

ds.entity.readFkDisplayValue = function (row, displayFieldName) {
    if (!row || typeof row !== "object") return undefined;

    const displayProp = ds.entity.getOwnPropertyName(row, displayFieldName);
    if (displayProp) return row[displayProp];

    const props = Object.keys(row);
    return props.length > 1 ? row[props[1]] : undefined;
};

// Hydrates hidden/read-only FK display fields, e.g. CountryName, before generic save/staging log.
ds.entity.hydrateForeignKeyDisplayFields = function (model, container) {
    if (!model || typeof model !== "object") return model;

    const formSessionId = container?.dataset?.formSessionId || null;
    if (!formSessionId) return model;

    const fkData = window.ds?.entity?.fkData?.[formSessionId] || {};
    const fkDataSourceMap = window.ds?.entity?.fkDataSourceMap?.[formSessionId] || {};
    const fkDisplayMap = window.ds?.entity?.fkDisplayMap?.[formSessionId] || [];

    fkDisplayMap.forEach(meta => {
        const fieldName = meta?.field || meta?.Field;
        const displayFieldName = meta?.displayField || meta?.DisplayField;
        if (!fieldName || !displayFieldName) return;

        const sourceKey =
            fkDataSourceMap[fieldName] ||
            fkDataSourceMap[String(fieldName).split(".").pop()] ||
            meta?.dataSourceKey ||
            meta?.DataSourceKey ||
            fieldName;

        const rows =
            fkData["FK_" + sourceKey] ||
            fkData["FK_" + fieldName] ||
            window["FK_" + sourceKey] ||
            window["FK_" + fieldName] ||
            [];

        const selectedValue = ds.entity.findValueInModel(model, fieldName);
        const selectedRecord = ds.entity.findSelectedFkRecord(rows, selectedValue);
        const displayValue = ds.entity.readFkDisplayValue(selectedRecord, displayFieldName);

        if (displayValue !== undefined && displayValue !== null) {
            ds.entity.setDisplayValueNearField(model, fieldName, displayFieldName, displayValue);
        }
    });

    return model;
};

// ================================
// Validation helpers
// ================================
ds.entity.focusFirstInvalid = function () {
    const el =
        document.querySelector(".e-error") ||
        document.querySelector(".e-invalid") ||
        document.querySelector("[aria-invalid='true']");

    if (el) {
        el.scrollIntoView({ behavior: "smooth", block: "center" });
        const input = el.querySelector("input,textarea,select") || el;
        input.focus?.();
    }
};

// EJ2 FormValidator (optional)
ds.entity.validateForm = function () {
    if (window.fv && typeof window.fv.validate === "function") {
        const ok = window.fv.validate();
        if (!ok) ds.entity.focusFirstInvalid();
        return ok;
    }
    return true;
};

// HTML5 validation (required/minlength/maxlength/pattern)
ds.entity.validateHtmlForm = function (container) {
    if (!container) return true;

    const form = container.closest("form");

    if (form && typeof form.reportValidity === "function") {
        const hiddenDropdowns = Array.from(form.querySelectorAll("select.e-ddl-hidden[required]"));
        for (const select of hiddenDropdowns) {
            if (select.disabled) continue;
            const component = document.getElementById(select.id.replace(/_hidden$/, ""))?.ej2_instances?.[0];
            const selected = component?.value ?? select.value;
            if (selected === null || selected === undefined || selected === "") {
                component?.focusIn?.();
                const input = component?.inputElement;
                input?.focus?.();
                if (input?.setCustomValidity && input?.reportValidity) {
                    input.setCustomValidity(`${component?.placeholder || "This field"} is required.`);
                    input.reportValidity();
                    input.setCustomValidity("");
                }
                return false;
            }
        }
        // EJ2's backing selects are aria-hidden and cannot receive native invalid focus.
        // Their required values were checked above; retain native validation for other fields.
        hiddenDropdowns.forEach(select => select.removeAttribute("required"));
        try { return form.reportValidity(); }
        finally { hiddenDropdowns.forEach(select => select.setAttribute("required", "required")); }
    }

    return true;
};

// ================================
// Detect PK (explicit via data attribute)
// ================================
ds.entity.detectPrimaryKey = function (model, container) {
    if (!model || !container) return null;

    const pkName = container.dataset.primaryKey;
    if (!pkName) return null;

    if (!Object.prototype.hasOwnProperty.call(model, pkName))
        return null;

    return {
        name: pkName,
        value: model[pkName]
    };
};

// ================================
// Check all and return isUpdate (has valid PK value) or Add (no PK or empty PK)
// ================================
ds.entity.isUpdate = function (pk) {
    console.log("PK IN IS UPDATE: ", pk);
    return !!(
        pk &&
        pk.value !== null &&
        pk.value !== undefined &&
        pk.value !== "" &&
        pk.value !== 0 &&
        pk.value !== "0"
    );
};

// ================================
// Default preSave
// (Views can override this to map/sync extra fields, then call ds.entity.save())
// ================================
ds.entity.preSave = function () {
    ds.entity.save();
};

// ================================
// Main SAVE (Add / Update)
// ================================
ds.entity.save = function (triggerElement) {
    if (ds.entity.isSaving) return;

    const container =
        triggerElement?.closest?.(".form-container") ||
        document.querySelector(".form-container");

    if (!container) {
        notify?.error?.("Form container not found");
        return;
    }

    const formSessionId = container.dataset.formSessionId || null;
    const formId = container.dataset.formId || null;

    // HTML5 validation
    if (!ds.entity.validateHtmlForm(container)) {
        notify?.warning?.("Please correct validation errors");
        return;
    }

    // EJ2 validation
    if (!ds.entity.validateForm()) {
        notify?.warning?.("Please correct validation errors");
        return;
    }

    // Syncfusion DatePicker / DateTimePicker required-value validation.
    // Native HTML5 required="true" does NOT catch .NET DateOnly.MinValue
    // (0001-01-01) round-tripping back as a "valid" date value, and the EJ2
    // FormValidator is optional. This bridges that gap so users can never
    // save a form with placeholder date fields silently posting MinValue.
    if (window.ds?.input?.validateRequiredDatePickers
        && !window.ds.input.validateRequiredDatePickers(container, { showToast: true })) {
        return;
    }

    // collect model
    let model = ds.entity.collectModel(container);
    if (window.dsEntityDebug === true) {
        console.log("[ds.entity.save] collected model", model);
    }
    console.log("model", model);
    // detect PK
    const pk = ds.entity.detectPrimaryKey(model, container);
    const isUpdate = ds.entity.isUpdate(pk);

    // optional model preparation hook
    const prepareModel =
        typeof ds.entity.prepareModel === "function"
            ? ds.entity.prepareModel
            : ds.entity.beforeSave; // legacy fallback for older forms

    if (typeof prepareModel === "function") {
        const maybeModel = prepareModel(model, container, isUpdate);
        if (maybeModel) {
            model = maybeModel;
        }
    }
    if (window.dsEntityDebug === true) {
        console.log("[ds.entity.save] prepared model", model);
    }

    model = ds.entity.hydrateForeignKeyDisplayFields(model, container);
    if (window.dsEntityDebug === true) {
        console.log("[ds.entity.save] hydrated model", model);
    }

    const endpoint = isUpdate ? "/Common/CRUD/Update" : "/Common/CRUD/Add";

    ds.entity.setSavingState(true, isUpdate ? "Saving changes..." : "Creating, please wait...");

    const ajax = new ej.base.Ajax({
        url: endpoint,
        type: "POST",
        data: JSON.stringify(model),
        contentType: "application/json"
    });

    ajax.beforeSend = function (args) {
        if (formSessionId) {
            args.httpRequest.setRequestHeader("FormSessionId", formSessionId);
        }
    };

    ajax.send()
        .then(resp => {
            let result = {};
            try { result = JSON.parse(resp); } catch { }

            if (!result || result.success !== true) {
                ds.entity.setSavingState(false);
                notify?.error?.(result?.message || "Save failed");
                return;
            }

            notify?.success?.(isUpdate ? "Updated successfully" : "Saved successfully");

            // generic callback
            if (typeof ds.entity.afterSave === "function") {
                ds.entity.setSavingState(false);
                ds.entity.afterSave(result, model, container, isUpdate);
                return;
            }

            // specific callbacks
            if (!isUpdate && typeof ds.entity.afterAdd === "function") {
                ds.entity.setSavingState(false);
                ds.entity.afterAdd(result, model, container);
                return;
            }

            if (isUpdate && typeof ds.entity.afterUpdate === "function") {
                ds.entity.setSavingState(false);
                ds.entity.afterUpdate(result, model, container);
                return;
            }

            // default fallback only for Entity360 Add
            if (!isUpdate && window.location.pathname.includes("/Common/Entity360")) {
                const row = result.updatedRow || result.row || result.data || {};
                const newPk =
                    (pk?.name ? row[pk.name] : null) ??
                    row.Id ?? row.id ?? row.PrimaryKey ?? row.primaryKey ?? result.frameworkId ?? result.id;

                if (newPk != null) {
                    setTimeout(() => {
                        const url = new URL("/Common/Entity360/Index", window.location.origin);
                        if (formId) url.searchParams.set("id", String(formId));
                        url.searchParams.set("entityPrimaryKeyValue", String(newPk));
                        window.location.href = url.toString();
                    }, 1200);
                    return;
                }
            }

            ds.entity.setSavingState(false);
        })
        .catch(err => {
            ds.entity.setSavingState(false);
            console.error("Save error:", err);
            notify?.error?.("Unexpected error occurred");
        });
};
