var loanPricingList = [];
var selectedPriceListId = null;
var loanPenaltyInterestRateMaximum = null;
var loanCaseWorkflowSteps = Array.isArray(window.loanCaseWorkflowSteps) ? window.loanCaseWorkflowSteps : [];

var loanCaseStatusRequestVersion = 0;
var loanCaseStatusEndpoint = "/Credit/CreditCaseLoan/GetNextCaseStatusesForDropdown";
var loanDecisionReasonsEndpoint = "/Credit/CreditCaseLoan/GetDecisionReasons";
var loanDecisionReasons = [];
var loanDecisionReasonsPromise = null;

function getSyncfusionValue(id) {
    return document.getElementById(id)?.ej2_instances?.[0]?.value ?? null;
}

function toLoanNumber(value) {
    if (value === undefined || value === null || value === "") {
        return null;
    }

    const parsed = Number(String(value).replace(/,/g, ""));
    return Number.isFinite(parsed) ? parsed : null;
}

function calculateLoanChargeAmount(prefix) {
    const amount = toLoanNumber(getSyncfusionValue(`${prefix}CreditAmount`));
    const rate = toLoanNumber(getSyncfusionValue(`${prefix}ChargeRate`));

    if (!amount || amount <= 0 || rate === null) {
        return;
    }

    setNumericValue(`${prefix}ChargeAmount`, Math.round((amount * rate / 100 + Number.EPSILON) * 100) / 100);
}

function getPersistedLoanStatusCode() {
    return document.getElementById("CurrentStatusCode")?.value ?? "";
}

function storeLoanCaseWorkflowSteps(steps) {
    loanCaseWorkflowSteps = Array.isArray(steps) ? steps : [];
    window.loanCaseWorkflowSteps = loanCaseWorkflowSteps;
}

function updateLoanDisbursementSectionVisibility(statusCode) {
    const section = document.getElementById("loanDisbursementSection");
    if (!section) {
        return;
    }

    section.classList.toggle("d-none", statusCode !== "DISBURSED");
}

function getLoanStatusOption(statusCode) {
    const ddl = document.getElementById("StatusCode")?.ej2_instances?.[0];
    const rows = Array.isArray(ddl?.dataSource) ? ddl.dataSource : [];
    return rows.find(x => String(x.CaseStatusCode || "").toLowerCase() ===
        String(statusCode || "").toLowerCase());
}

function updateLoanDetailsVisibility(statusCode) {
    const card = document.getElementById("loanDetailsCard");
    if (!card) return;

    const categoryCode = getLoanStatusOption(statusCode)?.CategoryStatusCode || "";
    card.classList.toggle("d-none",
        card.dataset.isAdd === "true" || categoryCode.toUpperCase() !== "COMPLETED");
}

function loadLoanDecisionReasons() {
    if (loanDecisionReasonsPromise) return loanDecisionReasonsPromise;

    loanDecisionReasonsPromise = fetch(loanDecisionReasonsEndpoint, {
        headers: { Accept: "application/json" }
    })
        .then(response => response.ok ? response.json() : [])
        .then(rows => loanDecisionReasons = Array.isArray(rows) ? rows : [])
        .catch(() => loanDecisionReasons = []);
    return loanDecisionReasonsPromise;
}

function applyLoanDecisionReasonFilter(statusCode, keepCurrentValue) {
    const ddl = document.getElementById("CreditDecisionReasonCode")?.ej2_instances?.[0];
    const notes = document.getElementById("LoanDecisionReasonNotes")?.ej2_instances?.[0];
    if (!ddl) return;

    const status = getLoanStatusOption(statusCode);
    const configuredCodes = status?.AllowedDecisionReasonCodes;
    const allowedCodes = Array.isArray(configuredCodes) ? configuredCodes : [];
    const allowed = new Set(allowedCodes.map(x => String(x).toLowerCase()));
    const filtered = loanDecisionReasons.filter(x => allowed.has(String(
        x.CreditDecisionReasonCode || "").toLowerCase()));
    const currentValue = keepCurrentValue ? ddl.value : null;

    ddl.dataSource = filtered;
    ddl.enabled = filtered.length > 0;
    ddl.value = currentValue && filtered.some(x =>
        x.CreditDecisionReasonCode === currentValue) ? currentValue : null;
    ddl.dataBind();

    if (notes) {
        notes.enabled = filtered.length > 0;
        if (!filtered.length) notes.value = null;
        notes.dataBind();
    }
}

