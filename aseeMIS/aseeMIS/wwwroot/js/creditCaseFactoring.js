let factoringCaseWorkflowSteps = Array.isArray(window.factoringCaseWorkflowSteps) ? window.factoringCaseWorkflowSteps : [];

const factoringCaseStatusEndpoint = "/Credit/CreditCaseFactoring/GetNextCaseStatusesForDropdown";
const factoringPolicyEndpoint = "/Credit/CreditCaseFactoring/GetPoliciesByFactoringType";

function getFactoringSyncfusionValue(id) {
    return document.getElementById(id)?.ej2_instances?.[0]?.value ?? null;
}

function getPersistedFactoringStatusCode() {
    return document.getElementById("CurrentStatusCode")?.value ?? "";
}

function getPersistedFactoringDecisionReasonCode() {
    return (document.getElementById("CurrentDecisionReasonCode")?.value ?? "").toString().trim();
}

function normalizeFactoringStatusCode(value) {
    return (value || "").toString().trim().toUpperCase();
}

function storeFactoringCaseWorkflowSteps(steps) {
    factoringCaseWorkflowSteps = Array.isArray(steps) ? steps : [];
    window.factoringCaseWorkflowSteps = factoringCaseWorkflowSteps;
}

function applyFactoringCaseStatuses(statuses, preferredStatusCode) {
    const ddl = document.getElementById("StatusCode")?.ej2_instances?.[0];
    if (!ddl) {
        return;
    }

    const items = statuses || [];
    ddl.dataSource = items;

    let resolvedValue = preferredStatusCode || ddl.value || null;
    if (!resolvedValue || !items.some(x => x.CaseStatusCode === resolvedValue)) {
        resolvedValue = items.length > 0 ? items[0].CaseStatusCode : null;
    }

    ddl.value = resolvedValue;
    ddl.dataBind();
}

function applyFactoringWorkflowDefinition(payload, preferredStatusCode) {
    if (Array.isArray(payload)) {
        applyFactoringCaseStatuses(payload, preferredStatusCode);
    } else {
        storeFactoringCaseWorkflowSteps(payload?.workflowSteps || []);
        applyFactoringCaseStatuses(payload?.statuses || [], preferredStatusCode);
    }

    if (typeof window.syncFactoringCaseFormMode === "function") {
        window.syncFactoringCaseFormMode(getFactoringSyncfusionValue("StatusCode") || preferredStatusCode || "");
    }
    window.syncFactoringDecisionReasons(getFactoringSyncfusionValue("StatusCode"), true);
}

function loadFactoringCaseWorkflowStatuses(productCode, currentStatusCode) {
    const requestedProduct = productCode;
    applyFactoringWorkflowDefinition({ statuses: [], workflowSteps: [] }, null);
    if (!productCode) {
        return;
    }

    $.ajax({
        url: factoringCaseStatusEndpoint,
        type: "GET",
        data: {
            productCode: productCode,
            currentStatusCode: currentStatusCode || ""
        },
        success: function (payload) {
            if (getFactoringSyncfusionValue("ProductCode") !== requestedProduct) return;
            applyFactoringWorkflowDefinition(payload, currentStatusCode);
        },
        error: function () {
            if (getFactoringSyncfusionValue("ProductCode") !== requestedProduct) return;
            applyFactoringWorkflowDefinition({ statuses: [], workflowSteps: [] }, null);
        }
    });
}

window.onFactoringProductChange = function () {
    const productDropdown = document.getElementById("ProductCode")?.ej2_instances?.[0];
    const productCode = getFactoringSyncfusionValue("ProductCode");
    const product = productDropdown?.itemData
        ?? (productDropdown?.dataSource || []).find(x => x.ProductCode === productCode);
    const workflow = document.getElementById("WorkflowCode");
    if (workflow) workflow.value = product?.WorkflowCode || "";

    const productTypeCodeInput = document.getElementById("ProductTypeCode");
    if (productTypeCodeInput) {
        productTypeCodeInput.value = product?.ProductTypeCode || "";
    }

    const statusCode = getPersistedFactoringStatusCode();
    loadFactoringCaseWorkflowStatuses(productCode, statusCode);
};

function formatFactoringPolicyLabel(policy) {
    const name = policy?.FactoringPolicyName || policy?.FactoringPolicyCode || "";
    const required = policy?.NotificationRequired === true;
    return `${name} — Lajmërim: ${required ? "Po" : "Jo"}`;
}

function mapFactoringPolicyOptions(items) {
    return (items || []).map(function (policy) {
        return {
            ...policy,
            FactoringPolicyName: formatFactoringPolicyLabel(policy)
        };
    });
}

