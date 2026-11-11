/**
 * ============================================================================================
 * Syncfusion Grid Utility Script for dsMIS Applications
 * ============================================================================================
 */

/* ---------------------------- Notification Helpers ---------------------------- */

function getMsgInstance(arg) {
    let gridId = null;

    if (arg && typeof arg === "string") {
        gridId = arg;
    } else if (arg && arg.element && arg.element.id) {
        gridId = arg.element.id;
    } else if (window && window.__currentGridId) {
        gridId = window.__currentGridId;
    }

    if (gridId) {
        const el = document.getElementById(gridId + "_MSG");
        const inst = el?.ej2_instances?.[0];
        if (inst) return inst;
    }

    if (gridId && window.__msgInstances && window.__msgInstances[gridId]) {
        return window.__msgInstances[gridId];
    }

    return (typeof window.msgInstance !== "undefined") ? window.msgInstance : null;
}

function hideMessages(arg) {
    const mi = getMsgInstance(arg);
    if (!mi) return;
    try {
        mi.visible = false;
        mi.content = "";
    } catch (_) { }
}

function showMessage(message, severity = "Success", arg) {
    // Generic CRUD feedback is displayed through the application toast. Always
    // clear a previously displayed grid message so stale banners do not remain
    // above the grid after this behavior is applied.
    const mi = getMsgInstance(arg);
    if (mi) {
        try {
            mi.visible = false;
            mi.content = "";
        } catch (_) { }
    }

    const notifyKey = { Error: "error", Warning: "warning", Info: "info", Success: "success" }[severity] || "info";
    if (window.notify && typeof window.notify[notifyKey] === "function") {
        try { window.notify[notifyKey](message); return; } catch (_) { }
    }

    (severity === "Error" ? console.error : console.log)("[GRID MSG]", message);
}

/* ---------------------------- Visual Helpers ---------------------------- */

function statusDetail(e) {
    const div = document.createElement("div");
    const span = document.createElement("span");

    if (e.IsApproved === true) {
        span.textContent = "\u2713";
        span.style.color = "green";
    } else if (e.IsApproved === false) {
        span.textContent = "x";
        span.style.color = "red";
    } else {
        span.textContent = "...";
        span.style.color = "orange";
    }

    span.style.fontWeight = "bold";
    div.appendChild(span);
    return div.outerHTML;
}

/* ---------------------------- Column Helpers ---------------------------- */

function showAllColumns(grid, requestType) {
    if (requestType === "beginEdit" || requestType === "add") {
        hideMessages(grid);
        grid.columns.forEach(col => {
            if (!col.visible && col.field) {
                grid.showColumns([col.field], "field");
            }
        });
    }
}

/* ------------------------- MaskedTextBox Bindings -------------------------- */

function maskBoxCreate() {
    const input = document.createElement("input");
    input.classList.add("e-input");
    input.style.width = "100%";
    return input;
}

function maskBoxWrite(args) {
    const ca = args.column?.customAttributes || {};
    const mask = ca["data-mask"] || "";
    const promptChar = ca["data-prompt"] || "_";

    if (args.element.ej2_instances?.[0]) {
        args.element.ej2_instances[0].destroy();
        args.element.ej2_instances = [];
    }

    const initialValue = args.value ?? args.rowData?.[args.column.field] ?? "";
    const masked = new ej.inputs.MaskedTextBox({
        mask: mask,
        promptChar: promptChar,
        value: initialValue
    });

    masked.appendTo(args.element);
    args.element.ej2_instances = [masked];
}

function maskBoxRead(args) {
    const instance = args?.element?.ej2_instances?.[0];
    if (!instance) return args.value ?? "";
    return instance.value ?? "";
}

function maskBoxDestroy(args) {
    const instance = args?.element?.ej2_instances?.[0];
    if (instance) {
        instance.destroy();
        args.element.ej2_instances = [];
    }
}

/* ---------------------------- Dropdown Bindings ---------------------------- */

function customDropDownCreate() {
    return document.createElement("input");
}

function customDropDownRead(args) {
    return args.ej2_instances[0].value;
}

function customDropDownWrite(args) {
    const fkName = args.column.field;
    const input = args.element;
    input.setAttribute("data-name", fkName);

    const isRequired = !!(args.column?.validationRules?.required);

    const gridElement = args.element.closest(".e-grid");
    const allData = getGridForeignKeyData(fkName, gridElement);
    const filteredData = allData.filter(x => (x.IsActive ?? true));

    const textField = args.column.foreignKeyValue;
    const valueField = args.column.foreignKeyField;

    const nullOption = { [textField]: "(None)", [valueField]: null };
    const dataSource = isRequired ? filteredData : [nullOption, ...filteredData];

    const dropdown = new ej.dropdowns.DropDownList({
        dataSource,
        fields: { text: textField, value: valueField },
        value: args.rowData[fkName] ?? null,
        floatLabelType: "Always",
        placeholder: fkName,
        showClearButton: !isRequired,
        change: function (e) {
            const gridCascadeMap = gridElement?.id ? (window[gridElement.id + "_CascadeMap"] || []) : [];
            const legacyCascadeMap = typeof cascadeMap !== "undefined" ? cascadeMap : [];
            const activeCascadeMap = gridCascadeMap.length ? gridCascadeMap : legacyCascadeMap;
            const pair = activeCascadeMap.find(p => equalsIgnoreCase(p.Source, fkName));
            if (!pair) return;

            const form = args.element.closest(".e-gridform") || args.element.closest("form");
            const targetInput = form?.querySelector(`input[name="${pair.Target}"], input[data-name="${pair.Target}"]`);
            const targetDropdown = targetInput?.ej2_instances?.[0];

            if (!targetDropdown) return;

            const fullData = getGridForeignKeyData(pair.Target, gridElement);
            const targetIsRequired = !!(targetDropdown?.validationRules?.required);
            const tText = targetDropdown?.fields?.text || pair.Target + "Name";
            const tValue = targetDropdown?.fields?.value || pair.Target;

            const tNullOption = { [tText]: "(None)", [tValue]: null };

            const next = (e.value == null)
                ? (targetIsRequired ? [] : [tNullOption])
                : fullData.filter(x =>
                    valuesEqual(readObjectValue(x, pair.Source), e.value) &&
                    readBooleanFlag(x, "IsActive", true) &&
                    readBooleanFlag(x, "IsApproved", true)
                );
            targetDropdown.dataSource = next;
            targetDropdown.value = null;
            targetDropdown.dataBind();
        }
    });

    dropdown.appendTo(input);

    if (filteredData.length === 1) {
        dropdown.value = filteredData[0][valueField];
        dropdown.dataBind();
    }
}

function getGridForeignKeyData(fieldName, gridElement) {
    const map = gridElement?.id ? (window[gridElement.id + "_FKDataSourceMap"] || {}) : {};
    const shortFieldName = String(fieldName || "").split(".").pop();
    const sourceField = map[fieldName] || map[shortFieldName] || fieldName;
    const scopedData = gridElement?.id ? window[gridElement.id + "_FK_" + sourceField] : null;

    if (Array.isArray(scopedData)) return scopedData;

    return window["FK_" + sourceField] || [];
}

function equalsIgnoreCase(left, right) {
    return String(left ?? "").toLowerCase() === String(right ?? "").toLowerCase();
}

function valuesEqual(left, right) {
    if (left == null || right == null) return left == right;
    return String(left) === String(right);
}

function readObjectValue(row, fieldName) {
    if (!row || !fieldName) return undefined;
    if (Object.prototype.hasOwnProperty.call(row, fieldName)) return row[fieldName];

    const lower = String(fieldName).toLowerCase();
    const key = Object.keys(row).find(k => k.toLowerCase() === lower);
    return key ? row[key] : undefined;
}

function readBooleanFlag(row, fieldName, defaultValue) {
    const value = readObjectValue(row, fieldName);
    if (value === undefined || value === null) return defaultValue;
    if (typeof value === "boolean") return value;
    if (typeof value === "number") return value !== 0;

    const normalized = String(value).trim().toLowerCase();
    if (["true", "1", "yes", "y"].includes(normalized)) return true;
    if (["false", "0", "no", "n"].includes(normalized)) return false;
    return defaultValue;
}

function readPathValueCaseInsensitive(source, path) {
    if (!source || !path) return undefined;
    const segments = String(path).split(".").filter(Boolean);
    let current = source;

    for (const segment of segments) {
        if (current == null || typeof current !== "object") return undefined;

        if (Object.prototype.hasOwnProperty.call(current, segment)) {
            current = current[segment];
            continue;
        }

        const lowerSegment = segment.toLowerCase();
        const matchedKey = Object.keys(current).find(k => k.toLowerCase() === lowerSegment);
        if (!matchedKey) return undefined;
        current = current[matchedKey];
    }

    return current;
}