function syncLoanStatusUi(statusCode, keepDecisionReason) {
    updateLoanDisbursementSectionVisibility(statusCode);
    updateLoanDetailsVisibility(statusCode);
    loadLoanDecisionReasons().then(() =>
        applyLoanDecisionReasonFilter(statusCode, keepDecisionReason));
}

function applyLoanCaseStatuses(statuses, preferredStatusCode) {
    const ddl = document.getElementById("StatusCode")?.ej2_instances?.[0];
    if (!ddl) {
        return;
    }

    const items = (Array.isArray(statuses) ? statuses : []).map(item => ({
        ...item,
        CaseStatusCode: item.CaseStatusCode ?? item.caseStatusCode ?? item.StatusCode ?? item.statusCode,
        CaseStatusName: item.CaseStatusName ?? item.caseStatusName ?? item.StatusName ?? item.statusName,
        CategoryStatusCode: item.CategoryStatusCode ?? item.categoryStatusCode,
        Action: item.Action ?? item.action,
        SetEntityActive: item.SetEntityActive ?? item.setEntityActive,
        RequiresDecisionReason: item.RequiresDecisionReason ?? item.requiresDecisionReason,
        AllowedDecisionReasonCodes: item.AllowedDecisionReasonCodes ?? item.allowedDecisionReasonCodes
    }));
    ddl.fields = { text: "CaseStatusName", value: "CaseStatusCode" };
    ddl.dataSource = items;

    let resolvedValue = preferredStatusCode || ddl.value || null;
    if (!resolvedValue || !items.some(x => x.CaseStatusCode === resolvedValue)) {
        resolvedValue = items.length > 0 ? items[0].CaseStatusCode : null;
    }

    ddl.value = resolvedValue;
    ddl.dataBind();

    syncLoanStatusUi(resolvedValue || "", true);
}

function applyLoanWorkflowDefinition(payload, preferredStatusCode) {
    if (Array.isArray(payload)) {
        applyLoanCaseStatuses(payload, preferredStatusCode);
    } else {
        storeLoanCaseWorkflowSteps((payload?.workflowSteps ?? payload?.WorkflowSteps ?? []).map(step => ({
            ...step,
            CaseStatusCode: step.CaseStatusCode ?? step.caseStatusCode,
            IsEditable: step.IsEditable ?? step.isEditable,
            AllowsStatusChange: step.AllowsStatusChange ?? step.allowsStatusChange,
            IsTerminal: step.IsTerminal ?? step.isTerminal
        })));
        applyLoanCaseStatuses(payload?.statuses ?? payload?.Statuses ?? [], preferredStatusCode);
    }

    if (typeof window.syncLoanCaseFormMode === "function") {
        window.syncLoanCaseFormMode(getSyncfusionValue("StatusCode") || preferredStatusCode || "");
    }
}