window.onFactoringTypeChange = function (args) {
    const policyDdl = document.getElementById("FactoringPolicyCode")?.ej2_instances?.[0];
    if (!policyDdl) {
        return;
    }

    const factoringTypeCode = args?.value || getFactoringSyncfusionValue("FactoringTypeCode") || "";
    policyDdl.enabled = false;
    policyDdl.value = null;
    policyDdl.dataSource = [];
    policyDdl.dataBind();
    window.factoringPolicyOptions = [];
    window.onFactoringPolicyChange({ value: null });

    if (!factoringTypeCode) {
        policyDdl.enabled = true;
        policyDdl.dataBind();
        return;
    }

    $.ajax({
        url: factoringPolicyEndpoint,
        type: "GET",
        data: { factoringTypeCode: factoringTypeCode },
        success: function (items) {
            if (getFactoringSyncfusionValue("FactoringTypeCode") !== factoringTypeCode) return;
            window.factoringPolicyOptions = mapFactoringPolicyOptions(items);
            policyDdl.dataSource = window.factoringPolicyOptions;
            policyDdl.value = window.factoringPolicyOptions.find(x => x.IsDefault)?.FactoringPolicyCode || null;
            policyDdl.enabled = true;
            policyDdl.dataBind();
            window.onFactoringPolicyChange({ value: policyDdl.value });
        },
        error: function () {
            policyDdl.enabled = true;
            policyDdl.dataBind();
        }
    });
};

window.onFactoringStatusChange = function (args) {
    const statusCode = args?.value || "";

    if (typeof window.syncFactoringCaseFormMode === "function") {
        window.syncFactoringCaseFormMode(statusCode);
    }
    window.syncFactoringDecisionReasons(statusCode, false);
};

window.onFactoringPolicyChange = function (args) {
    const risk = document.getElementById("RecoursePolicyCode")?.ej2_instances?.[0];
    if (!risk) return;
    const policy = (window.factoringPolicyOptions || []).find(x => x.FactoringPolicyCode === args?.value);
    risk.dataSource = (window.factoringRiskBearers || []).filter(x => x.RiskBearerCode === policy?.RiskBearerCode);
    risk.value = policy?.RiskBearerCode || null;
    risk.enabled = false;
    risk.dataBind();
};

window.syncFactoringDecisionReasons = function (statusCode, keepCurrent) {
    const status = (document.getElementById("StatusCode")?.ej2_instances?.[0]?.dataSource || []).find(x => x.CaseStatusCode === statusCode);
    const ddl = document.getElementById("CreditDecisionReasonCode")?.ej2_instances?.[0];
    const notes = document.getElementById("FactoringDecisionReasonNotes")?.ej2_instances?.[0];
    if (!ddl) return;

    const persistedStatusCode = normalizeFactoringStatusCode(getPersistedFactoringStatusCode());
    const persistedReasonCode = getPersistedFactoringDecisionReasonCode();
    const allowed = new Set((status?.AllowedDecisionReasonCodes || []).map(x => String(x).toLowerCase()));
    let rows = (window.factoringDecisionReasons || []).filter(x =>
        allowed.has(String(x.CreditDecisionReasonCode || "").toLowerCase()));

    if (keepCurrent &&
        persistedReasonCode &&
        normalizeFactoringStatusCode(statusCode) === persistedStatusCode) {
        const persistedRow = (window.factoringDecisionReasons || []).find(x =>
            String(x.CreditDecisionReasonCode || "").toLowerCase() === persistedReasonCode.toLowerCase());
        if (persistedRow && !rows.some(x => x.CreditDecisionReasonCode === persistedRow.CreditDecisionReasonCode)) {
            rows = [persistedRow, ...rows];
        }
    }

    const current = keepCurrent
        ? (ddl.value || persistedReasonCode || null)
        : null;

    ddl.dataSource = rows;
    ddl.value = rows.some(x => x.CreditDecisionReasonCode === current) ? current : null;
    ddl.dataBind();

    if (notes) {
        if (!keepCurrent || !rows.length) notes.value = null;
        notes.dataBind();
    }

    if (typeof window.syncFactoringCaseFormMode === "function") {
        window.syncFactoringCaseFormMode(statusCode);
    }
};

window.refreshFactoringCaseWorkflowStatuses = function (preferredStatusCode) {
    const productCode = getFactoringSyncfusionValue("ProductCode");
    const currentStatusCode = preferredStatusCode || getPersistedFactoringStatusCode() || getFactoringSyncfusionValue("StatusCode");
    loadFactoringCaseWorkflowStatuses(productCode, currentStatusCode);
};