function parseGridDialogDateValue(value) {
    function buildStrictDate(year, month, day) {
        if (!Number.isInteger(year) || !Number.isInteger(month) || !Number.isInteger(day)) return null;
        if (year < 1 || year > 9999 || month < 1 || month > 12 || day < 1 || day > 31) return null;

        const parsed = new Date(year, month - 1, day);
        if (Number.isNaN(parsed.getTime())) return null;
        if (parsed.getFullYear() !== year || (parsed.getMonth() + 1) !== month || parsed.getDate() !== day) return null;
        return parsed;
    }

    if (value == null) return null;

    if (value instanceof Date) {
        return Number.isNaN(value.getTime()) ? null : value;
    }

    if (typeof value === "number") {
        // JavaScript ms epoch.
        const asJsDate = new Date(value);
        if (!Number.isNaN(asJsDate.getTime())) return asJsDate;

        // .NET ticks fallback (100ns since 0001-01-01).
        if (value > 621355968000000000) {
            const ms = Math.floor((value - 621355968000000000) / 10000);
            const fromTicks = new Date(ms);
            return Number.isNaN(fromTicks.getTime()) ? null : fromTicks;
        }

        return null;
    }

    if (typeof value === "string") {
        const text = value.trim();
        if (!text) return null;

        const lower = text.toLowerCase();
        if (lower.includes("day") || lower.includes("month") || lower.includes("year")) return null;
        if (text.startsWith("0001-01-01") || text === "01-01-0001") return null;

        const iso = text.match(/^(\d{4})-(\d{1,2})-(\d{1,2})(?:T.*)?$/);
        if (iso) {
            const year = Number(iso[1]);
            const month = Number(iso[2]);
            const day = Number(iso[3]);
            return buildStrictDate(year, month, day);
        }

        const local = text.match(/^(\d{1,2})[-/.](\d{1,2})[-/.](\d{4})$/);
        if (local) {
            const day = Number(local[1]);
            const month = Number(local[2]);
            const year = Number(local[3]);
            return buildStrictDate(year, month, day);
        }

        // Accept compact values like 02022026 (ddMMyyyy) and 20260202 (yyyyMMdd).
        const compact = text.match(/^\d{8}$/);
        if (compact) {
            const digits = compact[0];

            const yyyy = Number(digits.slice(0, 4));
            const mm = Number(digits.slice(4, 6));
            const dd = Number(digits.slice(6, 8));
            const asYyyyMmDd = buildStrictDate(yyyy, mm, dd);
            if (asYyyyMmDd) return asYyyyMmDd;

            const day = Number(digits.slice(0, 2));
            const month = Number(digits.slice(2, 4));
            const year = Number(digits.slice(4, 8));
            const asDdMmYyyy = buildStrictDate(year, month, day);
            if (asDdMmYyyy) return asDdMmYyyy;
        }

        const nativeParsed = new Date(text);
        return Number.isNaN(nativeParsed.getTime()) ? null : nativeParsed;
    }

    if (typeof value === "object") {
        const year = value.year ?? value.Year;
        const month = value.month ?? value.Month;
        const day = value.day ?? value.Day;
        if (year && month && day) {
            const parsed = new Date(Number(year), Number(month) - 1, Number(day));
            return Number.isNaN(parsed.getTime()) ? null : parsed;
        }
    }

    return null;
}

function isGridDateColumn(column) {
    const type = String(column?.type ?? "").toLowerCase();
    const editType = String(column?.editType ?? "").toLowerCase();
    return type === "date" || type === "datetime" || type === "datetimepicker"
        || editType === "datepickeredit" || editType === "datetimepickeredit";
}

function getGridDateEditorInstance(field) {
    const instances = field?.ej2_instances;
    if (!Array.isArray(instances) || instances.length === 0) return null;

    return instances.find(instance => {
        const moduleName = instance?.getModuleName?.();
        return moduleName === "datepicker" || moduleName === "datetimepicker";
    }) || null;
}

function findGridEditorInput(form, fieldName) {
    if (!form || !fieldName) return null;
    const selectors = [
        `[name="${fieldName}"]`,
        `#${fieldName}`,
        `[id$="${fieldName}"]`
    ];

    const candidates = [];
    for (const selector of selectors) {
        const elements = Array.from(form.querySelectorAll(selector));
        elements.forEach(element => {
            if (!candidates.includes(element)) {
                candidates.push(element);
            }
        });
    }

    if (!candidates.length) return null;

    const withDateEditor = candidates.find(element => !!getGridDateEditorInstance(element));
    if (withDateEditor) return withDateEditor;

    const visibleInput = candidates.find(element => String(element?.type ?? "").toLowerCase() !== "hidden");
    return visibleInput || candidates[0] || null;
}

function syncGridDialogDateEditors(grid, args) {
    if (args?.requestType !== "beginEdit") return;

    const row = args.rowData || args.data;
    if (!row) return;

    const dialog = args.dialog;
    const form = args.form || dialog?.element?.querySelector("form.e-gridform");
    if (!form) return;

    const columns = grid?.getColumns?.() || grid?.columns || [];
    console.log("[GridDateDebug] beginEdit row", row);
    columns
        .filter(isGridDateColumn)
        .forEach(column => {
            const fieldName = column?.field;
            if (!fieldName) return;

            const input = findGridEditorInput(form, fieldName);
            const editor = getGridDateEditorInstance(input);
            if (!editor) {
                console.log("[GridDateDebug] no editor for field", fieldName, { inputFound: !!input });
                return;
            }

            const currentValue = editor.value;
            const currentParsed = parseGridDialogDateValue(currentValue);
            if (currentParsed) return;

            const rawValue = readPathValueCaseInsensitive(row, fieldName);
            const parsed = parseGridDialogDateValue(rawValue);
            console.log("[GridDateDebug] field", fieldName, {
                rawValue,
                currentValue,
                currentParsed,
                parsed
            });
            if (!parsed) return;

            try {
                editor.value = parsed;
                editor.dataBind?.();
            } catch (_) { /* keep dialog stable */ }
        });
}

function setPathValueCaseInsensitive(source, path, value) {
    if (!source || !path) return;

    const segments = String(path).split(".").filter(Boolean);
    if (!segments.length) return;

    let current = source;
    for (let i = 0; i < segments.length - 1; i++) {
        const segment = segments[i];
        const lowerSegment = segment.toLowerCase();
        const matchedKey = Object.keys(current).find(k => k.toLowerCase() === lowerSegment) || segment;

        if (current[matchedKey] == null || typeof current[matchedKey] !== "object") {
            current[matchedKey] = {};
        }
        current = current[matchedKey];
    }

    const lastSegment = segments[segments.length - 1];
    const lastLower = lastSegment.toLowerCase();
    const lastKey = Object.keys(current).find(k => k.toLowerCase() === lastLower) || lastSegment;
    current[lastKey] = value;
}

function formatDateForDateOnlyApi(date) {
    const yyyy = String(date.getFullYear());
    const mm = String(date.getMonth() + 1).padStart(2, "0");
    const dd = String(date.getDate()).padStart(2, "0");
    return `${yyyy}-${mm}-${dd}`;
}

function normalizeGridDatePayloadForApi(grid, payload, args) {
    if (!payload || typeof payload !== "object") return payload;

    const form = args?.form
        || args?.dialog?.element?.querySelector?.("form.e-gridform")
        || grid?.element?.querySelector?.("form.e-gridform")
        || document.querySelector(".e-dialog.e-edit-dialog form.e-gridform");

    const columns = grid?.getColumns?.() || grid?.columns || [];
    columns
        .filter(isGridDateColumn)
        .forEach(column => {
            const fieldName = column?.field;
            if (!fieldName) return;

            const rawValue = readPathValueCaseInsensitive(payload, fieldName);
            let parsed = parseGridDialogDateValue(rawValue);

            if (!parsed && form) {
                const input = findGridEditorInput(form, fieldName);
                const editor = getGridDateEditorInstance(input);
                const editorValue = editor?.value
                    ?? editor?.inputElement?.value
                    ?? editor?.element?.value
                    ?? input?.value
                    ?? null;
                parsed = parseGridDialogDateValue(editorValue);
            }

            if (!parsed) return;

            setPathValueCaseInsensitive(payload, fieldName, formatDateForDateOnlyApi(parsed));
        });

    return payload;
}

/* ---------------------------- AutoComplete Bindings ---------------------------- */

function customAutoCompleteCreate() {
    return document.createElement("input");
}

function customAutoCompleteRead(args) {
    const selectedId = args?.dataset?.autocompleteSelectedId;
    const idField = args?.dataset?.autocompleteIdField;
    const displayField = args?.dataset?.autocompleteDisplayField;

    // When an autocomplete is bound directly to the FK property
    // (for example FrameworkId), the editor must display DisplayText but
    // return the selected numeric key to the grid model.
    if (idField && displayField && idField === displayField && selectedId !== undefined)
        return selectedId === "" ? null : selectedId;

    return args.ej2_instances[0].value;
}