function loadLoanCaseWorkflowStatuses(workflowCode, currentStatusCode) {
    const requestVersion = ++loanCaseStatusRequestVersion;
    if (!workflowCode) {
        storeLoanCaseWorkflowSteps([]);
        applyLoanCaseStatuses([], null);
        return;
    }

    return $.ajax({
        url: document.getElementById("LoanCaseStatusEndpoint")?.value || loanCaseStatusEndpoint,
        type: "GET",
        data: {
            workflowCode: workflowCode,
            currentStatusCode: currentStatusCode || ""
        },
        success: function (payload) {
            if (requestVersion !== loanCaseStatusRequestVersion) return;
            if (!Array.isArray(payload) && !Array.isArray(payload?.statuses) && !Array.isArray(payload?.Statuses)) {
                console.error("Invalid loan workflow status response.");
                return;
            }
            applyLoanWorkflowDefinition(payload, currentStatusCode);
        },
        error: function (xhr) {
            console.error("Failed to load loan case statuses:", {
                url: loanCaseStatusEndpoint,
                status: xhr.status,
                response: xhr.responseText
            });
        }
    });
}

let activeLoanProductLimits = null;

function applyLoanProductLimits(product) {
    activeLoanProductLimits = product ? {
        amountMin: toLoanNumber(product.AmountMin),
        amountMax: toLoanNumber(product.AmountMax),
        termMonthsMin: toLoanNumber(product.TermMonthsMin),
        termMonthsMax: toLoanNumber(product.TermMonthsMax)
    } : null;

    const limitsHint = document.getElementById("LoanProductLimitsHint");
    if (!limitsHint) return;

    if (!product) {
        limitsHint.textContent = "";
        window.validateAllLoanProductLimits?.();
        return;
    }

    const formatAmount = function (value) {
        if (value === null || value === undefined || value === "") return "—";
        const number = Number(value);
        return Number.isFinite(number)
            ? number.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })
            : "—";
    };

    const currency = product.CurrencyCode ? ` ${product.CurrencyCode}` : "";
    const amountRange = `${formatAmount(product.AmountMin)}–${formatAmount(product.AmountMax)}${currency}`;
    const durationRange = `${product.TermMonthsMin ?? "—"}–${product.TermMonthsMax ?? "—"} months`;
    limitsHint.textContent = `Amount: ${amountRange} · Duration: ${durationRange}`;

    window.validateAllLoanProductLimits?.();
}

function setLoanProductValidationMessage(fieldId, message) {
    const errorElement = document.getElementById(fieldId + "ProductError");
    if (!errorElement) return;

    errorElement.textContent = message || "";
    errorElement.classList.toggle("d-none", !message);
}

window.validateLoanProductField = function (fieldId) {
    if (!activeLoanProductLimits) {
        setLoanProductValidationMessage(fieldId, "");
        return true;
    }

    const value = toLoanNumber(getSyncfusionValue(fieldId));
    if (value === null) {
        setLoanProductValidationMessage(fieldId, "");
        return true;
    }

    const isAmount = fieldId === "RequestedCreditAmount" || fieldId === "ApprovedCreditAmount";
    const minimum = isAmount
        ? activeLoanProductLimits.amountMin
        : activeLoanProductLimits.termMonthsMin;
    const maximum = isAmount
        ? activeLoanProductLimits.amountMax
        : activeLoanProductLimits.termMonthsMax;
    const label = isAmount ? "Credit amount" : "Months";

    let message = "";
    if (minimum !== null && value < minimum) {
        message = `${label} cannot be less than ${minimum.toLocaleString()}.`;
    } else if (maximum !== null && value > maximum) {
        message = `${label} cannot exceed ${maximum.toLocaleString()}.`;
    }

    setLoanProductValidationMessage(fieldId, message);
    return !message;
};

window.validateAllLoanProductLimits = function () {
    const results = [
        "RequestedCreditAmount",
        "ApprovedCreditAmount",
        "TermMonths",
        "ApprovedTermMonths"
    ].map(function (fieldId) {
        return window.validateLoanProductField(fieldId);
    });

    return results.every(Boolean);
};