window.onFactoringCaseStatusChanged = function (statusCode) {
    const resolvedStatusCode = statusCode || getFactoringSyncfusionValue("StatusCode") || "";

    if (typeof window.syncFactoringCaseFormMode === "function") {
        window.syncFactoringCaseFormMode(resolvedStatusCode);
    }
    window.syncFactoringDecisionReasons(resolvedStatusCode, true);
};

window.setFactoringCaseWorkflowSteps = function (steps) {
    storeFactoringCaseWorkflowSteps(steps);

    if (typeof window.syncFactoringCaseFormMode === "function") {
        window.syncFactoringCaseFormMode(getFactoringSyncfusionValue("StatusCode") || getPersistedFactoringStatusCode() || "");
    }
};

const factoringRequestedToApprovedFieldMap = [
    ["RequestedStartDate", "ApprovedStartDate"],
    ["TermMonths", "ApprovedTermMonths"],
    ["RequestedMaturityDate", "ApprovedMaturityDate"],
    ["RequestedCreditAmount", "ApprovedCreditAmount"]
];

function factoringHasMeaningfulValue(value) {
    return !(value === undefined || value === null || value === "");
}

function factoringGetFieldValue(id) {
    const element = document.getElementById(id);
    if (!element) {
        return null;
    }

    const instance = element.ej2_instances?.[0];
    if (instance) {
        if (typeof instance.checked !== "undefined") {
            return instance.checked;
        }

        return instance.value;
    }

    if (element.matches("input, select, textarea")) {
        return element.value;
    }

    const input = element.querySelector("input, select, textarea");
    return input ? input.value : null;
}

function factoringSetFieldValue(id, value) {
    const element = document.getElementById(id);
    if (!element) {
        return;
    }

    const instance = element.ej2_instances?.[0];
    if (instance) {
        instance.value = value instanceof Date ? new Date(value.getTime()) : value;
        instance.dataBind?.();
        return;
    }

    element.value = value ?? "";
}

function copyFactoringRequestedTermsToApproved(overwrite) {
    factoringRequestedToApprovedFieldMap.forEach(function (pair) {
        const requestedValue = factoringGetFieldValue(pair[0]);
        const approvedValue = factoringGetFieldValue(pair[1]);

        if (factoringHasMeaningfulValue(requestedValue) &&
            (overwrite || !factoringHasMeaningfulValue(approvedValue))) {
            factoringSetFieldValue(pair[1], requestedValue);
        }
    });
}

function factoringToDateObject(value) {
    if (value instanceof Date && !Number.isNaN(value.getTime())) {
        return new Date(value.getTime());
    }

    if (!value) {
        return null;
    }

    const text = String(value).trim();
    const eu = text.match(/^(\d{1,2})[./-](\d{1,2})[./-](\d{4})$/);
    const iso = eu
        ? `${eu[3]}-${eu[2].padStart(2, "0")}-${eu[1].padStart(2, "0")}`
        : text.substring(0, 10);
    const parts = iso.split("-");

    if (parts.length !== 3) {
        return null;
    }

    return new Date(Number(parts[0]), Number(parts[1]) - 1, Number(parts[2]));
}

function factoringAddMonthsSafe(date, months) {
    const result = new Date(date.getTime());
    result.setMonth(result.getMonth() + months);
    return result;
}

function calculateFactoringMaturityFromStartDate(prefix, termFieldId) {
    const startDatePicker = document.getElementById(prefix + "StartDate")?.ej2_instances?.[0];
    const termInput = document.getElementById(termFieldId)?.ej2_instances?.[0];
    const endDatePicker = document.getElementById(prefix + "MaturityDate")?.ej2_instances?.[0];

    if (!startDatePicker || !termInput || !endDatePicker) {
        return;
    }

    const startDate = factoringToDateObject(startDatePicker.value);
    const months = Number(termInput.value);

    if (!startDate || !Number.isFinite(months) || months <= 0) {
        return;
    }

    endDatePicker.value = factoringAddMonthsSafe(startDate, months);
    endDatePicker.dataBind?.();
}

function calculateFactoringTermMonthsFromDates(prefix, termFieldId) {
    const startDatePicker = document.getElementById(prefix + "StartDate")?.ej2_instances?.[0];
    const termInput = document.getElementById(termFieldId)?.ej2_instances?.[0];
    const endDatePicker = document.getElementById(prefix + "MaturityDate")?.ej2_instances?.[0];

    if (!startDatePicker || !termInput || !endDatePicker) {
        return;
    }

    const startDate = factoringToDateObject(startDatePicker.value);
    const endDate = factoringToDateObject(endDatePicker.value);
    if (!startDate || !endDate || endDate <= startDate) {
        return;
    }

    let months = ((endDate.getFullYear() - startDate.getFullYear()) * 12) +
        endDate.getMonth() - startDate.getMonth();
    if (endDate.getDate() > startDate.getDate()) {
        months++;
    }

    termInput.value = Math.max(1, months);
    termInput.dataBind?.();
}