function customAutoCompleteWrite(args) {
    const nameField = args.column.field;
    const attrs = args.column.customAttributes || {};
    const idField = attrs["data-autocomplete-id-field"] || nameField.replace("Name", "Id");
    const valueField = attrs["data-autocomplete-value-field"] || "id";
    const textField = attrs["data-autocomplete-text-field"] || "display";
    const searchParameter = attrs["data-autocomplete-search-parameter"] || "searchText";
    const minLength = Number(attrs["data-autocomplete-min-length"] || 3);
    const suggestionCount = Number(attrs["data-autocomplete-suggestion-count"] || 20);
    const searchPlaceholder =
        attrs["data-autocomplete-placeholder"] || "Shkruaj për të kërkuar";
    const input = args.element;
    const dynamicUrl = attrs["data-autocomplete-url"];
    let cache = [];
    let lastPrefix = "";

    const read = (row, field) => {
        if (!row || !field) return null;
        return row[field] ?? row[field.charAt(0).toLowerCase() + field.slice(1)] ?? null;
    };

    const applySelection = item => {
        if (!item || !args.rowData) return;

        const selectedId = read(item, valueField);
        const selectedText = read(item, textField);

        input.dataset.autocompleteIdField = idField;
        input.dataset.autocompleteDisplayField = nameField;
        input.dataset.autocompleteSelectedId = selectedId ?? "";
        input.dataset.autocompleteSelectedText = selectedText ?? "";
        input.dataset.autocompleteCleared = "false";

        args.rowData[idField] = selectedId;

        // Do not overwrite the key with its label when both point to the
        // same model property. The AutoComplete instance still renders the
        // selected label in its input.
        if (nameField !== idField)
            args.rowData[nameField] = selectedText;
    };

    const autoComplete = new ej.dropdowns.AutoComplete({
        dataSource: [],
        fields: { text: textField, value: textField },
        placeholder: args.column.headerText || nameField,
        floatLabelType: "Always",
        highlight: true,
        minLength: minLength,
        suggestionCount: suggestionCount,
        value: args.rowData[nameField] || "",
        filtering: e => {
            e.preventDefaultAction = true;
            const text = (e.text || "").trim();

            if (!dynamicUrl || text.length < minLength) {
                e.updateData([]);
                return;
            }

            const prefix = text.substring(0, minLength).toLowerCase();
            if (prefix === lastPrefix) {
                const normalized = text.toLowerCase();
                const filtered = cache
                    .filter(x => String(read(x, textField) ?? "").toLowerCase().includes(normalized)
                        || String(read(x, valueField) ?? "").toLowerCase().includes(normalized))
                    .slice(0, suggestionCount);
                e.updateData(filtered);
                return;
            }

            lastPrefix = prefix;
            const separator = dynamicUrl.includes("?") ? "&" : "?";
            const url = `${dynamicUrl}${separator}${encodeURIComponent(searchParameter)}=${encodeURIComponent(text)}`;

            fetch(url, { headers: { Accept: "application/json" } })
                .then(r => r.ok ? r.json() : [])
                .then(data => {
                    cache = Array.isArray(data) ? data : [];
                    e.updateData(cache.slice(0, suggestionCount));
                })
                .catch(() => {
                    cache = [];
                    e.updateData([]);
                });
        },
        select: e => {
            applySelection(e.itemData);
        },
        change: e => {
            if (e.itemData) {
                applySelection(e.itemData);
            } else if (!autoComplete.value && args.rowData) {
                input.dataset.autocompleteIdField = idField;
                input.dataset.autocompleteDisplayField = nameField;
                input.dataset.autocompleteSelectedId = "";
                input.dataset.autocompleteSelectedText = "";
                input.dataset.autocompleteCleared = "true";
                args.rowData[idField] = null;
                args.rowData[nameField] = null;
            }
        }
    });

    autoComplete.appendTo(input);

    // Keep the field name as the floating label and show search guidance
    // inside the input only while the autocomplete has no value.
    input.setAttribute("placeholder", searchPlaceholder);
}

function handleForeignKeyAutoComplete(grid, args) {
    const row = Array.isArray(args.data) ? (args.data[0] ?? null) : args.data;
    if (!row) return;

    hydrateForeignKeyAutoCompletePayload(grid, args);

    grid.getColumns().forEach(column => {
        const isAutoComplete =
            column.customAttributes &&
            Object.keys(column.customAttributes).some(k => k.toLowerCase() === "data-autocomplete-url");

        if (!isAutoComplete) return;

        const textValue = row[column.field];
        if (!textValue) return;

        const attrs = column.customAttributes || {};
        const idField = attrs["data-autocomplete-id-field"] || column.field.replace("Name", "Id");
        const lookupUrl = attrs["data-autocomplete-url"];
        const valueField = attrs["data-autocomplete-value-field"] || "id";
        const textField = attrs["data-autocomplete-text-field"] || "display";
        const searchParameter = attrs["data-autocomplete-search-parameter"] || "searchText";
        if (!lookupUrl) return;

        const xhr = new XMLHttpRequest();
        const separator = lookupUrl.includes("?") ? "&" : "?";
        xhr.open("GET", `${lookupUrl}${separator}${encodeURIComponent(searchParameter)}=${encodeURIComponent(textValue)}`, false);
        xhr.send(null);

        if (xhr.status === 200) {
            const result = JSON.parse(xhr.responseText);
            if (Array.isArray(result) && result.length > 0) {
                const match = result[0];
                const read = (source, field) => source?.[field] ?? source?.[field.charAt(0).toLowerCase() + field.slice(1)] ?? null;
                row[idField] = read(match, valueField);
                row[column.field] = read(match, textField);
            }
        }
    });
}

function hydrateForeignKeyAutoCompletePayload(grid, args) {
    const row = Array.isArray(args.data) ? (args.data[0] ?? null) : args.data;
    if (!row) return;

    const form =
        args.form ||
        grid.element?.querySelector(".e-gridform") ||
        document.querySelector(".e-gridform");

    const parentFk = grid.element?.dataset?.parentForeignAttributeName;
    const parentTargetId = grid.element?.dataset?.parentTargetId;
    const parentTargetText = grid.element?.dataset?.parentTargetText;

    grid.getColumns().forEach(column => {
        const attrs = column.customAttributes || {};
        const isAutoComplete = Object.keys(attrs).some(k => k.toLowerCase() === "data-autocomplete-url");
        if (!isAutoComplete) return;

        const displayField = column.field;
        const idField = attrs["data-autocomplete-id-field"] || displayField.replace("Name", "Id");
        const input = form?.querySelector(`[data-autocomplete-id-field="${idField}"][data-autocomplete-display-field="${displayField}"]`);
        const wasCleared = input?.dataset?.autocompleteCleared === "true";

        if (input?.dataset) {
            if (Object.prototype.hasOwnProperty.call(input.dataset, "autocompleteSelectedId")) {
                const selectedId = input.dataset.autocompleteSelectedId;
                row[idField] = selectedId === "" ? null : selectedId;
            }

            if (Object.prototype.hasOwnProperty.call(input.dataset, "autocompleteSelectedText")) {
                row[displayField] = input.dataset.autocompleteSelectedText || null;
            }
        }

        if (!wasCleared
            && (row[idField] === undefined || row[idField] === null || row[idField] === "")
            && args.rowData
            && args.rowData[idField] !== undefined
            && args.rowData[idField] !== null
            && args.rowData[idField] !== "") {
            row[idField] = args.rowData[idField];
        }

        if (!wasCleared
            && (row[displayField] === undefined || row[displayField] === null || row[displayField] === "")
            && args.rowData
            && args.rowData[displayField] !== undefined
            && args.rowData[displayField] !== null
            && args.rowData[displayField] !== "") {
            row[displayField] = args.rowData[displayField];
        }

        if (parentFk
            && parentTargetId
            && equalsIgnoreCase(parentFk, idField)
            && (row[idField] === undefined || row[idField] === null || row[idField] === "")) {
            row[idField] = parentTargetId;
        }

        if (parentFk
            && parentTargetText
            && equalsIgnoreCase(parentFk, idField)
            && (row[displayField] === undefined || row[displayField] === null || row[displayField] === "")) {
            row[displayField] = parentTargetText;

            if (input?.dataset) {
                input.dataset.autocompleteSelectedId = row[idField] ?? "";
                input.dataset.autocompleteSelectedText = parentTargetText;
                input.dataset.autocompleteCleared = "false";
            }
        }
    });
}

function handleForeignKeyDropdowns(grid, args) {
    const row = Array.isArray(args.data) ? (args.data[0] ?? null) : args.data;
    if (!row) return;

    grid.getColumns().forEach(column => {
        if (column.foreignKeyField && column.editType === "dropdownedit") {
            if (row[column.field] !== null && row[column.field] !== undefined) {
                const ds = column.dataSource?.dataSource?.json || column.dataSource || [];
                const selectedRecord = ds.find(item => item[column.foreignKeyField] == row[column.field]);
                if (selectedRecord && column.foreignKeyValue && column.customAttributes?.["data-custom-foreign-column-name"]) {
                    row[column.customAttributes["data-custom-foreign-column-name"]] = selectedRecord[column.foreignKeyValue];
                }
            }
        }
    });
}

