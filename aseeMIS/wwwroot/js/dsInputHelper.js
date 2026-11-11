(function () {
    window.ds = window.ds || {};
    const input = window.ds.input = window.ds.input || {};

    function getElement(idOrElement) {
        if (!idOrElement) return null;
        if (typeof idOrElement !== "string") return idOrElement;
        return document.getElementById(idOrElement);
    }

    function getEj2(idOrElement) {
        return getElement(idOrElement)?.ej2_instances?.[0] || null;
    }

    input.getElement = getElement;
    input.getControl = getEj2;

    input.getInput = function (idOrElement) {
        const element = getElement(idOrElement);
        if (!element) return null;
        if (element.matches?.("input, select, textarea")) return element;
        return element.querySelector?.("input, select, textarea") || null;
    };

    input.getValue = function (idOrElement) {
        const element = getElement(idOrElement);
        const control = getEj2(idOrElement);

        if (control && Object.prototype.hasOwnProperty.call(control, "value")) {
            return control.value;
        }

        if (element?.matches?.("input, select, textarea")) {
            if (element.type === "checkbox") return element.checked;
            return element.value ?? "";
        }

        const nested = element?.querySelector?.("input, select, textarea");
        if (nested?.type === "checkbox") return nested.checked;
        return nested?.value ?? "";
    };

    input.setValue = function (idOrElement, value) {
        const element = getElement(idOrElement);
        const control = getEj2(idOrElement);

        if (control && Object.prototype.hasOwnProperty.call(control, "value")) {
            control.value = value ?? null;
            control.dataBind?.();
            return;
        }

        if (element?.matches?.("input, select, textarea")) {
            if (element.type === "checkbox") {
                element.checked = !!value;
            } else {
                element.value = value ?? "";
            }
            element.dispatchEvent(new Event("input", { bubbles: true }));
            element.dispatchEvent(new Event("change", { bubbles: true }));
            return;
        }

        const nested = element?.querySelector?.("input, select, textarea");
        if (nested) {
            if (nested.type === "checkbox") {
                nested.checked = !!value;
            } else {
                nested.value = value ?? "";
            }
            nested.dispatchEvent(new Event("input", { bubbles: true }));
            nested.dispatchEvent(new Event("change", { bubbles: true }));
        }
    };

    input.setEnabled = function (idOrElement, enabled) {
        const control = getEj2(idOrElement);
        if (control) {
            control.enabled = enabled;
            control.dataBind?.();
            return;
        }

        const field = input.getInput(idOrElement);
        if (field) field.disabled = !enabled;
    };

    input.getDropdownData = function (idOrElement) {
        const control = getEj2(idOrElement);
        const sources = [control?.dataSource, control?.listData];

        for (const data of sources) {
            if (Array.isArray(data)) return data;
            if (Array.isArray(data?.result)) return data.result;
        }

        return [];
    };

    input.normalizeText = function (value) {
        return String(value ?? "")
            .trim()
            .toLowerCase()
            .normalize("NFD")
            .replace(/[\u0300-\u036f]/g, "");
    };

    input.getRowValue = function (row, names) {
        for (const name of names) {
            const value = row?.[name];
            if (value !== null && value !== undefined && value !== "") return value;
        }

        return null;
    };

    input.isEmptyValue = function (value) {
        return value === null || value === undefined || String(value).trim() === "";
    };

    function normalizeRows(rows) {
        if (Array.isArray(rows)) return rows;
        if (Array.isArray(rows?.result)) return rows.result;
        return [];
    }

    function getLookupCandidates(row, fieldNames, includeScalarValues) {
        const explicitCandidates = (fieldNames || []).map(function (name) {
            return row?.[name];
        });

        const scalarCandidates = includeScalarValues === true && row && typeof row === "object"
            ? Object.values(row).filter(function (value) {
                const type = typeof value;
                return type === "string" || type === "number";
            })
            : [];

        return explicitCandidates
            .concat(scalarCandidates)
            .filter(function (value) {
                return value !== null && value !== undefined && String(value).trim() !== "";
            });
    }

    input.findRowBySelectedValue = function (rows, selectedValue, options) {
        const selected = input.normalizeText(selectedValue);
        if (!selected) return null;

        const config = options || {};
        const fieldNames = config.fieldNames || [];
        const includeScalarValues = config.includeScalarValues === true;
        const normalizedRows = normalizeRows(rows);

        return normalizedRows.find(function (row) {
            const candidates = getLookupCandidates(row, fieldNames, includeScalarValues);
            return candidates.some(function (candidate) {
                return input.normalizeText(candidate) === selected;
            });
        }) ?? null;
    };

    input.findCountryRowBySelectedValue = function (selectedValue, rowsOrDropdown) {
        const rows = typeof rowsOrDropdown === "string"
            ? input.getDropdownData(rowsOrDropdown)
            : normalizeRows(rowsOrDropdown);

        return input.findRowBySelectedValue(rows, selectedValue, {
            fieldNames: [
                "CountryCode",
                "countryCode",
                "Value",
                "value",
                "ConstantKey",
                "constantKey",
                "CountryName",
                "countryName",
                "Text",
                "text",
                "ConstantValue",
                "constantValue",
                "Name",
                "name",
                "Label",
                "label"
            ],
            includeScalarValues: true
        });
    };

    input.getCountryPhonePrefixBySelectedValue = function (selectedValue, rowsOrDropdown) {
        const row = input.findCountryRowBySelectedValue(selectedValue, rowsOrDropdown);
        return input.getCountryPhonePrefix(row);
    };

    input.getFieldContainer = function (idOrElement) {
        const element = getElement(idOrElement);
        return element?.closest?.("[class*='col-']") || element?.parentElement || null;
    };

    input.setFieldInvalid = function (id, message) {
        const container = input.getFieldContainer(id);
        const element = getElement(id);
        const field = input.getInput(id);

        container?.classList.add("ds-invalid");
        element?.classList.add("ds-invalid");
        field?.setAttribute("aria-invalid", "true");

        if (!container) return;

        let error = container.querySelector(`.ds-field-error[data-for='${id}']`);
        if (!error) {
            error = document.createElement("div");
            error.className = "ds-field-error";
            error.dataset.for = id;
            container.appendChild(error);
        }

        error.textContent = message;
        error.classList.add("show");
    };

    input.clearFieldInvalid = function (id) {
        const container = input.getFieldContainer(id);
        const element = getElement(id);
        const field = input.getInput(id);

        container?.classList.remove("ds-invalid");
        element?.classList.remove("ds-invalid");
        field?.removeAttribute("aria-invalid");
        container?.querySelector(`.ds-field-error[data-for='${id}']`)?.classList.remove("show");
    };

    input.clearValidationState = function (fields) {
        (fields || []).forEach(function (field) {
            input.clearFieldInvalid(typeof field === "string" ? field : field.id);
        });
    };

    input.focusField = function (idOrField) {
        const id = typeof idOrField === "string" ? idOrField : idOrField?.id;
        if (!id) return;

        const element = getElement(id);
        element?.scrollIntoView?.({ behavior: "smooth", block: "center" });

        const control = getEj2(id);
        if (typeof control?.focusIn === "function") {
            control.focusIn();
            return;
        }

        input.getInput(id)?.focus?.();
    };

    input.showValidationMessage = function (message) {
        const text = message || "Please correct validation errors.";

        if (window.notify?.warning) {
            window.notify.warning(text);
            return;
        }

        if (typeof window.showMessage === "function") {
            window.showMessage(text, "Warning");
            return;
        }

        alert(text);
    };

    input.bindControlChange = function (id, handler, key) {
        const bindKey = key || "dsChangeBound";
        const element = getElement(id);
        if (element && element.dataset[bindKey] !== "true") {
            element.dataset[bindKey] = "true";
            element.addEventListener("change", handler);
        }

        const control = getEj2(id);
        const controlKey = `__${bindKey}`;
        if (control && control[controlKey] !== true) {
            const previousChange = control.change;
            control.change = function (args) {
                if (typeof previousChange === "function") previousChange.call(control, args);
                handler(args);
            };
            control[controlKey] = true;
            control.dataBind?.();
        }
    };

    input.bindInputChange = function (id, handler, key) {
        const field = input.getInput(id);
        const bindKey = key || "dsInputChangeBound";
        if (!field || field.dataset[bindKey] === "true") return;

        field.dataset[bindKey] = "true";
        field.addEventListener("input", handler);
        field.addEventListener("change", handler);
    };

    input.copyValue = function (sourceId, targetId, options) {
        const value = input.getValue(sourceId);
        if (!value) return false;

        input.setValue(targetId, value);

        const config = options ?? {};
        if (config.sourcePrefixId && config.targetPrefixId) {
            input.setValue(config.targetPrefixId, input.getValue(config.sourcePrefixId));
        }

        window.notify?.success?.("Copied");
        return true;
    };

    function normalizePhonePrefix(value) {
        const text = String(value ?? "").trim();
        if (!text) return "";

        const match = text.match(/\+\s*\d{1,4}/);
        if (match) return `+${match[0].replace(/\D/g, "").slice(0, 4)}`;

        let digits = text.replace(/\D/g, "");
        if (digits.startsWith("00")) {
            digits = digits.slice(2);
        }

        return digits ? `+${digits.slice(0, 4)}` : "";
    }

    input.normalizePhonePrefix = normalizePhonePrefix;

    input.composePhoneNumber = function (number, prefix) {
        const value = String(number ?? "").trim();
        if (!value) return "";

        const compactValue = value.replace(/\s+/g, "");
        if (compactValue.startsWith("+")) return compactValue;

        const localNumber = value.replace(/\D/g, "");
        const normalizedPrefix = normalizePhonePrefix(prefix);

        return normalizedPrefix && localNumber ? `${normalizedPrefix}${localNumber}` : value;
    };

    input.stripPhonePrefix = function (number, prefix) {
        const digits = String(number ?? "").replace(/\D+/g, "");
        const prefixDigits = normalizePhonePrefix(prefix).replace(/\D+/g, "");

        return prefixDigits && digits.startsWith(prefixDigits)
            ? digits.slice(prefixDigits.length)
            : digits;
    };

    input.normalizeLocalPhone = function (number, prefix, maxLength) {
        let digits = input.stripPhonePrefix(number, prefix);

        while (digits.startsWith("0")) {
            digits = digits.slice(1);
        }

        const length = Number(maxLength);
        return Number.isInteger(length) && length > 0
            ? digits.slice(0, length)
            : digits;
    };

    input.getCountryPhonePrefix = function (row) {
        return normalizePhonePrefix(input.getRowValue(row, [
            "PhonePrefix",
            "phonePrefix",
            "CountryPhonePrefix",
            "countryPhonePrefix"
        ]));
    };

    input.getCountryPhoneMaxLength = function (row) {
        const value = input.getRowValue(row, [
            "PhoneNumberMaxLenght",
            "phoneNumberMaxLenght",
            "PhoneNumberMaxLength",
            "phoneNumberMaxLength",
            "PhoneMaxLength",
            "phoneMaxLength"
        ]);
        const length = Number(value);
        return Number.isInteger(length) && length > 0 ? length : null;
    };

    // Reads DefCountry.PersonalNumberMaxLength (the legal-ID number length for that
    // country) tolerantly across casings / shorter aliases. Returns null if the
    // country row does not define one — callers should then fall back to a generic
    // limit (or skip the strict length check).
    input.getCountryPersonalNumberMaxLength = function (row) {
        const value = input.getRowValue(row, [
            "PersonalNumberMaxLength",
            "personalNumberMaxLength",
            "PersonalNumberMaxLenght",
            "personalNumberMaxLenght",
            "PersonalIdMaxLength",
            "personalIdMaxLength"
        ]);
        const length = Number(value);
        return Number.isInteger(length) && length > 0 ? length : null;
    };

    input.isResidentCountryRow = function (row) {
        const value = input.getRowValue(row, [
            "IsResidentCountry",
            "isResidentCountry",
            "ResidentCountry",
            "residentCountry"
        ]);

        const normalized = String(value ?? "").trim().toLowerCase();
        return value === true
            || value === 1
            || normalized === "1"
            || normalized === "true";
    };

    input.createPhonePrefixManager = function (options) {
        const config = options || {};
        const fields = config.fields || {};
        const fieldIds = Object.keys(fields);
        const manualState = {};
        let initialized = false;

        fieldIds.forEach(function (fieldId) {
            manualState[fieldId] = false;
        });

        const getFieldValue = config.getValue || input.getValue;
        const setFieldValue = config.setValue || input.setValue;
        const getCountryPrefix = config.getCountryPrefix || function () { return ""; };
        const getKnownPrefixes = config.getKnownPrefixes || function () { return []; };
        const getCountryRows = config.getCountryRows || function () { return []; };
        const configuredGetPhoneMaxLength = config.getPhoneMaxLength;

        function normalizeRows(rows) {
            if (Array.isArray(rows)) return rows;
            if (Array.isArray(rows?.result)) return rows.result;
            return [];
        }

        function getPrefixInput(fieldId) {
            const field = fields[fieldId];
            return field ? getElement(field.inputId) : null;
        }

        function getPhoneInput(fieldId) {
            return input.getInput(fieldId);
        }

        function findCountryRowByPrefix(prefix) {
            const normalizedPrefix = normalizePhonePrefix(prefix);
            if (!normalizedPrefix) return null;

            return normalizeRows(getCountryRows()).find(function (row) {
                return input.getCountryPhonePrefix(row) === normalizedPrefix;
            }) ?? null;
        }

        function getPhoneMaxLength(prefix) {
            if (typeof configuredGetPhoneMaxLength === "function") {
                const configuredLength = Number(configuredGetPhoneMaxLength(prefix));
                if (Number.isInteger(configuredLength) && configuredLength > 0) {
                    return configuredLength;
                }
            }

            return input.getCountryPhoneMaxLength(findCountryRowByPrefix(prefix));
        }

        function applyPhoneMaxLength(fieldId, prefix) {
            const field = getPhoneInput(fieldId);
            if (!field) return;

            const length = getPhoneMaxLength(prefix);
            if (!length) return;

            field.maxLength = length;
            field.setAttribute("maxlength", String(length));
        }

        function setBadgeState(fieldId) {
            const field = fields[fieldId];
            const badge = field ? getElement(field.badgeId) : null;
            badge?.classList.add("is-resident");
        }

        function writePrefix(fieldId, prefix) {
            const prefixInput = getPrefixInput(fieldId);
            const normalizedPrefix = normalizePhonePrefix(prefix);

            if (prefixInput) {
                prefixInput.value = normalizedPrefix || "+";
            }

            setBadgeState(fieldId);
            applyPhoneMaxLength(fieldId, normalizedPrefix);
        }

        function setPrefix(fieldId, prefix, setOptions) {
            const setConfig = setOptions || {};

            if (setConfig.resetManual === true) {
                manualState[fieldId] = false;
            }

            if (setConfig.force !== true && manualState[fieldId]) {
                return;
            }

            writePrefix(fieldId, prefix);
        }

        function getPrefix(fieldId) {
            return normalizePhonePrefix(getPrefixInput(fieldId)?.value);
        }

        function collectKnownPhonePrefixes() {
            const prefixes = new Set();

            (getKnownPrefixes() || []).forEach(function (prefix) {
                const normalizedPrefix = normalizePhonePrefix(prefix);
                if (normalizedPrefix) prefixes.add(normalizedPrefix);
            });

            fieldIds.forEach(function (fieldId) {
                const prefix = getPrefix(fieldId);
                if (prefix) prefixes.add(prefix);
            });

            return Array.from(prefixes).sort((a, b) => b.length - a.length);
        }

        function findPrefixInValue(value, preferredPrefix) {
            const compactValue = String(value ?? "").replace(/\s+/g, "");
            if (!compactValue.startsWith("+")) return "";

            const preferred = normalizePhonePrefix(preferredPrefix);
            const candidates = [
                ...(preferred ? [preferred] : []),
                ...collectKnownPhonePrefixes().filter(prefix => prefix !== preferred)
            ];

            const matchedKnownPrefix = candidates.find(prefix => compactValue.startsWith(prefix));
            if (matchedKnownPrefix) return matchedKnownPrefix;

            return compactValue.match(/^\+\d{1,4}/)?.[0] ?? "";
        }

        function splitPhoneNumberForDisplay(fieldId) {
            const value = String(getFieldValue(fieldId) ?? "").trim();
            const compactValue = value.replace(/\s+/g, "");

            if (!compactValue.startsWith("+")) return;

            const matchedPrefix = findPrefixInValue(compactValue, getPrefix(fieldId));
            if (!matchedPrefix) return;

            writePrefix(fieldId, matchedPrefix);
            manualState[fieldId] = true;
            setFieldValue(fieldId, compactValue.slice(matchedPrefix.length));
        }

        function bindInputs() {
            fieldIds.forEach(function (fieldId) {
                const prefixInput = getPrefixInput(fieldId);
                if (!prefixInput || prefixInput.dataset.dsPhonePrefixBound === "true") return;

                prefixInput.dataset.dsPhonePrefixBound = "true";
                prefixInput.addEventListener("click", function (event) {
                    event.stopPropagation();
                    prefixInput.focus();
                    prefixInput.select();
                });
                prefixInput.addEventListener("focus", function () {
                    setTimeout(function () {
                        prefixInput.select();
                    }, 0);
                });
                prefixInput.addEventListener("input", function () {
                    manualState[fieldId] = true;
                    const value = String(prefixInput.value ?? "").replace(/[^\d+]/g, "");
                    const digits = value.replace(/\D/g, "").slice(0, 4);
                    prefixInput.value = digits ? `+${digits}` : "+";
                    setBadgeState(fieldId);
                    applyPhoneMaxLength(fieldId, prefixInput.value);
                });
                prefixInput.addEventListener("blur", function () {
                    manualState[fieldId] = true;
                    writePrefix(fieldId, prefixInput.value);
                });
            });
        }

        function sync(setOptions) {
            const prefix = getCountryPrefix();
            fieldIds.forEach(function (fieldId) {
                setPrefix(fieldId, prefix, setOptions);
            });
        }

        function splitAll() {
            fieldIds.forEach(splitPhoneNumberForDisplay);
        }

        return {
            bindInputs,
            sync,
            splitAll,
            initialize: function () {
                bindInputs();

                if (!initialized) {
                    sync({ force: true, resetManual: true });
                    splitAll();
                    initialized = true;
                    return;
                }

                sync();
            },
            compose: function (fieldId) {
                return input.composePhoneNumber(getFieldValue(fieldId), getPrefix(fieldId));
            },
            getPrefixValue: getPrefix,
            getMaxLength: function (fieldId) {
                return getPhoneMaxLength(getPrefix(fieldId));
            },
            setPrefixValue: setPrefix
        };
    };

    input.hasDateMaskText = function (value) {
        const text = String(value ?? "").trim().toLowerCase();
        return text.includes("day") || text.includes("month") || text.includes("year");
    };

    input.parseDateOnly = function (value) {
        if (!value || input.hasDateMaskText(value)) return null;

        if (value instanceof Date) {
            return Number.isNaN(value.getTime()) ? null : value;
        }

        const text = String(value).trim();
        if (!text) return null;

        const isoMatch = text.match(/^(\d{4})-(\d{1,2})-(\d{1,2})(?:T.*)?$/);
        const localMatch = text.match(/^(\d{1,2})[-/.](\d{1,2})[-/.](\d{4})$/);
        const parts = isoMatch
            ? { year: Number(isoMatch[1]), month: Number(isoMatch[2]), day: Number(isoMatch[3]) }
            : localMatch
                ? { year: Number(localMatch[3]), month: Number(localMatch[2]), day: Number(localMatch[1]) }
                : null;

        if (!parts) {
            const nativeParsed = new Date(text);
            return Number.isNaN(nativeParsed.getTime()) ? null : nativeParsed;
        }

        const parsed = new Date(parts.year, parts.month - 1, parts.day);
        if (
            parsed.getFullYear() !== parts.year ||
            parsed.getMonth() !== parts.month - 1 ||
            parsed.getDate() !== parts.day
        ) {
            return null;
        }

        return parsed;
    };

    input.getDateValue = function (id) {
        const value = input.getValue(id);
        if (!input.isEmptyValue(value)) return value;

        return input.getInput(id)?.value ?? null;
    };

    input.isClearableDateValue = function (value) {
        if (input.isEmptyValue(value)) return false;
        if (input.hasDateMaskText(value)) return true;

        const text = String(value).trim();
        if (text.includes("0001") || text === "0001-01-01" || text === "01-01-0001") return true;

        const parsed = input.parseDateOnly(value);
        return !!parsed && parsed.getFullYear() <= 1900;
    };

    input.clearDateInputIfNeeded = function (id, options) {
        const config = options || {};
        const control = input.getControl(id);
        const field = input.getInput(id);
        const shouldClear =
            input.isClearableDateValue(control?.value) ||
            input.isClearableDateValue(field?.value);

        if (control) {
            control.enableMask = false;
            if (shouldClear && "value" in control) {
                control.value = null;
            }
            control.dataBind?.();
        }

        if (field) {
            field.placeholder = config.placeholder || "dd-MM-yyyy";
            if (shouldClear) {
                field.value = "";
            }
        }
    };

    input.configureDateInputs = function (ids, options) {
        const config = options || {};
        const bindKey = config.bindKey || "dsDateBound";

        (ids || []).forEach(function (id) {
            const field = input.getInput(id);
            input.clearDateInputIfNeeded(id, config);

            if (field && field.dataset[bindKey] !== "true") {
                field.dataset[bindKey] = "true";
                field.addEventListener("focus", function () {
                    input.clearDateInputIfNeeded(id, config);
                });
                field.addEventListener("blur", function () {
                    input.clearDateInputIfNeeded(id, config);
                });
            }
        });
    };

    input.isDefaultDate = function (value) {
        if (!value || input.hasDateMaskText(value)) return true;
        if (String(value).includes("0001")) return true;

        const parsed = input.parseDateOnly(value);
        return !parsed || parsed.getFullYear() <= 1900;
    };

    input.copyToClipboard = async function (sourceId, options) {
        const config = options ?? {};
        const prefixId = config.prefixId;
        const value = prefixId
            ? input.composePhoneNumber(input.getValue(sourceId), input.getValue(prefixId))
            : input.getValue(sourceId);
        if (!value) return false;

        if (navigator.clipboard?.writeText) {
            await navigator.clipboard.writeText(value);
        } else {
            const temp = document.createElement("textarea");
            temp.value = value;
            temp.setAttribute("readonly", "readonly");
            temp.style.position = "fixed";
            temp.style.opacity = "0";
            document.body.appendChild(temp);
            temp.select();
            document.execCommand("copy");
            temp.remove();
        }

        window.notify?.success?.("Copied");
        return true;
    };

    input.copyTextToClipboard = async function (value) {
        const text = String(value ?? "").trim();
        if (!text) return false;

        if (navigator.clipboard?.writeText) {
            await navigator.clipboard.writeText(text);
        } else {
            const temp = document.createElement("textarea");
            temp.value = text;
            temp.setAttribute("readonly", "readonly");
            temp.style.position = "fixed";
            temp.style.opacity = "0";
            document.body.appendChild(temp);
            temp.select();
            document.execCommand("copy");
            temp.remove();
        }

        window.notify?.success?.("Copied");
        return true;
    };

    input.copyCombinedToClipboard = function (sourceIds, options) {
        const config = options ?? {};
        const separator = config.separator ?? " ";
        const text = (sourceIds || [])
            .map(id => input.getValue(id))
            .map(value => String(value ?? "").trim())
            .filter(Boolean)
            .join(separator)
            .trim();

        return input.copyTextToClipboard(text);
    };

    input.isEmail = function (value) {
        return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(String(value ?? "").trim());
    };

    input.bindEmailValidation = function (scope) {
        const root = scope || document;

        root.querySelectorAll("[data-ds-email='true'], .ds-email-input").forEach(function (element) {
            const field = element.matches("input, textarea")
                ? element
                : element.querySelector("input, textarea");

            if (!field || field.dataset.dsEmailBound === "true") return;

            field.dataset.dsEmailBound = "true";
            field.setAttribute("inputmode", "email");
            field.setAttribute("autocomplete", "email");

            field.addEventListener("blur", function () {
                const value = field.value.trim();
                const isValid = !value || input.isEmail(value);
                field.classList.toggle("ds-input-invalid", !isValid);

                if (!isValid) {
                    window.notify?.warning?.("Email must contain a valid @ domain.");
                }
            });
        });
    };

    function runCopyButton(button) {
        const combinedSources = button.getAttribute("data-ds-copy-sources");
        if (combinedSources) {
            const separator = button.getAttribute("data-ds-copy-separator") || " ";
            input.copyCombinedToClipboard(
                combinedSources.split(",").map(value => value.trim()).filter(Boolean),
                { separator }
            );
            return;
        }

        const sourceId = button.getAttribute("data-ds-copy-source");
        const targetId = button.getAttribute("data-ds-copy-target");
        const prefixId = button.getAttribute("data-ds-copy-prefix");
        const sourcePrefixId = button.getAttribute("data-ds-copy-source-prefix");
        const targetPrefixId = button.getAttribute("data-ds-copy-target-prefix");
        const mode = button.getAttribute("data-ds-copy-mode") || (targetId ? "field" : "clipboard");

        if (!sourceId) return;

        if (mode === "clipboard" || !targetId) {
            input.copyToClipboard(sourceId, { prefixId });
            return;
        }

        input.copyValue(sourceId, targetId, { sourcePrefixId, targetPrefixId });
    }

    input.bindCopyButtons = function (scope) {
        const root = scope || document;

        root.querySelectorAll(".ds-copy-btn[data-ds-copy-source], .ds-copy-btn[data-ds-copy-sources]").forEach(function (button) {
            if (button.dataset.dsCopyBound === "true") return;

            button.dataset.dsCopyBound = "true";
            button.addEventListener("click", function () {
                runCopyButton(button);
            });
            button.addEventListener("keydown", function (event) {
                if (event.key === "Enter" || event.key === " ") {
                    event.preventDefault();
                    runCopyButton(button);
                }
            });
        });
    };

    // ============================================================================
    // SYNCFUSION DATEPICKER / DATETIMEPICKER — global normalization & validation
    // ----------------------------------------------------------------------------
    // Two recurring problems across the app:
    //   1. C# non-nullable DateOnly / DateTime properties round-trip as their .NET
    //      MinValue (0001-01-01), and Syncfusion happily renders that as a real
    //      date instead of showing the mask "dd-MM-yyyy" placeholder. Users see
    //      "01-01-0001" everywhere on Add forms.
    //   2. Different views use a mix of dd-MM-yyyy / dd.MM.yyyy / dd/MM/yyyy
    //      formats. The app standard is dd-MM-yyyy with dash separators (chosen
    //      for readability — slash characters visually clash with the trailing
    //      slash on RTL/LTR field icons).
    // The normalizer below fixes BOTH issues for every datepicker on the page
    // regardless of where/when it was rendered (initial load, modal open,
    // AJAX-loaded partial), so individual cshtml files don't need to repeat this.
    // ============================================================================

    const DS_DATE_FORMAT = "dd-MM-yyyy";
    // Anything older than 1900 we treat as a sentinel "empty" — covers
    // DateOnly.MinValue (0001-01-01) and any accidental epoch-zero values.
    const DS_DATE_MIN_VALID_TIME = new Date("1900-01-01").getTime();

    function isSyncfusionDateModule(name) {
        return name === "datepicker" || name === "datetimepicker";
    }

    function isEmptyDateValue(value) {
        if (value == null) return true;
        if (value instanceof Date) {
            const t = value.getTime();
            return isNaN(t) || t < DS_DATE_MIN_VALID_TIME;
        }
        // String values from server (ISO) — check for the MinValue prefix.
        if (typeof value === "string") {
            return value === "" || value.startsWith("0001-01-01");
        }
        return false;
    }

    input.normalizeSyncfusionDatePickers = function (scope) {
        const root = scope || document;
        // Only walk inputs — Syncfusion always renders datepickers as wrapped
        // <input> elements with ej2_instances attached to the input itself.
        root.querySelectorAll("input").forEach(function (field) {
            const ej = field.ej2_instances?.[0];
            if (!ej || !isSyncfusionDateModule(ej.getModuleName?.())) return;
            const isGridEditDialogField = !!field.closest?.(".e-dialog.e-edit-dialog, .e-edit-dialog");

            // (a) Enforce the app-wide dd/MM/yyyy format.
            if (!isGridEditDialogField && ej.format !== DS_DATE_FORMAT) {
                try {
                    ej.format = DS_DATE_FORMAT;
                } catch (_) { /* ignore — older instances may be locked */ }
            }

            // (b) Ensure the mask hint is visible so users see "dd/MM/yyyy".
            if (!isGridEditDialogField && ej.enableMask !== true) {
                try { ej.enableMask = true; } catch (_) { /* ignore */ }
            }

            // (c) Clear .NET MinValue so the placeholder/mask actually shows.
            if (isEmptyDateValue(ej.value)) {
                try { ej.value = null; } catch (_) { /* ignore */ }
            }

            try { ej.dataBind?.(); } catch (_) { /* ignore */ }
        });
    };

    // Submit-time validator. Returns true when every required datepicker has a
    // real (non-empty / non-MinValue) value. Toggles `ds-input-invalid` on the
    // wrapper for the existing red-border CSS, focuses the first invalid field,
    // and (by default) raises a toast. Callers integrate this into their save
    // flow — see entityDetailHelper.js ds.entity.save.
    input.validateRequiredDatePickers = function (scope, options) {
        const root = scope || document;
        const showToast = options?.showToast !== false;
        let firstInvalid = null;
        let invalidCount = 0;

        root.querySelectorAll("input").forEach(function (field) {
            const ej = field.ej2_instances?.[0];
            if (!ej || !isSyncfusionDateModule(ej.getModuleName?.())) return;

            // Required is signalled either by the native attribute (Syncfusion
            // mirrors required="true" onto the rendered input) or by the
            // ds-required-empty CSS marker the views already use for styling.
            const wrapper = field.closest("[id]");
            const ancestorRequired = field.closest(".ds-required-empty");
            const isRequired = field.required === true
                || field.getAttribute("aria-required") === "true"
                || !!ancestorRequired;
            if (!isRequired) return;

            const empty = isEmptyDateValue(ej.value);
            const marker = ancestorRequired || wrapper;
            marker?.classList?.toggle("ds-input-invalid", empty);

            if (empty) {
                invalidCount += 1;
                if (!firstInvalid) firstInvalid = marker || field;
            }
        });

        if (invalidCount > 0) {
            if (showToast) {
                window.notify?.warning?.(
                    invalidCount === 1
                        ? "Please fill the required date field."
                        : `Please fill all required date fields (${invalidCount} missing).`
                );
            }
            firstInvalid?.scrollIntoView?.({ behavior: "smooth", block: "center" });
            const focusable = firstInvalid?.querySelector?.("input") || firstInvalid;
            focusable?.focus?.();
        }

        return invalidCount === 0;
    };

    function setupDatePickerNormalizationObserver() {
        if (window.__dsDateObserverInstalled) return;
        window.__dsDateObserverInstalled = true;

        let scheduled = false;
        const scheduleNormalize = function () {
            if (scheduled) return;
            scheduled = true;
            requestAnimationFrame(function () {
                scheduled = false;
                try { input.normalizeSyncfusionDatePickers(document); } catch (_) { /* ignore */ }
            });
        };

        // Catch every late-mounted Syncfusion datepicker (modals, AJAX partials,
        // grid edit dialogs). Cheap: subtree mutations debounced into a single
        // rAF per tick.
        try {
            new MutationObserver(function (mutations) {
                for (const m of mutations) {
                    if (m.addedNodes && m.addedNodes.length) {
                        scheduleNormalize();
                        return;
                    }
                }
            }).observe(document.body, { childList: true, subtree: true });
        } catch (_) { /* MutationObserver should always exist, but be safe */ }

        // Initial passes — Syncfusion's <ejs-scripts> runs slightly after
        // DOMContentLoaded, so cover both ticks defensively.
        setTimeout(scheduleNormalize, 0);
        setTimeout(scheduleNormalize, 150);
    }

    input.init = function (scope) {
        input.bindCopyButtons(scope);
        input.bindEmailValidation(scope);
        input.normalizeSyncfusionDatePickers(scope);
    };

    document.addEventListener("DOMContentLoaded", function () {
        input.init(document);
        setupDatePickerNormalizationObserver();
    });
})();