window.onProductChange = function (args) {
    const productCode = getSyncfusionValue("ProductCode");
    const preserveExistingSelection = args?.preserveExistingSelection === true;
    const preferredPurposeCode = preserveExistingSelection
        ? (args?.preferredPurposeCode ?? getSyncfusionValue("CreditPurposeCode"))
        : null;
    const preferredPriceListId = preserveExistingSelection
        ? toLoanNumber(args?.preferredPriceListId ?? getSyncfusionValue("PriceListId"))
        : null;
    const productDropdown = document.getElementById("ProductCode")?.ej2_instances?.[0];
    const selectedProduct = args?.itemData
        ?? productDropdown?.itemData
        ?? productDropdown?.dataSource?.find?.(x => x.ProductCode === productCode);
    const workflowCode = selectedProduct?.WorkflowCode || "";
    const productTypeCode = selectedProduct?.ProductTypeCode || "";
    const productPurposes = selectedProduct?.CreditProductPurposes || [];
    const statusCode = getPersistedLoanStatusCode();

    applyLoanProductLimits(selectedProduct);

    const productTypeCodeInput = document.getElementById("ProductTypeCode");
    if (productTypeCodeInput) {
        productTypeCodeInput.value = productTypeCode;
    }

    applyLoanProductPurposes(productPurposes, preferredPurposeCode);
    loadLoanCaseWorkflowStatuses(workflowCode, statusCode);

    if (!productCode) return;

    $.ajax({
        url: "/CreditPriceList/CreditPriceList/GetPriceListsByProduct",
        type: "GET",
        data: { productCode: productCode },
        success: function (priceLists) {
            const priceListDropdown = document.getElementById("PriceListId")?.ej2_instances?.[0];
            if (!priceListDropdown) return;

            if (!priceLists || priceLists.length === 0) {
                priceListDropdown.dataSource = [];
                priceListDropdown.value = null;
                priceListDropdown.dataBind();
                console.warn("No price lists found.");
                return;
            }

            priceListDropdown.dataSource = priceLists;

            if (preferredPriceListId !== null && priceLists.some(x =>
                Number(x.PriceListId) === preferredPriceListId)) {
                selectedPriceListId = preferredPriceListId;
                priceListDropdown.value = preferredPriceListId;
                priceListDropdown.dataBind();
                loadPricing(productCode, preferredPriceListId);
            }
            else if (priceLists.length === 1) {
                selectedPriceListId = priceLists[0].PriceListId;
                priceListDropdown.value = selectedPriceListId;
                priceListDropdown.dataBind();
                loadPricing(productCode, selectedPriceListId);
            }
            else {
                selectedPriceListId = null;
                priceListDropdown.value = null;
                priceListDropdown.dataBind();
            }
        },
        error: function (xhr) {
            console.error("Failed to load price lists for product:", {
                url: "/CreditPriceList/CreditPriceList/GetPriceListsByProduct",
                productCode: productCode,
                status: xhr.status,
                response: xhr.responseText
            });
        }
    });
};

function loadPricing(productCode, priceListId) {
    $.ajax({
        url: "/CreditPriceList/CreditPriceListItemLoan/SelectLoanPriceListByProduct",
        type: "GET",
        data: {
            productCode: productCode,
            priceListId: priceListId
        },
        success: function (data) {
            loanPricingList = data || [];
            window.onAmountChange();
        },
        error: function (xhr) {
            console.error("Failed to load loan price-list items:", {
                url: "/CreditPriceList/CreditPriceListItemLoan/SelectLoanPriceListByProduct",
                productCode: productCode,
                priceListId: priceListId,
                status: xhr.status,
                response: xhr.responseText
            });
        }
    });
}

window.onPriceListChange = function (args) {
    const productCode = getSyncfusionValue("ProductCode");
    const priceListId = args.value;

    if (!productCode || !priceListId) return;

    selectedPriceListId = priceListId;
    loadPricing(productCode, priceListId);
};