function updateChildDropdowns(grid, newRecord) {
    grid.getColumns().forEach(col => {
        if (col.foreignKeyField && col.editType === "dropdownedit" && col.customAttributes?.["data-is-child"] === "true") {
            const fkField = col.foreignKeyField;
            const fkValue = col.foreignKeyValue;
            const fkId = newRecord[fkField];
            const fkDisplay = newRecord[fkValue];

            if (fkId !== undefined && fkDisplay !== undefined) {
                if (!col.columnData) col.columnData = [];

                const combinedList = [...col.columnData];
                if (!combinedList.some(item => item[fkField] === fkId)) {
                    combinedList.push({ [fkField]: fkId, [fkValue]: fkDisplay });
                }

                (grid.dataSource || []).forEach(record => {
                    const id = record[fkField], name = record[fkValue];
                    if (!combinedList.some(item => item[fkField] === id) && id && name) {
                        combinedList.push({ [fkField]: id, [fkValue]: name });
                    }
                });

                col.columnData = combinedList;
                if (Array.isArray(col.edit?.params?.dataSource)) {
                    col.edit.params.dataSource = combinedList;
                }
            }
        }
    });
}

/* ---------------------------- File Upload Edit Bindings ---------------------------- */

function getColumnCustomAttributes(column) {
    return column?.customAttributes
        || column?.CustomAttributes
        || column?.customAttributes?.attributes
        || {};
}

function fileUploadCreate() {
    const wrapper = document.createElement("div");
    wrapper.className = "ds-file-editor";

    const fileInput = document.createElement("input");
    fileInput.type = "file";
    fileInput.className = "form-control file-uploader-wide";

    const hiddenInput = document.createElement("input");
    hiddenInput.type = "hidden";

    wrapper.appendChild(fileInput);
    wrapper.appendChild(hiddenInput);

    return wrapper;
}

function fileUploadWrite(args) {
    const wrapper = args.element;
    if (!wrapper) return;

    const fileInput = wrapper.querySelector("input[type='file']");
    const hiddenInput = wrapper.querySelector("input[type='hidden']");
    if (!fileInput || !hiddenInput) return;

    const ca = getColumnCustomAttributes(args.column);

    const fieldName = (ca["data-file-field"] || args.column?.field || "").toString();
    const transportField = (ca["data-transport-field"] || `${fieldName}Base64`).toString();
    const originalFileNameField = (ca["data-original-file-name-field"] || `${fieldName}OriginalFileName`).toString();
    const contentTypeField = (ca["data-content-type-field"] || `${fieldName}ContentType`).toString();

    fileInput.id = "Uploader_" + fieldName;
    fileInput.name = fieldName + "_File";
    fileInput.setAttribute("data-file-field", fieldName);
    fileInput.setAttribute("data-transport-field", transportField);
    fileInput.setAttribute("data-original-file-name-field", originalFileNameField);
    fileInput.setAttribute("data-content-type-field", contentTypeField);

    hiddenInput.name = transportField;
    hiddenInput.id = "hidden_" + transportField;
    hiddenInput.value = "";

    fileInput.value = "";
}

function fileUploadRead(args) {
    const wrapper = args?.element;
    const hiddenInput = wrapper?.querySelector("input[type='hidden']");
    return hiddenInput?.value || "";
}

function fileUploadDestroy() { }

/* ---------------------------- File Upload Helpers ---------------------------- */

function getFileInputsFromGrid(grid) {
    const root = grid?.element || document;

    return Array.from(
        root.querySelectorAll(
            ".e-editedrow input[type='file'], " +
            ".e-insertedrow input[type='file'], " +
            ".e-gridform input[type='file']"
        )
    );
}

function getFileFieldName(fileInput) {
    const dsField = (fileInput?.dataset?.fileField || "").trim();
    if (dsField) return dsField;

    const name = (fileInput?.name || "").trim();
    if (name) return name.replace(/_File$/i, "");

    const id = (fileInput?.id || "").trim();
    if (!id) return "";

    const uploaderPrefix = "Uploader_";
    const idx = id.indexOf(uploaderPrefix);
    if (idx >= 0) {
        return id.substring(idx + uploaderPrefix.length);
    }

    return "";
}

function getTransportFieldName(fileInput, fileField) {
    const dsField = (fileInput?.dataset?.transportField || "").trim();
    if (dsField) return dsField;
    return fileField ? `${fileField}Base64` : "";
}

function getOriginalFileNameFieldName(fileInput, fileField) {
    const dsField = (fileInput?.dataset?.originalFileNameField || "").trim();
    if (dsField) return dsField;
    return fileField ? `${fileField}OriginalFileName` : "";
}

function getContentTypeFieldName(fileInput, fileField) {
    const dsField = (fileInput?.dataset?.contentTypeField || "").trim();
    if (dsField) return dsField;
    return fileField ? `${fileField}ContentType` : "";
}

async function fileToDataUrl(file) {
    return await new Promise((resolve, reject) => {
        const reader = new FileReader();

        reader.onload = e => {
            const arrayBuffer = e.target.result;
            const bytes = new Uint8Array(arrayBuffer);
            let binary = "";
            for (let i = 0; i < bytes.length; i++) {
                binary += String.fromCharCode(bytes[i]);
            }

            const base64 = btoa(binary);
            const mime = file.type || "application/octet-stream";
            resolve(`data:${mime};base64,${base64}`);
        };

        reader.onerror = reject;
        reader.readAsArrayBuffer(file);
    });
}

async function attachFilesToData(grid, sourceData) {
    const data = JSON.parse(JSON.stringify(sourceData || {}));

    if (!data || typeof data !== "object") {
        console.log("attachFilesToData: invalid sourceData", sourceData);
        return data;
    }

    const fileInputs = getFileInputsFromGrid(grid);
    console.log("attachFilesToData: fileInputs found =", fileInputs.length, fileInputs);

    if (!fileInputs.length) return data;

    for (const fileInput of fileInputs) {
        if (!fileInput || !fileInput.files || fileInput.files.length === 0) {
            continue;
        }

        const file = fileInput.files[0];
        const fileField = getFileFieldName(fileInput);
        const transportField = getTransportFieldName(fileInput, fileField);
        const originalFileNameField = getOriginalFileNameFieldName(fileInput, fileField);
        const contentTypeField = getContentTypeFieldName(fileInput, fileField);

        if (!fileField || !transportField) {
            console.warn("attachFilesToData: unable to resolve mapping", {
                fileInput,
                fileField,
                transportField
            });
            continue;
        }

        const payload = await fileToDataUrl(file);

        data[transportField] = payload;

        if (originalFileNameField) {
            data[originalFileNameField] = file.name || null;
        }

        if (contentTypeField) {
            data[contentTypeField] = file.type || "application/octet-stream";
        }

        const hiddenInput = document.getElementById(`hidden_${transportField}`);
        if (hiddenInput) {
            hiddenInput.value = payload;
        }

        console.log("attachFilesToData: mapped file", {
            fileField,
            transportField,
            originalFileNameField,
            contentTypeField,
            fileName: file.name
        });
    }

    console.log("attachFilesToData: final payload =", JSON.parse(JSON.stringify(data)));
    return data;
}

/* ---------------------------- CRUD Operations ---------------------------- */

function isSaving(grid) { return !!grid.__saving; }
function setSaving(grid, val) { grid.__saving = !!val; }

async function performAdd(grid, args) {
    args.cancel = true;

    if (isSaving(grid)) return;
    setSaving(grid, true);

    const payload = normalizeGridDatePayloadForApi(grid, await attachFilesToData(grid, args.data), args);
    console.log("performAdd payload:", JSON.parse(JSON.stringify(payload)));

    const formSessionId = grid.element.dataset.formSessionId;

    const ajax = new ej.base.Ajax({
        url: "/Common/CRUD/Add",
        type: "POST",
        data: JSON.stringify(payload),
        contentType: "application/json",
        beforeSend: function (xhr) {
            xhr.httpRequest.setRequestHeader("FormSessionId", formSessionId);
        }
    });

    ajax.send().then(response => {
        const result = JSON.parse(response);

        if (result.success) {
            const newRecord = result.updatedRow;
            newRecord.IsApproved = result.updatedRow.IsApproved;

            grid.dataSource.unshift(newRecord);
            grid.refresh();
            updateChildDropdowns(grid, newRecord);
            showMessage(result.message, "Success", grid.element.id);
        } else {
            showMessage(result.message, "Error", grid.element.id);
        }

        setSaving(grid, false);
    }).catch(xhr => {
        showMessage(xhr.message, "Error", grid.element.id);
        setSaving(grid, false);
    });
}