window.calculateFactoringRequestedEndDate = function () {
    calculateFactoringMaturityFromStartDate("Requested", "TermMonths");
    copyFactoringRequestedTermsToApproved(true);
};

window.onFactoringRequestedStartDateChange = function () {
    window.calculateFactoringRequestedEndDate?.();
};

window.onFactoringRequestedMaturityDateChange = function () {
    calculateFactoringTermMonthsFromDates("Requested", "TermMonths");
    copyFactoringRequestedTermsToApproved(true);
};

window.calculateFactoringApprovedEndDate = function () {
    calculateFactoringMaturityFromStartDate("Approved", "ApprovedTermMonths");
};

window.onFactoringApprovedStartDateChange = function () {
    window.calculateFactoringApprovedEndDate?.();
};

window.onFactoringApprovedMaturityDateChange = function () {
    calculateFactoringTermMonthsFromDates("Approved", "ApprovedTermMonths");
};

function bindFactoringChange(id, handler) {
    const element = document.getElementById(id);
    const instance = element?.ej2_instances?.[0];
    if (!instance) {
        return false;
    }

    const previous = instance.change;
    instance.change = function (args) {
        if (typeof previous === "function") {
            previous.call(this, args);
        }

        handler(args);
    };
    instance.dataBind?.();
    return true;
}

function bindFactoringTermsHandlers() {
    bindFactoringChange("RequestedStartDate", function () {
        window.onFactoringRequestedStartDateChange?.();
    });
    bindFactoringChange("TermMonths", function () {
        window.calculateFactoringRequestedEndDate?.();
    });
    bindFactoringChange("RequestedMaturityDate", function () {
        window.onFactoringRequestedMaturityDateChange?.();
    });
    bindFactoringChange("ApprovedStartDate", function () {
        window.onFactoringApprovedStartDateChange?.();
    });
    bindFactoringChange("ApprovedTermMonths", function () {
        window.calculateFactoringApprovedEndDate?.();
    });
    bindFactoringChange("ApprovedMaturityDate", function () {
        window.onFactoringApprovedMaturityDateChange?.();
    });

    ["TermMonths", "ApprovedTermMonths"].forEach(function (id) {
        const element = document.getElementById(id);
        element?.addEventListener("input", function () {
            queueMicrotask(function () {
                if (id === "TermMonths") {
                    window.calculateFactoringRequestedEndDate?.();
                } else {
                    window.calculateFactoringApprovedEndDate?.();
                }
            });
        });
    });
}

window.initializeFactoringTerms = function (attempt) {
    attempt = attempt || 0;

    const requiredIds = [
        "RequestedStartDate",
        "TermMonths",
        "RequestedMaturityDate",
        "ApprovedStartDate",
        "ApprovedTermMonths",
        "ApprovedMaturityDate"
    ];
    const allReady = requiredIds.every(function (id) {
        return !!document.getElementById(id)?.ej2_instances?.[0];
    });

    if (!allReady && attempt < 20) {
        setTimeout(function () {
            window.initializeFactoringTerms(attempt + 1);
        }, 50);
        return;
    }

    bindFactoringTermsHandlers();
    window.calculateFactoringRequestedEndDate?.();
};

window.initializeFactoringDecisionReasons = function (attempt) {
    attempt = attempt || 0;

    const statusReady = !!document.getElementById("StatusCode")?.ej2_instances?.[0];
    const decisionReady = !!document.getElementById("CreditDecisionReasonCode")?.ej2_instances?.[0];

    if ((!statusReady || !decisionReady) && attempt < 20) {
        setTimeout(function () {
            window.initializeFactoringDecisionReasons(attempt + 1);
        }, 50);
        return;
    }

    const statusCode = getFactoringSyncfusionValue("StatusCode")
        || getPersistedFactoringStatusCode()
        || "";

    if (typeof window.onFactoringCaseStatusChanged === "function") {
        window.onFactoringCaseStatusChanged(statusCode);
    }
};

(function initializeFactoringPolicyOptions() {
    if (!Array.isArray(window.factoringPolicyOptions) || !window.factoringPolicyOptions.length) {
        return;
    }

    window.factoringPolicyOptions = mapFactoringPolicyOptions(window.factoringPolicyOptions);
    const policyDdl = document.getElementById("FactoringPolicyCode")?.ej2_instances?.[0];
    if (policyDdl?.dataSource?.length) {
        policyDdl.dataSource = window.factoringPolicyOptions;
        policyDdl.dataBind();
    }
})();