function findLoanPricingForAmount(amount) {
    if (!loanPricingList || loanPricingList.length === 0)
        return null;
    if (!amount || amount <= 0) return null;

    return loanPricingList
        .filter(x =>
            amount >= x.AmountFrom &&
            amount <= x.AmountTo
        )
        .sort((a, b) => a.SortOrder - b.SortOrder)[0];
}

function applyLoanPricingToTerms(prefix, amount) {
    const match = findLoanPricingForAmount(amount);

    if (!match) {
        console.warn("No pricing match found.");
        return false;
    }

    setNumericValue(`${prefix}InterestRate`, match.LoanNominalInterestRate);
    setNumericValue(`${prefix}ChargeRate`, match.LoanDisbursementFeePercent);
    setNumericValue(`${prefix}EffectiveInterestRate`, match.LoanEffectiveInterestRate);
    setLoanPenaltyInterestRate(match.LoanPenaltyInterestRate);

    const minimumFeeInput = document.getElementById(`${prefix}DisbursementFeeMinimumAmount`);
    if (minimumFeeInput) {
        minimumFeeInput.value = match.LoanDisbursementFeeMinimumAmount ?? 0;
    }

    const calculatedFeeAmount = (amount * match.LoanDisbursementFeePercent) / 100;
    const feeAmount = match.LoanDisbursementFeePercent === 0
        ? 0
        : match.LoanDisbursementFeeMinimumAmount
            ? Math.max(calculatedFeeAmount, match.LoanDisbursementFeeMinimumAmount)
            : calculatedFeeAmount;

    setNumericValue(`${prefix}ChargeAmount`, feeAmount);
    return true;
}

window.applyLoanPricingForAmount = function (prefix, currentAmount) {
    const amount = toLoanNumber(
        currentAmount !== undefined
            ? currentAmount
            : getSyncfusionValue(`${prefix}CreditAmount`));
    return applyLoanPricingToTerms(prefix, amount);
};

window.onAmountChange = function (args) {
    const amount = toLoanNumber(
        args?.value !== undefined
            ? args.value
            : getSyncfusionValue("RequestedCreditAmount"));
    window.validateLoanProductField?.("RequestedCreditAmount");
    if (!applyLoanPricingToTerms("Requested", amount)) return;

    if (typeof window.copyLoanRequestedTermsToApproved === "function") {
        window.copyLoanRequestedTermsToApproved();
    }

    if (typeof window.onLoanTermsChanged === "function") {
        window.onLoanTermsChanged("Requested");
    }
};

window.onApprovedAmountChange = function (args) {
    const amount = toLoanNumber(
        args?.value !== undefined
            ? args.value
            : getSyncfusionValue("ApprovedCreditAmount"));
    window.validateLoanProductField?.("ApprovedCreditAmount");
    if (!applyLoanPricingToTerms("Approved", amount)) return;

    if (typeof window.onLoanTermsChanged === "function") {
        window.onLoanTermsChanged("Approved");
    }
};

window.onApprovedChargeRateChange = function (args) {
    const amount = toLoanNumber(getSyncfusionValue("ApprovedCreditAmount"));
    const chargeRate = toLoanNumber(
        args?.value !== undefined
            ? args.value
            : getSyncfusionValue("ApprovedChargeRate"));
    const minimumFee = toLoanNumber(
        document.getElementById("ApprovedDisbursementFeeMinimumAmount")?.value);

    if (amount <= 0) {
        setNumericValue("ApprovedChargeAmount", 0);
        return;
    }

    const percentageFee = Math.round((amount * chargeRate / 100) * 100) / 100;
    const appliedFee = chargeRate === 0
        ? 0
        : Math.max(percentageFee, minimumFee);
    setNumericValue("ApprovedChargeAmount", appliedFee);

    if (typeof window.onLoanTermsChanged === "function") {
        window.onLoanTermsChanged("Approved");
    }
};