async function performAddDialog(grid, args) {
    args.cancel = true;

    if (isSaving(grid)) return;
    setSaving(grid, true);

    const payload = normalizeGridDatePayloadForApi(grid, await attachFilesToData(grid, args.data), args);
    const formSessionId = grid.element.dataset.formSessionId;

    const ajax = new ej.base.Ajax({
        url: "/Common/CRUD/Add",
        type: "POST",
        data: JSON.stringify(payload),
        contentType: "application/json",
        beforeSend: function (xhr) {
            xhr.httpRequest.setRequestHeader("FormSessionId", formSessionId);
        }
    });

    ajax.send().then(response => {
        const result = JSON.parse(response);

        if (result.success) {
            const newRecord = result.updatedRow;

            // ✅ shumë e rëndësishme
            grid.closeEdit();   // mbyll dialogun pastër

            // ✅ shto record pa refresh brutal
            grid.dataSource = [newRecord, ...grid.dataSource];
            grid.dataBind();

            updateChildDropdowns(grid, newRecord);
            showMessage(result.message, "Success", grid.element.id);

        } else {
            // ❗ ERROR → mos prek grid
            // dialog mbetet open
            showMessage(result.message, "Error", grid.element.id);
        }

        setSaving(grid, false);
    }).catch(xhr => {
        showMessage(xhr.message, "Error", grid.element.id);
        setSaving(grid, false);
    });
}


async function performEdit(grid, args) {
    args.cancel = true;

    const formSessionId = grid.element.dataset.formSessionId;
    const payload = normalizeGridDatePayloadForApi(grid, await attachFilesToData(grid, args.data), args);

    console.log("performEdit payload:", JSON.parse(JSON.stringify(payload)));

    const ajax = new ej.base.Ajax({
        url: "/Common/CRUD/Update",
        type: "POST",
        data: JSON.stringify(payload),
        contentType: "application/json",
        beforeSend: function (xhr) {
            xhr.httpRequest.setRequestHeader("FormSessionId", formSessionId);
        }
    });

    ajax.send().then(response => {
        const result = JSON.parse(response);
        const rowIndex = args.rowIndex;

        grid.closeEdit();

        if (result.success) {
            grid.updateRow(rowIndex, result.updatedRow);
            showMessage(result.message, "Success", grid.element.id);
        } else {
            grid.refresh();
            showMessage(result.message, "Error", grid.element.id);
        }
    }).catch(xhr => {
        grid.refresh();
        showMessage(xhr.message, "Error", grid.element.id);
    });
}

function getKeyFields(grid) {
    return (grid.getColumns?.() || grid.columns || [])
        .filter(c => c.isPrimaryKey)
        .map(c => c.field);
}

function makeKey(rec, keyFields) {
    return keyFields.map(f => `${f}:${String(rec?.[f] ?? "")}`).join("|");
}

function findRowIndexByKey(grid, record) {
    const keyFields = getKeyFields(grid);
    const view = grid.getCurrentViewRecords?.() || [];
    const targetKey = makeKey(record, keyFields);
    return view.findIndex(r => makeKey(r, keyFields) === targetKey);
}

function performDelete(grid, args) {
    args.cancel = true;
    const formSessionId = grid.element.dataset.formSessionId;
    const payload = Array.isArray(args.data) ? args.data[0] : args.data;

    const ajax = new ej.base.Ajax({
        url: "/Common/CRUD/Remove",
        type: "POST",
        data: JSON.stringify(payload),
        contentType: "application/json",
        beforeSend: function (xhr) {
            xhr.httpRequest.setRequestHeader("FormSessionId", formSessionId);
        }
    });

    ajax.send().then(response => {
        const result = JSON.parse(response);
        args.data = result.updatedRow;
        const rowIndex = grid.getSelectedRowIndexes()[0];

        if (result.success) {
            grid.updateRow(rowIndex, args.data);
            showMessage(result.message, "Success", grid.element.id);
        } else {
            grid.refresh();
            showMessage(result.message, "Error", grid.element.id);
        }
    }).catch(xhr => {
        grid.refresh();
        showMessage(xhr.message, "Error", grid.element.id);
    });
}

/* ---------------------------- Grid Event Handlers ---------------------------- */

function actionBegin(args) {
    const grid = this;
    showAllColumns(grid, args.requestType);
    hideMessages(this);

    const gridName = grid.element.id;
    const hasAddButton = grid.toolbar?.some(t => t === "Add" || t.id === `${gridName}_add`);
    const hasEditButton = grid.toolbar?.some(t => t === "Edit" || t.id === `${gridName}_edit`);
    const usesPartialDialogEditor = grid.element?.dataset?.editorMode === "PartialDialog";
    const partialAddPath = grid.element?.dataset?.editorPartialAddPath || grid.element?.dataset?.editorPartialViewPath || "";
    const partialEditPath = grid.element?.dataset?.editorPartialEditPath || grid.element?.dataset?.editorPartialViewPath || "";

    if (args.requestType === "add") {
        hydrateParentForeignKeyForAdd(grid, args);
    }

    // Optional template editor: detail grids can keep generic data loading/CRUD,
    // but replace Syncfusion inline/dialog editing with a custom partial view dialog.
    if (usesPartialDialogEditor && partialAddPath && args.requestType === "add") {
        args.cancel = true;
        if (!hasAddButton) {
            return;
        }
        window.openGridPartialDialogEditor?.(grid, "add", {}, -1);
        return;
    }

    if (usesPartialDialogEditor && partialEditPath && args.requestType === "beginEdit") {
        args.cancel = true;
        if (!hasEditButton) {
            return;
        }
        window.openGridPartialDialogEditor?.(grid, "edit", args.rowData || args.data || {}, args.rowIndex);
        return;
    }

    if (args.requestType === "beginEdit" && !hasEditButton) {
        args.cancel = true;
        return;
    }

    if (args.requestType === "save") {
        handleForeignKeyAutoComplete(grid, args);
        handleForeignKeyDropdowns(grid, args);
        const isDialog = grid.editSettings?.mode === "Dialog";

        if (args.action === "add") {
            //performAdd(grid, args);

            if (isDialog) {
                performAddDialog(grid, args);   // ✅ e re
                console.log("performAddDialog");
            } else {
                performAdd(grid, args);         // ✅ ekzistuese
                console.log("performAdd");
            }

        } else {
            performEdit(grid, args);
        }
    } else if (args.requestType === "delete") {
        handleForeignKeyAutoComplete(grid, args);
        handleForeignKeyDropdowns(grid, args);
        performDelete(grid, args);
    }
}

function hydrateParentForeignKeyForAdd(grid, args) {
    const parentFk = grid.element?.dataset?.parentForeignAttributeName;
    const parentTargetId = grid.element?.dataset?.parentTargetId;
    const parentTargetText = grid.element?.dataset?.parentTargetText;

    if (!parentFk || !parentTargetId) return;

    const row = args.data || args.rowData || {};
    args.data = row;

    if (row[parentFk] === undefined || row[parentFk] === null || row[parentFk] === "") {
        row[parentFk] = parentTargetId;
    }

    grid.getColumns().forEach(column => {
        const attrs = column.customAttributes || {};
        const idField = attrs["data-autocomplete-id-field"];
        const displayField = attrs["data-autocomplete-display-field"] || column.field;
        const isParentAutoComplete =
            attrs["data-is-parent-foreign-key"] === "true"
            || (idField && equalsIgnoreCase(idField, parentFk));

        if (!isParentAutoComplete) return;

        if (idField && (row[idField] === undefined || row[idField] === null || row[idField] === "")) {
            row[idField] = parentTargetId;
        }

        if (parentTargetText
            && displayField
            && (row[displayField] === undefined || row[displayField] === null || row[displayField] === "")) {
            row[displayField] = parentTargetText;
        }
    });
}

/* ---------------------------- Dialog Layouts ---------------------------- */

function readGridDialogLayout(grid) {
    const raw = grid.element?.dataset?.editorDialogLayout;
    if (!raw || !raw.trim().startsWith("{")) return null;

    try {
        const config = JSON.parse(raw);
        return config.editorDialog || config.dialog || config;
    } catch (error) {
        console.warn("Invalid FormUIConfiguration JSON.", error);
        return null;
    }
}

function customTextAreaCreate() {
    const element = document.createElement("textarea");
    element.className = "e-input e-field";
    element.rows = 4;
    element.style.resize = "vertical";
    element.style.minHeight = "96px";
    return element;
}

function customTextAreaWrite(args) {
    const fieldName = args.column.field;
    args.element.id = fieldName;
    args.element.setAttribute("name", fieldName);
    args.element.setAttribute("aria-label", args.column.headerText || fieldName);
    args.element.placeholder = args.column.headerText || fieldName;
    args.element.value = args.rowData?.[fieldName] ?? "";
}

function customTextAreaRead(element) {
    return element?.value ?? "";
}

function customTextAreaDestroy() {
}

function getGridMenuMetadata(grid) {
    const formId = Number(grid.element?.dataset?.formId || 0);
    if (!formId) return null;

    try {
        const menuData = JSON.parse(localStorage.getItem("formHierarchy") || "[]");
        if (!Array.isArray(menuData)) return null;

        return menuData.find(item => Number(item?.FormId ?? item?.formId ?? 0) === formId) || null;
    } catch (error) {
        console.warn("Invalid formHierarchy menu data.", error);
        return null;
    }
}

function findGridEditorFieldRow(form, fieldName) {
    const candidates = [
        `[name="${fieldName}"]`,
        `#${fieldName}`,
        `[id$="${fieldName}"]`
    ];

    for (const selector of candidates) {
        const input = form.querySelector(selector);
        const row = input?.closest("tr");
        if (row) return row;
    }

    return null;
}

function applyGridEditorFieldLabel(field, labelText) {
    if (!labelText) return;

    const labels = field.querySelectorAll("label, .e-float-text");
    if (labels.length === 0) {
        const label = document.createElement("label");
        label.className = "form-label";
        label.textContent = labelText;
        field.prepend(label);
        return;
    }

    labels.forEach(label => {
        label.textContent = labelText;
    });
}

function getGridDialogParentContextText(grid) {
    const parentFk = grid.element?.dataset?.parentForeignAttributeName;
    const parentValue = grid.element?.dataset?.parentTargetId;
    if (!parentFk || !parentValue) return "";

    const rows = getGridForeignKeyData(parentFk, grid.element);
    const row = Array.isArray(rows)
        ? rows.find(x => String(x?.[parentFk] ?? x?.Value ?? x?.value ?? "") === String(parentValue))
        : null;

    if (!row) return "";

    const labelFields = [
        parentFk.replace(/Id$/i, "Name"),
        "FrameworkTitle",
        "FrameworkName",
        "Title",
        "Name",
        "Text",
        "text"
    ];

    for (const field of labelFields) {
        if (row[field]) return String(row[field]);
    }

    const values = Object.values(row).filter(v => v !== null && v !== undefined && String(v).trim() !== "");
    return values.length > 1 ? String(values[1]) : String(values[0] || "");
}

function applyGenericGridDialogLayout(grid, args) {
    const layout = readGridDialogLayout(grid);
    if (!layout) return;

    const dlg = args.dialog;
    const form = args.form || (dlg && dlg.element.querySelector("form.e-gridform"));
    if (!dlg || !form || form.dataset.dsLayoutApplied === "true") return;

    form.dataset.dsLayoutApplied = "true";
    dlg.width = layout.width || "900px";
    const menuMetadata = getGridMenuMetadata(grid);
    const dialogTitle = menuMetadata?.FormTitle
        || menuMetadata?.formTitle
        || grid.element?.dataset?.editorDialogTitle
        || layout.title
        || "Details";
    const dialogDescription = menuMetadata?.FormDescription
        || menuMetadata?.formDescription
        || grid.element?.dataset?.editorDialogDescription
        || layout.intro?.text
        || "";

    dlg.header = dialogTitle;

    const sourceTable = form.querySelector("table.e-table");
    if (!sourceTable) return;

    const shell = document.createElement("div");
    shell.className = "ds-grid-editor-layout";

    const usedRows = new Set();
    const labels = layout.labels || {};

    if (layout.intro) {
        const intro = document.createElement("div");
        intro.className = "ds-grid-editor-intro";

        const context = document.createElement("div");
        context.className = "ds-grid-editor-context";

        // FormUIConfiguration may promote a generated Syncfusion FK editor into the intro.
        const contextFieldName = layout.contextField || grid.element?.dataset?.parentForeignAttributeName;
        const contextRow = contextFieldName ? findGridEditorFieldRow(form, contextFieldName) : null;
        if (contextRow) {
            usedRows.add(contextRow);
            const contextField = document.createElement("div");
            contextField.className = "ds-grid-editor-field";
            contextField.dataset.field = contextFieldName;
            contextField.appendChild(contextRow);
            applyGridEditorFieldLabel(contextField, labels[contextFieldName]);
            context.appendChild(contextField);
        } else {
            const title = document.createElement("div");
            title.className = "fw-semibold";
            title.textContent = getGridDialogParentContextText(grid) || dialogTitle;
            context.append(title);
        }

        const meta = document.createElement("div");
        meta.className = "ds-grid-editor-meta";
        const badge = document.createElement("span");
        badge.className = "badge bg-light text-dark border";
        badge.textContent = layout.intro.badge || "Generic CRUD dialog";

        const text = document.createElement("div");
        text.className = "text-muted small";
        text.textContent = dialogDescription;

        meta.append(badge, text);
        intro.append(context, meta);
        shell.appendChild(intro);
    }

    layout.sections.forEach(section => {
        const card = document.createElement("div");
        card.className = "ds-grid-editor-card";

        const header = document.createElement("div");
        header.className = "ds-grid-editor-card-header";
        header.innerHTML = `<i class="${section.icon || "bi bi-ui-checks"} me-1"></i>${section.title || ""}`;

        const body = document.createElement("div");
        body.className = "ds-grid-editor-card-body";
        body.style.setProperty("--ds-dialog-columns", section.columns || 2);

        section.fields.forEach(fieldName => {
            const row = findGridEditorFieldRow(form, fieldName);
            if (!row) return;

            usedRows.add(row);
            const field = document.createElement("div");
            field.className = "ds-grid-editor-field";
            field.dataset.field = fieldName;
            field.appendChild(row);
            applyGridEditorFieldLabel(field, labels[fieldName]);
            body.appendChild(field);
        });

        if (section.note) {
            const note = document.createElement("div");
            note.className = "ds-grid-editor-note text-muted small";
            note.textContent = section.note;
            body.appendChild(note);
        }

        if (body.children.length > 0) {
            card.append(header, body);
            shell.appendChild(card);
        }
    });

    const remainingRows = Array.from(sourceTable.querySelectorAll("tr"))
        .filter(row => !usedRows.has(row) && row.querySelector("input,select,textarea"));

    if (remainingRows.length > 0) {
        const hidden = document.createElement("div");
        hidden.className = "d-none";
        remainingRows.forEach(row => hidden.appendChild(row));
        shell.appendChild(hidden);
    }

    sourceTable.replaceWith(shell);
}

function resolveGridDialogFormValidator(grid, form) {
    if (grid?.editModule?.formObj) return grid.editModule.formObj;
    if (!Array.isArray(form?.ej2_instances)) return null;

    return form.ej2_instances.find(instance => instance?.getModuleName?.() === "formvalidator") || null;
}

function findGridEditorFieldContainer(form, inputElement, explicitFieldName) {
    const direct = inputElement?.closest?.(".ds-grid-editor-field");
    if (direct) return direct;

    const fieldName = explicitFieldName
        || inputElement?.getAttribute?.("name")
        || inputElement?.name
        || inputElement?.id
        || "";
    if (!fieldName) return null;

    const candidates = Array.from(form.querySelectorAll(".ds-grid-editor-field[data-field]"));
    return candidates.find(field => equalsIgnoreCase(field.dataset?.field, fieldName)) || null;
}

function resolveGridDialogEditorAnchor(form, fieldName, inputElement) {
    const direct = (inputElement?.type || "").toLowerCase() !== "hidden"
        ? inputElement
        : null;
    if (direct) return direct;

    const byField = findGridEditorInput(form, fieldName);
    if (byField) return byField;

    const container = findGridEditorFieldContainer(form, inputElement, fieldName);
    if (!container) return inputElement || null;

    const visibleInput = container.querySelector("input:not([type='hidden']), textarea, select, .e-input");
    if (visibleInput) return visibleInput;

    return inputElement || null;
}

function placeGridDialogValidationTooltip(form, fieldName, inputElement, errorElement) {
    const fieldContainer = findGridEditorFieldContainer(form, inputElement, fieldName);
    const anchor = resolveGridDialogEditorAnchor(form, fieldName, inputElement);
    const tooltip = document.getElementById(`${fieldName}_Error`)
        || errorElement?.closest?.(".e-tooltip-wrap")
        || errorElement;
    if (!tooltip) return;

    if (fieldContainer && !fieldContainer.contains(tooltip)) {
        fieldContainer.appendChild(tooltip);
    } else if (!fieldContainer && tooltip.parentElement !== form) {
        form.appendChild(tooltip);
    }

    if (!anchor?.getBoundingClientRect || !tooltip?.style) return;

    if (fieldContainer) {
        fieldContainer.style.position = fieldContainer.style.position || "relative";
    } else {
        form.style.position = form.style.position || "relative";
    }

    const inputRect = anchor.getBoundingClientRect();
    const hostRect = (fieldContainer || form).getBoundingClientRect();
    tooltip.style.position = "absolute";
    tooltip.style.left = `${Math.max(0, inputRect.left - hostRect.left)}px`;
    tooltip.style.top = `${Math.max(0, inputRect.bottom - hostRect.top + 6)}px`;
    tooltip.style.right = "auto";
    tooltip.style.bottom = "auto";
    tooltip.style.transform = "none";
}