function setLoanPenaltyInterestRate(value) {
    const penalty = toLoanNumber(value) ?? 0;
    const comp = document.getElementById("ApprovedPenaltyInterest")?.ej2_instances?.[0];
    loanPenaltyInterestRateMaximum = penalty;

    if (!comp) return;

    comp.max = penalty ?? undefined;
    comp.value = penalty;
    comp.dataBind();
}

window.onApprovedPenaltyInterestChange = function (args) {
    if (loanPenaltyInterestRateMaximum === null) return;

    const value = toLoanNumber(args?.value);
    if (value !== null && value > loanPenaltyInterestRateMaximum) {
        setNumericValue("ApprovedPenaltyInterest", loanPenaltyInterestRateMaximum);
    }
};

function setNumericValue(id, value) {
    const comp = document.getElementById(id)?.ej2_instances?.[0];
    if (comp) {
        comp.value = value;
        comp.dataBind();
    }
}

window.calculateLoanChargeAmount = calculateLoanChargeAmount;

window.onCaseStatusChange = function (args) {
    const statusCode = args?.value || "";
    syncLoanStatusUi(statusCode, false);

    if (typeof window.syncLoanCaseFormMode === "function") {
        window.syncLoanCaseFormMode(statusCode);
        requestAnimationFrame(function () {
            window.syncLoanCaseFormMode(statusCode);
        });
        setTimeout(function () {
            window.syncLoanCaseFormMode(statusCode);
        }, 50);
    }
};

window.refreshLoanCaseWorkflowStatuses = function (preferredStatusCode) {
    const productDropdown = document.getElementById("ProductCode")?.ej2_instances?.[0];
    const productCode = productDropdown?.value;
    const selectedProduct = productDropdown?.itemData
        ?? productDropdown?.dataSource?.find?.(x => x.ProductCode === productCode);
    const workflowCode = selectedProduct?.WorkflowCode || "";
    const currentStatusCode = preferredStatusCode || getPersistedLoanStatusCode() || getSyncfusionValue("StatusCode");
    return loadLoanCaseWorkflowStatuses(workflowCode, currentStatusCode);
};

function applyLoanProductPurposes(purposes, preferredPurposeCode) {
    const purposeDropdown = document.getElementById("CreditPurposeCode")?.ej2_instances?.[0];
    if (!purposeDropdown) return;

    const rows = (Array.isArray(purposes) ? purposes : []).map(row => ({
        ...row,
        CreditPurposeCode: row.CreditPurposeCode ?? row.creditPurposeCode,
        CreditPurposeName: row.CreditPurposeName ?? row.creditPurposeName
    }));
    // An incomplete asynchronous product load must not clear an existing valid choice.
    if (!rows.length && preferredPurposeCode && Array.isArray(purposeDropdown.dataSource)) {
        const current = purposeDropdown.dataSource.find(x => x.CreditPurposeCode === preferredPurposeCode);
        if (current) rows.push(current);
    }
    purposeDropdown.fields = { text: "CreditPurposeName", value: "CreditPurposeCode" };
    purposeDropdown.dataSource = rows;
    purposeDropdown.value = preferredPurposeCode && rows.some(x =>
        x.CreditPurposeCode === preferredPurposeCode)
        ? preferredPurposeCode
        : null;
    purposeDropdown.dataBind();
}

window.onLoanCaseStatusChanged = function (statusCode) {
    const resolvedStatusCode = statusCode || getSyncfusionValue("StatusCode") || "";
    syncLoanStatusUi(resolvedStatusCode, true);

    if (typeof window.syncLoanCaseFormMode === "function") {
        window.syncLoanCaseFormMode(resolvedStatusCode);
    }
};

window.setLoanCaseWorkflowSteps = function (steps) {
    storeLoanCaseWorkflowSteps(steps);

    if (typeof window.syncLoanCaseFormMode === "function") {
        window.syncLoanCaseFormMode(getSyncfusionValue("StatusCode") || getPersistedLoanStatusCode() || "");
    }
};