function observeGridDialogValidationTooltips(form) {
    if (!form || form.__dsValidationObserverBound) return;
    form.__dsValidationObserverBound = true;

    const observer = new MutationObserver(function () {
        const tooltips = Array.from(form.querySelectorAll("[id$='_Error']"));
        tooltips.forEach(tooltip => {
            const fieldName = tooltip.id?.replace(/_Error$/i, "") || "";
            if (!fieldName) return;
            const inputElement = findGridEditorInput(form, fieldName) || form.querySelector(`[name="${fieldName}"]`);
            placeGridDialogValidationTooltip(form, fieldName, inputElement, tooltip);
        });
    });

    observer.observe(form, {
        childList: true,
        subtree: true
    });
}

function wireGridDialogValidationPlacement(grid, args) {
    const form = args?.form || args?.dialog?.element?.querySelector?.("form.e-gridform");
    if (!form || form.dataset.dsValidationPlacementBound === "true") return;

    observeGridDialogValidationTooltips(form);
    const formValidator = resolveGridDialogFormValidator(grid, form);
    if (!formValidator) return;

    form.dataset.dsValidationPlacementBound = "true";
    const defaultPlacement = ej?.grids?.getObject?.("valErrorPlacement", grid?.editModule)?.bind?.(grid?.editModule) || null;
    formValidator.customPlacement = function (inputElement, errorElement) {
        if (!errorElement) return;

        if (typeof defaultPlacement === "function") {
            try { defaultPlacement(inputElement, errorElement); } catch (_) { }
        }

        const fieldName = inputElement?.getAttribute?.("name")
            || inputElement?.name
            || errorElement?.id?.replace(/_Error$/i, "")
            || "";
        placeGridDialogValidationTooltip(form, fieldName, inputElement, errorElement);
    };

}

function actionComplete(args) {
    const grid = this;

    if (args.requestType === "save") {
        handleForeignKeyAutoComplete(grid, args);
    }

    if (args.requestType === "beginEdit" || args.requestType === "add") {
        const dlg = args.dialog;
        const form = args.form || (dlg && dlg.element.querySelector("form.e-gridform"));
        if (!dlg || !form) return;

        applyGenericGridDialogLayout(grid, args);
        wireGridDialogValidationPlacement(grid, args);
        setTimeout(() => wireGridDialogValidationPlacement(grid, args), 0);
        setTimeout(() => wireGridDialogValidationPlacement(grid, args), 120);
        syncGridDialogDateEditors(grid, args);
        setTimeout(() => syncGridDialogDateEditors(grid, args), 0);
        setTimeout(() => syncGridDialogDateEditors(grid, args), 120);

        dlg.element.classList.add("ds-editdlg-2col");
        dlg.width = "900px";

        const table = form.querySelector("table.e-table");
        if (table) table.classList.add("ds-two-col");
    }
}

/* ---------------------------- Toolbar / Export ---------------------------- */

function isExporting(grid) { return !!grid.__pdfExporting; }
function setExporting(grid, val) { grid.__pdfExporting = !!val; }

function wirePdfExportGuards(grid) {
    grid.pdfExportComplete = () => setExporting(grid, false);
    grid.pdfExportFailure = () => setExporting(grid, false);
}

window.reloadGenericGrid = async function (gridOrName) {
    const grid = typeof gridOrName === "string"
        ? document.getElementById(gridOrName)?.ej2_instances?.[0]
        : gridOrName;

    if (!grid?.element) {
        return false;
    }

    // Remote grids already own a DataManager configured with the generic paged endpoint.
    // Refreshing it issues a new read while preserving paging/filtering/sorting state.
    if (typeof ej !== "undefined" && grid.dataSource instanceof ej.data.DataManager) {
        grid.refresh();
        return true;
    }

    const formSessionId =
        grid.element.dataset.formSessionId ||
        grid.element.getAttribute("data-form-session-id") ||
        "";

    if (!formSessionId) {
        throw new Error("Missing FormSessionId for grid refresh.");
    }

    grid.showSpinner?.();
    try {
        const response = await fetch("/Common/CRUD/RefreshGridData", {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "FormSessionId": formSessionId
            },
            body: JSON.stringify({
                contextFormType: Number(grid.element.dataset.contextFormType || 0),
                foreignAttributeName: grid.element.dataset.parentForeignAttributeName || null,
                targetId: grid.element.dataset.parentTargetId || null
            })
        });

        const result = await response.json();
        if (!response.ok || !result.success) {
            throw new Error(result.message || "Grid refresh failed.");
        }

        grid.dataSource = result.data || [];
        grid.dataBind();
        return true;
    } finally {
        grid.hideSpinner?.();
    }
};

function toolbarClick(args) {
    const grid = this;
    const gridName = grid.element.id;
    hideMessages(`${gridName}_MSG`);

    switch (args.item.id) {
        case "Grid_Mode":
            switchToMode(grid, gridName);
            break;
        case "Grid_Dialog":
            switchToDialogMode(grid, gridName);
            break;
        case `${gridName}_pdfexport`:
            if (isExporting(grid)) return;
            setExporting(grid, true);
            applyExportFormat(grid, "pdf");
            preloadExportImages(grid)
                .then(() => {
                    const exportDataSource = buildPdfExportDataSource(grid);
                    if (exportDataSource) {
                        grid.pdfExport({ dataSource: exportDataSource });
                    } else {
                        grid.pdfExport();
                    }
                })
                .catch(err => {
                    setExporting(grid, false);
                    console.error("PDF export failed:", err);
                    showMessage("PDF export failed. Check console for details.", "Error", grid.element.id);
                });
            break;
        case `${gridName}_excelexport`:
            applyExportFormat(grid, "excel");
            preloadExportImages(grid).then(() => {
                grid.excelExport({ includeTemplateColumn: true });
            });
            break;
        case `${gridName}_csvexport`:
            grid.csvExport();
            break;
        case "ApproveForm":
            handleApproveForm(grid);
            break;
        case "Publish":
            ajaxPublish(grid);
            break;
        case "ToggleFilter":
            toggleFilter(grid);
            break;
        case "Open360":
            openEntity360FromToolbar(grid);
            break;
        case "RefreshGrid":
            window.reloadGenericGrid(grid).catch(err => {
                console.error("Grid refresh failed:", err);
                showMessage(err.message || "Grid refresh failed.", "Error", gridName);
            });
            break;
        case "small":
            this.rowHeight = 20;
            break;
        case "medium":
            this.rowHeight = 40;
            break;
        case "big":
            this.rowHeight = 60;
            break;
    }
}

function switchToMode(grid) {
    grid.editSettings.mode = grid.editSettings.mode === "Normal" ? "Dialog" : "Normal";
    grid.refresh();
}

function switchToDialogMode(grid) {
    grid.editSettings.mode = "Dialog";
    grid.refresh();
}

function switchToInlineMode(grid) {
    grid.editSettings.mode = "Normal";
    grid.refresh();
}

/* ---------------- EXPORT HELPERS (Image + Email) ---------------- */

const __imgBase64Cache = new Map();

function __getColCA(col) {
    return col?.customAttributes
        || col?.CustomAttributes
        || col?.customAttributes?.attributes
        || {};
}

function __isImageExportColumn(col) {
    const ca = __getColCA(col);
    const flag = ca["data-export-image"] ?? ca["data-export-image".toLowerCase()];
    return (flag?.toString() === "1" || flag?.toString().toLowerCase() === "true");
}

function __resolveImageSrcFromRow(col, row) {
    const ca = __getColCA(col);
    const fieldsCsv = (ca["data-img-fields"] ?? "").toString();
    const defaultImg = (ca["data-img-default"] ?? "/images/profilePictures/user.png").toString();

    const fields = fieldsCsv.split(",").map(x => x.trim()).filter(Boolean);

    let p = "";
    for (const f of fields) {
        const v = row?.[f];
        if (typeof v === "string" && v.trim()) {
            p = v.trim();
            break;
        }
    }

    return p ? (p.startsWith("/") ? p : "/" + p) : defaultImg;
}

async function __toBase64(url) {
    if (__imgBase64Cache.has(url)) return __imgBase64Cache.get(url);

    const res = await fetch(url, { credentials: "same-origin" });
    if (!res.ok) throw new Error("Image fetch failed: " + res.status);

    const blob = await res.blob();
    const base64 = await new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve((reader.result || "").toString().split(",")[1]);
        reader.onerror = reject;
        reader.readAsDataURL(blob);
    });

    __imgBase64Cache.set(url, base64);
    return base64;
}

async function preloadExportImages(grid) {
    const cols = grid.getColumns();
    const imgCols = cols.filter(c => __isImageExportColumn(c));
    if (imgCols.length === 0) return;

    const rows = grid.getCurrentViewRecords?.() || [];
    const urls = new Set();

    for (const r of rows) {
        for (const c of imgCols) {
            urls.add(__resolveImageSrcFromRow(c, r));
        }
    }

    await Promise.all([...urls].map(async (u) => {
        if (__imgBase64Cache.has(u)) return;
        try {
            const b64jpg = await __toJpegBase64(u);
            __imgBase64Cache.set(u, b64jpg);
        } catch (e) {
            console.warn("Preload failed for:", u, e);
        }
    }));
}

async function __toJpegBase64(url) {
    const res = await fetch(url, { credentials: "same-origin" });
    if (!res.ok) throw new Error("Image fetch failed: " + res.status);

    const blob = await res.blob();
    const img = await createImageBitmap(blob);

    const canvas = document.createElement("canvas");
    canvas.width = img.width;
    canvas.height = img.height;
    const ctx = canvas.getContext("2d");
    ctx.drawImage(img, 0, 0);

    const jpegDataUrl = canvas.toDataURL("image/jpeg", 0.92);
    return jpegDataUrl.split(",")[1];
}

function exportQueryCellInfo(args) {
    const col = args?.column;
    const row = args?.data || {};

    if (__isImageExportColumn(col)) {
        const src = __resolveImageSrcFromRow(col, row);
        const b64 = __imgBase64Cache.get(src);

        if (b64) {
            args.image = { base64: b64, height: 36, width: 36 };
            args.value = "";
        } else {
            args.value = "";
        }
        return;
    }

    const ht = (col?.headerText || "").toString().toLowerCase();
    if (ht === "email" || ht === "email id" || ht === "emailid") {
        const email = (row["Email"] || row["EmailID"] || row["EmailId"] || "").toString().trim();
        if (email) args.hyperLink = { target: "mailto:" + email, displayText: email };
    }
}

function applyExportFormat(grid, formatType) {
    grid.columns.forEach(column => {
        if (column.type === "datetime") {
            column.format = (formatType === "pdf") ? "long" : "yyyy.MM.dd HH:mm:ss";
        }
    });
}

function cloneForPdfExport(record) {
    try {
        if (typeof structuredClone === "function") {
            return structuredClone(record);
        }
    } catch (_) { /* fallback below */ }

    try {
        return JSON.parse(JSON.stringify(record));
    } catch (_) {
        return null;
    }
}

function getPdfDateColumnFields(grid) {
    const columns = grid?.getColumns?.() || grid?.columns || [];
    return columns
        .filter(column => {
            const type = String(column?.type ?? "").toLowerCase();
            return type === "date" || type === "datetime" || type === "datetimepicker";
        })
        .map(column => column?.field)
        .filter(Boolean);
}

function buildPdfExportDataSource(grid) {
    const dateFields = getPdfDateColumnFields(grid);
    if (!dateFields.length) return null;

    const sourceRows = grid?.getCurrentViewRecords?.() || [];
    if (!Array.isArray(sourceRows) || sourceRows.length === 0) return null;

    let hasConversion = false;
    const convertedRows = sourceRows.map(row => {
        if (row == null || typeof row !== "object") return row;

        let cloned = null;

        for (const fieldName of dateFields) {
            const rawValue = readPathValueCaseInsensitive(row, fieldName);
            if (rawValue == null || rawValue === "") continue;

            const parsedDate = parseGridDialogDateValue(rawValue);
            if (!parsedDate) continue;

            if (rawValue instanceof Date && !Number.isNaN(rawValue.getTime())) continue;

            if (!cloned) {
                cloned = cloneForPdfExport(row) || { ...row };
            }

            setPathValueCaseInsensitive(cloned, fieldName, parsedDate);
            hasConversion = true;
        }

        return cloned || row;
    });

    return hasConversion ? convertedRows : null;
}

function toggleFilter() {
    const filterSection = document.getElementById("filterSection");
    if (filterSection) {
        new bootstrap.Collapse(filterSection, { toggle: true });
    }
}

/* ---------------- Approval / Publish / Navigation ---------------- */

function handleApproveForm(grid) {
    const selected = grid.getSelectedRecords();
    if (selected.length === 0) {
        showMessage("Please select a row before approving the form.", "Error", grid.element.id);
        return;
    }

    const record = selected[0];
    if (record.IsApproved != null) {
        showMessage("This form has already been approved.", "Error", grid.element.id);
        return;
    }

    const formSessionId = grid.element.dataset.formSessionId;
    let primaryKey = getGridPrimaryKeyValue(grid, record);

    const isSDLT = grid.element.dataset.formIsSdlt ? grid.element.dataset.formIsSdlt === "true" : false;
    let formId = null;

    if (isSDLT) {
        const selectedRows = grid.getSelectedRecords();
        if (selectedRows.length > 0) {
            formId = selectedRows[0].FormId;
            primaryKey = selectedRows[0].TargetId;
        }
    }

    $.ajax({
        url: "/Common/SDLT/SDLTReviewForm",
        type: "POST",
        data: { targetId: primaryKey, formId: formId },
        beforeSend: function (xhr) {
            xhr.setRequestHeader("FormSessionId", formSessionId);
        },
        success: function (response) {
            $("#dialogContent").html(response);

            const dialog = document.getElementById("ApprovalDialog").ej2_instances[0];

            dialog.header = `
                <div class="d-flex align-items-center justify-content-between w-100">
                    <div>
                        <div class="fw-bold">Review Changes</div>
                    </div>
                </div>
            `;

            dialog.content = document.getElementById("dialogContent");
            dialog.dataBind();
            dialog.show();

            dialog.close = function () {
                const message = dialog.element.getAttribute("data-message");
                const isApproved = dialog.element.getAttribute("data-isApproved");
                const isDeleted = dialog.element.getAttribute("data-isDeleted");

                const rowIndex = grid.getSelectedRowIndexes()[0];
                const rowData = grid.getCurrentViewRecords()[rowIndex];

                rowData.IsApproved = JSON.parse(isApproved);

                if (isDeleted === "true" && isApproved === "true") {
                    grid.dataSource.splice(rowIndex, 1);
                } else {
                    grid.updateRow(rowIndex, rowData);
                }

                grid.refresh();
                showMessage(message, "Success", grid.element.id);
            };
        },
        error: function (xhr) {
            showMessage(xhr.responseText, "Error", grid.element.id);
        }
    });
}

function getGridPrimaryKeyValue(grid, record) {
    const keyColumns = grid.getColumns().filter(col => col.isPrimaryKey);
    return keyColumns.map(col => record[col.field]).join("|");
}

function ajaxPublish(grid) {
    const formSessionId = grid.element.dataset.formSessionId;

    const ajax = new ej.base.Ajax({
        url: "/Common/CRUD/Publish",
        type: "POST",
        contentType: "application/json",
        beforeSend: function (xhr) {
            xhr.httpRequest.setRequestHeader("FormSessionId", formSessionId);
        }
    });

    ajax.send().then(response => {
        const result = JSON.parse(response);
        result.success
            ? showMessage(result.message, "Success", grid.element.id)
            : showMessage(result.message, "Error", grid.element.id);
    }).catch(xhr => {
        showMessage(xhr.message, "Error", grid.element.id);
    });
}

function openEntity360FromToolbar(grid) {
    const gridEl = grid.element;
    const formConfigId = gridEl.dataset.formConfigId;
    if (!formConfigId) {
        showMessage("Form configuration is missing.", "Error");
        return;
    }

    const selected = grid.getSelectedRecords();
    if (selected.length === 0) {
        showMessage("Please select a row.", "Error");
        return;
    }

    const pk = getGridPrimaryKeyValue(grid, selected[0]);
    const url = `/Common/Entity360/Index?id=${encodeURIComponent(formConfigId)}&entityPrimaryKeyValue=${encodeURIComponent(pk)}`;
    window.location.href = url;
}

/* ---------------- Context Menu ---------------- */

function contextMenuClick(args) {
    const grid = this;
    hideMessages(`${grid.element.id}_MSG`);

    switch (args.item.id) {
        case "Copy":
            grid.copy();
            break;
        case "copyWithHeader":
            grid.copy(true);
            break;
        case "FreezeColumn": {
            const colIndex = args.column.index;
            grid.frozenColumns = colIndex + 1;
            grid.refresh();
            break;
        }
        case "UnfreezeColumn": {
            grid.frozenColumns = 0;
            grid.refresh();
            break;
        }
        case "FreezeRow": {
            const rowIndex = args.rowInfo?.rowIndex ?? args.row?.rowIndex;
            if (rowIndex != null && rowIndex >= 0) {
                grid.frozenRows = rowIndex + 1;
                grid.dataBind();
                grid.height = "54vh";
                grid.dataBind();
            }
            break;
        }
        case "UnfreezeRow": {
            grid.frozenRows = 0;
            grid.refresh();
            break;
        }
    }
}
