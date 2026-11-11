window.initCreditCaseFactoringInvoiceForm = function (config) {
    const form = document.getElementById(config.formDomId);
    if (!form) {
        return;
    }

    const state = {
        form,
        config,
        persistedStatus: "",
        pricingTimer: null,
        pricingRequestId: 0,
        priceListItems: [],
        pricingMessage: "",
        counterpartyPartyMap: {},
        factoringRates: {
            advanceRatePercent: 100,
            retentionRatePercent: 0,
            dilutionRatePercent: 0
        }
    };

    function getFormSessionId() {
        return form.querySelector(".form-container")?.dataset?.formSessionId || config.formSessionId || "";
    }

    function getFkData() {
        return window.ds?.entity?.fkData?.[getFormSessionId()] || {};
    }

    function getFkRows(key) {
        const data = getFkData();
        const normalized = String(key || "").startsWith("FK_") ? key : "FK_" + key;
        return data[normalized] || [];
    }

    function getTypePolicies() {
        return getFkRows("RecoursePolicyCode");
    }

    function getFactoringTypes() {
        return getFkRows("FactoringTypeCode");
    }

    function getRiskBearerCatalog() {
        return getFkRows("RiskBearerCode");
    }

    function field(id) {
        return form.querySelector("#" + id);
    }

    function pick(row, name) {
        if (!row) {
            return null;
        }

        if (row[name] !== undefined && row[name] !== null) {
            return row[name];
        }

        const camel = name.charAt(0).toLowerCase() + name.slice(1);
        return row[camel] ?? null;
    }

    function value(id) {
        const el = field(id);
        if (!el) {
            return null;
        }

        const ej = el.ej2_instances?.[0];
        if (ej && typeof window.ds?.entity?.normalizeValue === "function") {
            return window.ds.entity.normalizeValue(ej, el);
        }

        if (el.type === "checkbox") {
            return el.checked;
        }

        return el.value;
    }

    function setValue(id, next) {
        const el = field(id);
        if (!el) {
            return;
        }

        const ej = el.ej2_instances?.[0];
        if (ej && typeof ej.getModuleName === "function") {
            const moduleName = ej.getModuleName();
            if (moduleName === "datepicker" || moduleName === "datetimepicker") {
                if (!next || isEmptyDate(next)) {
                    ej.value = null;
                } else {
                    const parsed = window.ds?.input?.parseDateOnly?.(next);
                    ej.value = parsed || null;
                }
                ej.dataBind?.();
                return;
            }

            if (typeof ej.decimals === "number") {
                ej.value = next === "" || next === null || next === undefined ? null : Number(next);
                ej.dataBind?.();
                return;
            }
        }

        if (el.type === "checkbox") {
            el.checked = !!next;
            return;
        }

        el.value = next ?? "";
    }

    function populateSelect(id, rows, valueField, textField, selectedValue) {
        const select = field(id);
        if (!select) {
            return;
        }

        select.innerHTML = '<option value=""></option>';
        (rows || []).forEach(row => {
            const option = document.createElement("option");
            option.value = pick(row, valueField) || "";
            option.textContent = pick(row, textField) || pick(row, valueField) || "";
            select.appendChild(option);
        });

        select.value = selectedValue || "";
    }

    function normalizeStatusCode(next) {
        return String(next || "").trim().toUpperCase();
    }

    function emptyToNull(next) {
        return next && String(next).trim() ? String(next).trim() : null;
    }

    function isEmptyDate(next) {
        if (!next) {
            return true;
        }

        const text = String(next).substring(0, 10);
        return text === "0001-01-01" || text === "01/01/0001";
    }

    function toIsoDate(next) {
        if (!next || isEmptyDate(next)) {
            return null;
        }

        if (next instanceof Date && !Number.isNaN(next.getTime())) {
            return `${next.getFullYear()}-${String(next.getMonth() + 1).padStart(2, "0")}-${String(next.getDate()).padStart(2, "0")}`;
        }

        const parsed = window.ds?.input?.parseDateOnly?.(next);
        if (parsed) {
            return `${parsed.getFullYear()}-${String(parsed.getMonth() + 1).padStart(2, "0")}-${String(parsed.getDate()).padStart(2, "0")}`;
        }

        if (/^\d{4}-\d{2}-\d{2}$/.test(String(next))) {
            return String(next).substring(0, 10);
        }

        const match = /^(\d{1,2})[./-](\d{1,2})[./-](\d{4})$/.exec(String(next));
        if (!match) {
            return null;
        }

        return `${match[3]}-${match[2].padStart(2, "0")}-${match[1].padStart(2, "0")}`;
    }

    function toEuDate(next) {
        if (!next || isEmptyDate(next)) {
            return "";
        }

        const text = String(next).substring(0, 10);
        const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(text);
        if (!match) {
            return next;
        }

        return `${match[3]}/${match[2]}/${match[1]}`;
    }

    function toNumberOrNull(next) {
        if (next === "" || next === null || next === undefined) {
            return null;
        }

        const parsed = Number(next);
        return Number.isFinite(parsed) ? parsed : null;
    }

    function formatMoney(next) {
        if (next === null || next === undefined || Number.isNaN(Number(next))) {
            return "";
        }

        return Number(next).toFixed(2);
    }

    function roundMoney(next) {
        const parsed = Number(next);
        if (!Number.isFinite(parsed)) {
            return 0;
        }

        return Math.round(parsed * 100) / 100;
    }

    function getFactoringRates() {
        return state.factoringRates;
    }

    function formatTenorDays(days) {
        return days === 1 ? "1 ditë" : `${days} ditë`;
    }

    function calculateDaysBetween(isoFrom, isoTo) {
        if (!isoFrom || !isoTo) {
            return null;
        }

        const from = new Date(`${isoFrom}T00:00:00`);
        const to = new Date(`${isoTo}T00:00:00`);
        if (Number.isNaN(from.getTime()) || Number.isNaN(to.getTime()) || to <= from) {
            return null;
        }

        return Math.round((to - from) / 86400000);
    }

    function setTenorLabel(key, days, suffix) {
        const el = form.querySelector(`[data-invoice-tenor="${key}"]`);
        if (!el) {
            return;
        }

        el.textContent = days != null ? `${formatTenorDays(days)} ${suffix}` : "";
    }

    function refreshInvoiceTenorDisplay() {
        const issue = toIsoDate(value("InvoiceIssueDate"));
        const due = toIsoDate(value("InvoiceDueDate"));
        const bank = toIsoDate(value("BankInvoiceDueDate"));
        const disbursement = toIsoDate(value("DisbursementDate"));
        const issueToDue = calculateDaysBetween(issue, due);
        const disbursementToBank = calculateDaysBetween(disbursement, bank);

        setTenorLabel("issue-to-due", issueToDue, "nga lëshimi");
        setTenorLabel("disbursement-to-bank", disbursementToBank, "interes (disbursim → bank due)");

        const summary = form.querySelector("[data-invoice-tenor='summary']");
        if (summary) {
            const parts = [];
            if (issueToDue != null) {
                parts.push(`Lëshim → Pagesë: ${formatTenorDays(issueToDue)}`);
            }

            if (disbursementToBank != null) {
                parts.push(`Disbursim → Bank Due: ${formatTenorDays(disbursementToBank)} (interes)`);
            }

            summary.textContent = parts.join(" | ");
        }
    }

    function syncBankDueFromInvoiceDue() {
        const dueDate = toIsoDate(value("InvoiceDueDate"));
        if (!dueDate) {
            return;
        }

        setValue("BankInvoiceDueDate", dueDate);
    }

    function syncBankDueIfEmpty() {
        if (toIsoDate(value("BankInvoiceDueDate"))) {
            return;
        }

        syncBankDueFromInvoiceDue();
    }

    function syncDefaultDisbursementDate() {
        const current = toIsoDate(value("DisbursementDate"));
        if (current) {
            return;
        }

        const today = new Date();
        setValue("DisbursementDate", today);
    }

    function syncRequestedAmountConstraints() {
        const invoiceAmount = toNumberOrNull(value("InvoiceAmount"));
        const requestedEl = field("RequestedAmount");
        const requestedInstance = document.getElementById("RequestedAmount")?.ej2_instances?.[0];
        const hint = form.querySelector("[data-requested-amount-hint]");

        if (requestedInstance && invoiceAmount > 0) {
            requestedInstance.max = invoiceAmount;
            requestedInstance.dataBind?.();
        } else if (requestedEl && invoiceAmount > 0) {
            requestedEl.max = String(invoiceAmount);
        } else if (requestedEl) {
            requestedEl.removeAttribute("max");
        }

        if (hint) {
            hint.textContent = invoiceAmount > 0
                ? `Maksimumi: ${formatMoney(invoiceAmount)} (shuma e faturës)`
                : "Maksimumi: shuma e faturës";
        }
    }

    function validateRequestedAmount() {
        const requestedEl = field("RequestedAmount");
        const requestedWrapper = requestedEl?.closest(".e-control-wrapper");
        const invoiceAmount = toNumberOrNull(value("InvoiceAmount"));
        let requested = toNumberOrNull(value("RequestedAmount"));

        if (invoiceAmount > 0 && requested != null && roundMoney(requested) > roundMoney(invoiceAmount)) {
            requested = invoiceAmount;
            setValue("RequestedAmount", invoiceAmount);
        }

        const isInvalid = invoiceAmount > 0
            && requested != null
            && roundMoney(requested) > roundMoney(invoiceAmount);

        if (requestedEl) {
            requestedEl.classList.toggle("is-invalid", isInvalid);
        }

        if (requestedWrapper) {
            requestedWrapper.classList.toggle("e-error", isInvalid);
        }

        return !isInvalid;
    }

    function syncDilutionPercentFromAmount() {
        const invoice = toNumberOrNull(value("InvoiceAmount")) || 0;
        const dilution = toNumberOrNull(value("DilutionReserveAmount")) || 0;

        if (invoice > 0) {
            setValue("DilutionReservePercent", formatMoney(roundMoney(dilution / invoice * 100)));
        }
    }

    function recalculateNetDisbursement() {
        const advance = toNumberOrNull(value("AdvanceAmount")) || 0;
        const discount = toNumberOrNull(value("DiscountAmount")) || 0;
        const commission = toNumberOrNull(value("CommissionAmount")) || 0;
        setValue("NetDisbursementAmount", formatMoney(roundMoney(Math.max(0, advance - discount - commission))));
    }

    function recalculateInvoiceAmounts() {
        const invoice = toNumberOrNull(value("InvoiceAmount")) || 0;
        const rates = getFactoringRates();
        const dilutionPercent = toNumberOrNull(value("DilutionReservePercent"));

        if (dilutionPercent != null && dilutionPercent > 0 && invoice > 0) {
            setValue("DilutionReserveAmount", formatMoney(roundMoney(invoice * dilutionPercent / 100)));
        } else if ((dilutionPercent == null || dilutionPercent <= 0)
            && rates.dilutionRatePercent > 0
            && invoice > 0
            && !toNumberOrNull(value("DilutionReserveAmount"))) {
            setValue("DilutionReservePercent", formatMoney(rates.dilutionRatePercent));
            setValue("DilutionReserveAmount", formatMoney(roundMoney(invoice * rates.dilutionRatePercent / 100)));
        }

        const dilution = Math.min(toNumberOrNull(value("DilutionReserveAmount")) || 0, invoice);
        const eligible = roundMoney(Math.max(0, invoice - dilution));
        setValue("EligibleAmount", formatMoney(eligible));

        validateRequestedAmount();
        const requested = toNumberOrNull(value("RequestedAmount")) || 0;
        const approved = roundMoney(requested > 0 ? Math.min(requested, eligible) : 0);
        setValue("ApprovedAmount", formatMoney(approved));

        const retentionRate = rates.retentionRatePercent ?? 0;
        const retention = roundMoney(approved * retentionRate / 100);
        const advance = roundMoney(Math.max(0, approved - retention));
        setValue("RetentionAmount", formatMoney(retention));
        setValue("AdvanceAmount", formatMoney(advance));
    }

    function parseDateOnly(iso) {
        if (!iso) {
            return null;
        }

        const parts = iso.split("-");
        if (parts.length !== 3) {
            return null;
        }

        const year = Number(parts[0]);
        const month = Number(parts[1]);
        const day = Number(parts[2]);
        if (!Number.isFinite(year) || !Number.isFinite(month) || !Number.isFinite(day)) {
            return null;
        }

        return { year, month, day };
    }

    function compareDateOnly(left, right) {
        if (left.year !== right.year) {
            return left.year - right.year;
        }

        if (left.month !== right.month) {
            return left.month - right.month;
        }

        return left.day - right.day;
    }

    function addMonthsDateOnly(date, months) {
        let year = date.year;
        let month = date.month + months;
        let day = date.day;

        while (month > 12) {
            month -= 12;
            year += 1;
        }

        while (month < 1) {
            month += 12;
            year -= 1;
        }

        const daysInMonth = new Date(year, month, 0).getDate();
        day = Math.min(day, daysInMonth);
        return { year, month, day };
    }

    function addDaysDateOnly(date, days) {
        const jsDate = new Date(date.year, date.month - 1, date.day);
        jsDate.setDate(jsDate.getDate() + days);
        return {
            year: jsDate.getFullYear(),
            month: jsDate.getMonth() + 1,
            day: jsDate.getDate()
        };
    }

    function calculateTermMonths(fromIso, toIso) {
        const from = parseDateOnly(fromIso);
        const to = parseDateOnly(toIso);
        if (!from || !to || compareDateOnly(to, from) <= 0) {
            return 0;
        }

        let months = (to.year - from.year) * 12 + (to.month - from.month);
        if (to.day < from.day) {
            months -= 1;
        }

        return Math.max(1, months);
    }

    function calculateDiscountAmount(principal, annualRatePercent, fromIso, toIso) {
        if (principal <= 0 || annualRatePercent <= 0) {
            return 0;
        }

        const from = parseDateOnly(fromIso);
        const to = parseDateOnly(toIso);
        if (!from || !to || compareDateOnly(to, from) <= 0) {
            return 0;
        }

        const annualRate = annualRatePercent / 100;
        let discount = principal * annualRate / 12;
        let afterFirstMonth = addMonthsDateOnly(from, 1);

        if (compareDateOnly(to, afterFirstMonth) <= 0) {
            return roundMoney(discount);
        }

        let cursor = afterFirstMonth;
        while (compareDateOnly(cursor, to) < 0) {
            const daysInMonth = new Date(cursor.year, cursor.month, 0).getDate();
            discount += principal * annualRate / daysInMonth;
            cursor = addDaysDateOnly(cursor, 1);
        }

        return roundMoney(discount);
    }

    function findMatchingPriceListItem(advance, termMonths) {
        const items = state.priceListItems || [];
        return items
            .filter(item => {
                const amountFrom = Number(pick(item, "AmountFrom") ?? 0);
                const amountTo = Number(pick(item, "AmountTo") ?? 0);
                return advance >= amountFrom && advance <= amountTo;
            })
            .filter(item => {
                const monthsFrom = pick(item, "MonthsFrom");
                const monthsTo = pick(item, "MonthsTo");
                return (monthsFrom == null || termMonths >= Number(monthsFrom))
                    && (monthsTo == null || termMonths <= Number(monthsTo));
            })
            .sort((left, right) => Number(pick(left, "SortOrder") ?? 0) - Number(pick(right, "SortOrder") ?? 0))[0] || null;
    }

    function applyLocalPricelistFees() {
        const advance = toNumberOrNull(value("AdvanceAmount")) || 0;
        if (advance <= 0 || !state.priceListItems.length) {
            return null;
        }

        syncBankDueIfEmpty();
        const disbursement = resolvePricingDisbursementDate();
        const bankDue = resolvePricingBankDueDate();
        const termMonths = calculateTermMonths(disbursement, bankDue) || 1;
        const tier = findMatchingPriceListItem(advance, termMonths);
        if (!tier) {
            return null;
        }

        const commissionAmount = Number(pick(tier, "FactoringCommissionFixedAmount") ?? 0);
        const discountRate = Number(pick(tier, "FactoringNominalInterestRate") ?? 0);
        setValue("CommissionAmount", formatMoney(commissionAmount));
        setValue("DiscountRatePercent", formatMoney(discountRate));
        if (advance > 0) {
            setValue("CommissionPercent", formatMoney((commissionAmount / advance) * 100));
        }

        if (disbursement && bankDue && compareDateOnly(parseDateOnly(bankDue), parseDateOnly(disbursement)) > 0) {
            setValue("DiscountAmount", formatMoney(calculateDiscountAmount(advance, discountRate, disbursement, bankDue)));
        }

        return tier;
    }

    function updateLocalPricingStatus(tier) {
        const statusEl = form.querySelector("[data-pricing-status]");
        if (!statusEl) {
            return;
        }

        if (state.pricingMessage) {
            statusEl.textContent = state.pricingMessage;
            return;
        }

        if (!tier) {
            statusEl.textContent = state.priceListItems.length
                ? "Nuk u gjet tier pricelist për këtë shumë/afat."
                : "Pricelist nuk është ngarkuar.";
            return;
        }

        const disbursement = resolvePricingDisbursementDate();
        const bankDue = resolvePricingBankDueDate();
        const days = calculateDaysBetween(disbursement, bankDue);
        const parts = [];

        const rate = pick(tier, "FactoringNominalInterestRate");
        if (rate != null) {
            parts.push(`Norma: ${formatMoney(rate)}%`);
        }

        if (days != null) {
            parts.push(formatTenorDays(Number(days)));
        }

        const amountFrom = pick(tier, "AmountFrom");
        const amountTo = pick(tier, "AmountTo");
        if (amountFrom != null && amountTo != null) {
            parts.push(`Pricelist: ${formatMoney(amountFrom)} – ${formatMoney(amountTo)}`);
        }

        statusEl.textContent = parts.join(" · ");
    }

    function recalculateFactoringInvoice() {
        syncBankDueIfEmpty();
        refreshInvoiceTenorDisplay();
        syncRequestedAmountConstraints();
        validateRequestedAmount();
        recalculateInvoiceAmounts();
        const tier = applyLocalPricelistFees();
        recalculateNetDisbursement();
        updateLocalPricingStatus(tier);
    }

    function resolvePricingBankDueDate() {
        return toIsoDate(value("BankInvoiceDueDate")) || toIsoDate(value("InvoiceDueDate"));
    }

    function resolvePricingDisbursementDate() {
        return toIsoDate(value("DisbursementDate"));
    }

    function scheduleInvoicePricingRecalculation() {
        recalculateFactoringInvoice();
    }

    async function loadInvoicePricingContext() {
        const creditCaseId = resolveCreditCaseId();
        state.pricingMessage = creditCaseId <= 0
            ? "Credit case nuk është i lidhur me këtë faturë."
            : "";

        if (creditCaseId <= 0) {
            state.priceListItems = [];
            recalculateFactoringInvoice();
            return;
        }

        try {
            const response = await fetch(`/Credit/CreditCaseFactoring/GetInvoicePricingContext?creditCaseId=${creditCaseId}`, {
                headers: { Accept: "application/json" }
            });

            if (!response.ok) {
                state.priceListItems = [];
                state.pricingMessage = "Nuk u ngarkua pricelist për këtë credit case.";
                recalculateFactoringInvoice();
                return;
            }

            const context = await response.json();
            state.factoringRates = {
                advanceRatePercent: Number(pick(context, "AdvanceRatePercent") ?? 100),
                retentionRatePercent: Number(pick(context, "RetentionRatePercent") ?? 0),
                dilutionRatePercent: Number(pick(context, "DilutionRatePercent") ?? 0)
            };
            state.priceListItems = pick(context, "PriceListItems") || context.priceListItems || [];
            state.pricingMessage = pick(context, "Message") || context.message || "";

            const caseOwnerBankPartyId = Number(pick(context, "CaseOwnerBankPartyId") ?? context.caseOwnerBankPartyId ?? 0);
            if (caseOwnerBankPartyId > 0) {
                setValue("CaseOwnerBankPartyId", caseOwnerBankPartyId);
                setValue("CaseOwnerIsIndividual", pick(context, "CaseOwnerIsIndividual") === true || context.caseOwnerIsIndividual === true);
            }
        } catch (error) {
            console.error("Failed to load factoring pricing context:", error);
            state.priceListItems = [];
            state.pricingMessage = "Gabim gjatë ngarkimit të pricelist.";
        }

        recalculateFactoringInvoice();
    }

    function validateBeforeSave() {
        recalculateFactoringInvoice();
        syncRequestedAmountConstraints();
        refreshInvoiceTenorDisplay();

        const invoiceAmount = toNumberOrNull(value("InvoiceAmount"));
        const requested = toNumberOrNull(value("RequestedAmount"));
        const fileInput = field("InvoicePath_File");

        if (fileInput?.files?.length && resolveCreditCaseId() <= 0) {
            showLookupError("Credit case duhet të jetë i lidhur para ngarkimit të faturës.");
            return false;
        }

        if (fileInput?.files?.length && !String(value("InvoiceNumber") || "").trim()) {
            showLookupError("Numri i faturës duhet plotësuar para ngarkimit të dokumentit.");
            field("InvoiceNumber")?.focus();
            return false;
        }

        if (invoiceAmount > 0 && requested != null && roundMoney(requested) > roundMoney(invoiceAmount)) {
            validateRequestedAmount();
            field("RequestedAmount")?.focus();
            showLookupError("Shuma e kërkuar nuk mund të jetë më e madhe se shuma e faturës.");
            return false;
        }

        form.querySelector("[data-factoring-invoice-lookup-error]")?.remove();
        return true;
    }

    function showLookupError(message) {
        const cardBody = form.querySelector(".ds-card-b");
        if (!cardBody) {
            return;
        }

        cardBody.querySelector("[data-factoring-invoice-lookup-error]")?.remove();
        cardBody.insertAdjacentHTML(
            "afterbegin",
            `<div class="alert alert-warning py-2 mb-3" data-factoring-invoice-lookup-error>${message}</div>`);
    }

    function resolveCreditCaseId() {
        const fromField = Number(value("CreditCaseId") || 0);
        if (fromField > 0) {
            return fromField;
        }

        const fromConfig = Number(config.creditCaseId || 0);
        if (fromConfig > 0) {
            return fromConfig;
        }

        if (config.gridName) {
            const grid = document.getElementById(config.gridName);
            const fromGrid = Number(grid?.dataset?.parentTargetId || 0);
            if (fromGrid > 0) {
                return fromGrid;
            }
        }

        const factoringPane = document.querySelector("#ccMasterTabsContent > .tab-pane[data-credit-case-id]");
        const fromHub = Number(factoringPane?.dataset?.creditCaseId || 0);
        if (fromHub > 0) {
            return fromHub;
        }

        const creditCaseInput = document.getElementById("CreditCaseIdValue")
            || document.getElementById("CreditCaseId");
        const syncfusionValue = creditCaseInput?.ej2_instances?.[0]?.value;
        const fromUpsert = Number(syncfusionValue ?? creditCaseInput?.value ?? 0);
        return fromUpsert > 0 ? fromUpsert : 0;
    }

    function resolveFactoringInvoiceId(row) {
        const fromRow = Number(pick(row, "FactoringInvoiceId") || 0);
        if (fromRow > 0) {
            return fromRow;
        }

        return Number(value("FactoringInvoiceId") || 0);
    }

    function getWorkflowStep() {
        return null;
    }

    function isInvoiceStatusLocked(step) {
        if (!step) {
            return false;
        }

        if (pick(step, "IsTerminal") || pick(step, "SetEntityActive") === true) {
            return true;
        }

        return !pick(step, "IsEditable") && !pick(step, "AllowsStatusChange");
    }

    function setFieldEnabled(id, enabled) {
        const el = field(id);
        if (el) {
            el.disabled = !enabled;
        }

        const ej = document.getElementById(id)?.ej2_instances?.[0];
        if (ej && typeof ej.enabled === "boolean") {
            ej.enabled = enabled;
            ej.dataBind?.();
        }
    }

    function getCounterpartySourceType() {
        return (value("InvoiceSourceType") || "PARTY").toUpperCase();
    }

    function setCounterpartyBankPartyId(bankPartyId) {
        const normalized = Number(bankPartyId || 0);
        setValue("FactoringBankPartyId", normalized > 0 ? normalized : "");

        const agreementSelect = field("FactoringCounterpartyBankPartySelect");
        if (agreementSelect) {
            agreementSelect.value = normalized > 0 ? String(normalized) : "";
        }
    }

    function clearCounterpartySearch() {
        const autocomplete = document.getElementById(config.counterpartySearchId || "")?.ej2_instances?.[0];
        if (!autocomplete) {
            return;
        }

        autocomplete.value = null;
        autocomplete.dataSource = [];
        autocomplete.dataBind?.();
    }

    async function loadCounterpartyBankParties(selectedBankPartyId) {
        const select = field("FactoringCounterpartyBankPartySelect");
        if (!select || !config.counterpartyListUrl) {
            return;
        }

        try {
            const response = await fetch(config.counterpartyListUrl, {
                headers: { Accept: "application/json" }
            });

            if (!response.ok) {
                return;
            }

            const rows = await response.json();
            select.innerHTML = '<option value=""></option>';
            state.counterpartyPartyMap = {};
            (rows || []).forEach(row => {
                const bankPartyId = Number(pick(row, "BankPartyId") || 0);
                if (bankPartyId > 0) {
                    state.counterpartyPartyMap[bankPartyId] = pick(row, "IsIndividual") === true;
                }

                const option = document.createElement("option");
                option.value = pick(row, "BankPartyId") || "";
                option.textContent = pick(row, "DisplayText") || pick(row, "FullName") || option.value;
                option.dataset.isIndividual = pick(row, "IsIndividual") === true ? "true" : "false";
                select.appendChild(option);
            });

            if (selectedBankPartyId > 0) {
                select.value = String(selectedBankPartyId);
            }
        } catch (error) {
            console.error("Failed to load counterparty bank parties:", error);
        }
    }

    function initializeCounterpartySearch(selectedBankPartyId, attempt) {
        attempt = attempt || 0;
        const searchId = config.counterpartySearchId;
        if (!searchId) {
            return;
        }

        const autocomplete = document.getElementById(searchId)?.ej2_instances?.[0];
        if (!autocomplete) {
            if (attempt < 30) {
                setTimeout(function () {
                    initializeCounterpartySearch(selectedBankPartyId, attempt + 1);
                }, 50);
            }

            return;
        }

        if (selectedBankPartyId <= 0 || !config.bankPartySearchUrl) {
            return;
        }

        fetch(`${config.bankPartySearchUrl}?searchText=${encodeURIComponent(selectedBankPartyId)}`, {
            headers: { Accept: "application/json" }
        })
            .then(response => response.ok ? response.json() : [])
            .then(rows => {
                const selected = (rows || []).find(row => Number(pick(row, "BankPartyId")) === selectedBankPartyId);
                if (!selected) {
                    return;
                }

                autocomplete.dataSource = [selected];
                autocomplete.value = pick(selected, "DisplayText") || pick(selected, "FullName");
                autocomplete.dataBind?.();
            })
            .catch(() => { });
    }

    function registerCounterpartySelectHandler() {
        if (!config.counterpartySelectFn) {
            return;
        }

        window[config.counterpartySelectFn] = function (args) {
            setCounterpartyBankPartyId(Number(pick(args?.itemData, "BankPartyId") || 0));
        };
    }

    function refreshCounterpartyUi() {
        const sourceType = getCounterpartySourceType();
        form.querySelector(".factoring-invoice-party-standard")?.classList.toggle("d-none", sourceType === "AGREEMENT");
        form.querySelector(".factoring-invoice-party-agreement")?.classList.toggle("d-none", sourceType !== "AGREEMENT");
    }

    function setCounterpartyFieldsEnabled(enabled) {
        setFieldEnabled("FactoringCounterpartyBankPartySelect", enabled);
        const autocomplete = document.getElementById(config.counterpartySearchId || "")?.ej2_instances?.[0];
        if (autocomplete) {
            autocomplete.enabled = enabled;
            autocomplete.dataBind?.();
        }
    }

    function setInvoiceFieldsEnabled(enabled) {
        [
            "FactoringAgreementId",
            "FactoringBankPartyId",
            "RiskBearerCode",
            "RequestedAmount",
            "NetDisbursementAmount",
            "NotificationRequired",
            "InvoicePath_File",
            "FactoringInvoiceStatusCode",
            "InvoiceNumber",
            "ExternalInvoiceRef",
            "InvoiceFactoringTypeCode",
            "InvoiceRecoursePolicyCode",
            "InvoiceCurrencyCode",
            "InvoiceIssueDate",
            "InvoiceDueDate",
            "BankInvoiceDueDate",
            "DisbursementDate",
            "InvoiceAmount",
            "EligibleAmount",
            "ApprovedAmount",
            "DilutionReservePercent",
            "AdvanceAmount",
            "RetentionAmount",
            "DilutionReserveAmount",
            "DiscountRatePercent",
            "CommissionPercent",
            "CommissionAmount",
            "DiscountAmount",
            "BankCode",
            "DisbursementAccountNumber",
            "FactoringIDecisionReasonCode",
            "FactoringIReasonNotes"
        ].forEach(id => setFieldEnabled(id, enabled));
        setCounterpartyFieldsEnabled(enabled);
    }

    const FACTORING_TYPE_ROLE_MAP = {
        FACTORING: "SELLER",
        INVOICE_FINANCE: "SELLER",
        DEBT_INVOICE_FINANCE: "BUYER",
        REVERSE_FACTORING: "BUYER"
    };

    function getFactoringTypeRole(row) {
        const typeCode = normalizeStatusCode(pick(row, "FactoringTypeCode"));
        return normalizeStatusCode(
            pick(row, "InitiatorPartyRoleCode")
            || FACTORING_TYPE_ROLE_MAP[typeCode]
            || "");
    }

    function getPoliciesForFactoringType(factoringTypeCode) {
        const normalizedType = normalizeStatusCode(factoringTypeCode);
        if (!normalizedType) {
            return [];
        }

        return getTypePolicies().filter(policy =>
            normalizeStatusCode(pick(policy, "FactoringTypeCode")) === normalizedType);
    }

    function getRiskBearerDisplayName(code) {
        const row = getRiskBearerCatalog().find(item =>
            normalizeStatusCode(pick(item, "RiskBearerCode")) === normalizeStatusCode(code));
        return pick(row, "RiskBearerName") || code || "";
    }

    function formatRecoursePolicyLabel(policy) {
        const name = pick(policy, "FactoringPolicyName") || pick(policy, "FactoringPolicyCode") || "";
        const required = pick(policy, "NotificationRequired") === true;
        return `${name} — Lajmërim: ${required ? "Po" : "Jo"}`;
    }

    function syncNotificationFromPolicy(policyCode) {
        const policy = getPoliciesForFactoringType(value("InvoiceFactoringTypeCode"))
            .find(row => pick(row, "FactoringPolicyCode") === policyCode);

        if (!policy) {
            return;
        }

        setValue("NotificationRequired", pick(policy, "NotificationRequired") === true);
    }

    function refreshRecoursePolicySelect(preferredPolicyCode, riskBearerCode) {
        const policies = getPoliciesForFactoringType(value("InvoiceFactoringTypeCode"))
            .filter(policy => {
                if (!riskBearerCode) {
                    return true;
                }

                return pick(policy, "RiskBearerCode") === riskBearerCode;
            });

        const current = preferredPolicyCode ?? value("InvoiceRecoursePolicyCode");
        const selectedCode = policies.some(row => pick(row, "FactoringPolicyCode") === current)
            ? current
            : "";

        populateSelect(
            "InvoiceRecoursePolicyCode",
            policies.map(policy => ({
                ...policy,
                FactoringPolicyName: formatRecoursePolicyLabel(policy)
            })),
            "FactoringPolicyCode",
            "FactoringPolicyName",
            selectedCode);

        if (selectedCode) {
            syncNotificationFromPolicy(selectedCode);
        }
    }

    function populateRiskBearerSelect(rows, preferredCode) {
        const current = preferredCode ?? value("RiskBearerCode");
        const selectedCode = (rows || []).some(row => pick(row, "RiskBearerCode") === current)
            ? current
            : "";

        populateSelect("RiskBearerCode", rows, "RiskBearerCode", "RiskBearerName", selectedCode);
        return selectedCode;
    }

    function refreshRiskBearerSelect(preferredCode) {
        const factoringTypeCode = value("InvoiceFactoringTypeCode");
        const policies = getPoliciesForFactoringType(factoringTypeCode);
        const seen = new Set();
        const bearers = [];

        policies.forEach(policy => {
            const code = pick(policy, "RiskBearerCode");
            const normalizedCode = normalizeStatusCode(code);
            if (!normalizedCode || seen.has(normalizedCode)) {
                return;
            }

            seen.add(normalizedCode);
            bearers.push({
                RiskBearerCode: code,
                RiskBearerName: getRiskBearerDisplayName(code)
            });
        });

        const selectedCode = populateRiskBearerSelect(bearers, preferredCode);
        refreshRecoursePolicySelect(value("InvoiceRecoursePolicyCode"), selectedCode);
    }

    function syncRiskBearerFromPolicy(policyCode) {
        const policy = getPoliciesForFactoringType(value("InvoiceFactoringTypeCode"))
            .find(row => pick(row, "FactoringPolicyCode") === policyCode);

        if (!policy) {
            setValue("RiskBearerCode", "");
            refreshRecoursePolicySelect("", "");
            return;
        }

        const riskBearerCode = pick(policy, "RiskBearerCode") || "";
        setValue("RiskBearerCode", riskBearerCode);
        refreshRecoursePolicySelect(policyCode, riskBearerCode);
        syncNotificationFromPolicy(policyCode);
    }

    function filterFactoringTypesByRole(roleCode) {
        const normalizedRole = normalizeStatusCode(roleCode);

        if (!normalizedRole) {
            return [];
        }

        return getFactoringTypes().filter(row => getFactoringTypeRole(row) === normalizedRole);
    }

    function refreshFactoringTypeSelect(preferredCode, explicitRoleCode) {
        const roleCode = normalizeStatusCode(explicitRoleCode ?? value("FactoringPartyRoleCode"));
        const types = filterFactoringTypesByRole(roleCode);
        const current = preferredCode ?? value("InvoiceFactoringTypeCode");
        const selectedCode = types.some(row => pick(row, "FactoringTypeCode") === current)
            ? current
            : "";

        populateSelect(
            "InvoiceFactoringTypeCode",
            types,
            "FactoringTypeCode",
            "FactoringTypeName",
            selectedCode);

        if (selectedCode !== current) {
            refreshCascadeForFactoringType("", null, "");
        } else if (selectedCode) {
            refreshCascadeForFactoringType(
                selectedCode,
                value("InvoiceRecoursePolicyCode"),
                value("RiskBearerCode"));
        } else {
            populateRiskBearerSelect([], "");
            refreshRecoursePolicySelect("", "");
        }
    }

    function refreshCascadeForFactoringType(factoringTypeCode, selectedPolicyCode, preferredRiskBearerCode) {
        if (!factoringTypeCode) {
            populateRiskBearerSelect([], "");
            refreshRecoursePolicySelect("", "");
            return;
        }

        refreshRiskBearerSelect(preferredRiskBearerCode ?? "");
        refreshRecoursePolicySelect(selectedPolicyCode, value("RiskBearerCode"));
    }

    function refreshChoiceUi() {
        const sourceType = (value("InvoiceSourceType") || "PARTY").toUpperCase();
        const roleCode = (value("FactoringPartyRoleCode") || "").toUpperCase();

        form.querySelectorAll("[data-source-type]").forEach(button => {
            button.classList.toggle("active", button.dataset.sourceType === sourceType);
        });

        form.querySelectorAll("[data-role-code]").forEach(button => {
            button.classList.toggle("active", button.dataset.roleCode === roleCode);
        });

        form.querySelector(".factoring-invoice-party-panel")?.classList.remove("d-none");
        form.querySelector(".factoring-invoice-agreement-panel")?.classList.toggle("d-none", sourceType !== "AGREEMENT");
        refreshCounterpartyUi();
    }

    function setSourceType(sourceType) {
        const normalized = (sourceType || "PARTY").toUpperCase();
        setValue("InvoiceSourceType", normalized);

        if (normalized === "AGREEMENT") {
            setCounterpartyBankPartyId(0);
            clearCounterpartySearch();
            loadCounterpartyBankParties(0);
        } else {
            setValue("FactoringAgreementId", "");
            const agreementSelect = field("FactoringCounterpartyBankPartySelect");
            if (agreementSelect) {
                agreementSelect.value = "";
            }
        }

        refreshChoiceUi();
        refreshFactoringTypeSelect("", value("FactoringPartyRoleCode"));
    }

    function setRole(roleCode, preferredFactoringTypeCode) {
        const normalizedRole = normalizeStatusCode(roleCode);
        setValue("FactoringPartyRoleCode", normalizedRole);
        refreshChoiceUi();
        refreshFactoringTypeSelect(preferredFactoringTypeCode ?? "", normalizedRole);
    }

    function syncFormMode() {
        const statusCode = normalizeStatusCode(value("FactoringInvoiceStatusCode"));
        const step = getWorkflowStep();
        const locked = isInvoiceStatusLocked(step);
        const saveButton = form.querySelector("[data-factoring-invoice-save]");

        if (locked) {
            setInvoiceFieldsEnabled(false);
            setFieldEnabled("FactoringInvoiceStatusCode", !!pick(step, "AllowsStatusChange"));
            if (saveButton) {
                saveButton.disabled = !pick(step, "AllowsStatusChange");
            }
            return;
        }

        if (statusCode === "READYFORDISBURSEMENT") {
            setInvoiceFieldsEnabled(false);
            setFieldEnabled("BankCode", true);
            setFieldEnabled("DisbursementAccountNumber", true);
            setFieldEnabled("FactoringInvoiceStatusCode", true);
            if (saveButton) {
                saveButton.disabled = false;
            }
            return;
        }

        setInvoiceFieldsEnabled(true);
        setFieldEnabled("FactoringInvoiceStatusCode", true);
        if (saveButton) {
            saveButton.disabled = false;
        }

        refreshChoiceUi();
    }

    function ensureStatusOption(statusCode) {
        if (!statusCode) {
            return [];
        }

        return [{ CaseStatusCode: statusCode, CaseStatusName: statusCode }];
    }

    function bindModel(row) {
        const invoiceId = resolveFactoringInvoiceId(row);
        state.persistedStatus = invoiceId > 0 ? pick(row, "FactoringInvoiceStatusCode") : "";

        setValue("FactoringInvoiceId", invoiceId || 0);
        const resolvedCreditCaseId = pick(row, "CreditCaseId") || resolveCreditCaseId() || 0;
        setValue("CreditCaseId", resolvedCreditCaseId);
        setValue("FactoringAgreementId", pick(row, "FactoringAgreementId"));
        setValue("FactoringBankPartyId", pick(row, "FactoringBankPartyId"));
        setValue("RiskBearerCode", pick(row, "RiskBearerCode"));
        setValue("RequestedAmount", pick(row, "RequestedAmount"));
        setValue("NetDisbursementAmount", pick(row, "NetDisbursementAmount"));
        setValue("InvoicePath", pick(row, "InvoicePath"));
        setValue("FactoringIDecisionReasonCode", pick(row, "FactoringIDecisionReasonCode"));
        setValue("FactoringIReasonNotes", pick(row, "FactoringIReasonNotes"));
        setValue("NotificationRequired", pick(row, "NotificationRequired") === true);
        setValue("InvoiceNumber", pick(row, "InvoiceNumber"));
        setValue("ExternalInvoiceRef", pick(row, "ExternalInvoiceRef"));
        setValue("InvoiceCurrencyCode", pick(row, "InvoiceCurrencyCode") || "EUR");
        setValue("InvoiceIssueDate", pick(row, "InvoiceIssueDate"));
        setValue("InvoiceDueDate", pick(row, "InvoiceDueDate"));
        setValue("BankInvoiceDueDate", pick(row, "BankInvoiceDueDate"));
        const disbursementDateValue = pick(row, "DisbursementDate");
        setValue(
            "DisbursementDate",
            isEmptyDate(disbursementDateValue) ? null : disbursementDateValue);
        setValue("InvoiceAmount", pick(row, "InvoiceAmount"));
        setValue("EligibleAmount", pick(row, "EligibleAmount"));
        setValue("ApprovedAmount", pick(row, "ApprovedAmount"));
        setValue("DilutionReservePercent", pick(row, "DilutionReservePercent"));
        setValue("AdvanceAmount", pick(row, "AdvanceAmount"));
        setValue("RetentionAmount", pick(row, "RetentionAmount"));
        setValue("DilutionReserveAmount", pick(row, "DilutionReserveAmount"));
        setValue("DiscountRatePercent", pick(row, "DiscountRatePercent"));
        setValue("CommissionPercent", pick(row, "CommissionPercent"));
        setValue("CommissionAmount", pick(row, "CommissionAmount"));
        setValue("DiscountAmount", pick(row, "DiscountAmount"));
        setValue("BankCode", pick(row, "BankCode"));
        setValue("DisbursementAccountNumber", pick(row, "DisbursementAccountNumber"));

        populateSelect("BankCode", getFkRows("BankCode"), "BankCode", "BankName", pick(row, "BankCode"));
        populateSelect(
            "FactoringIDecisionReasonCode",
            getFkRows("FactoringIDecisionReasonCode"),
            "DecisionReasonCode",
            "DecisionReasonName",
            pick(row, "FactoringIDecisionReasonCode"));
        populateSelect("FactoringAgreementId", getFkRows("FactoringAgreementId"), "FactoringAgreementId", "AgreementNumber", pick(row, "FactoringAgreementId"));

        const counterpartyBankPartyId = Number(pick(row, "FactoringBankPartyId") || 0);
        setCounterpartyBankPartyId(counterpartyBankPartyId);

        const selectedStatus = pick(row, "FactoringInvoiceStatusCode") || (invoiceId > 0 ? "" : "DRAFT");
        populateSelect("FactoringInvoiceStatusCode", ensureStatusOption(selectedStatus), "CaseStatusCode", "CaseStatusName", selectedStatus);

        const preferredFactoringTypeCode = pick(row, "FactoringTypeCode");
        const roleCode = normalizeStatusCode(
            value("FactoringPartyRoleCode") || pick(row, "FactoringPartyRoleCode") || "");
        setSourceType(pick(row, "FactoringAgreementId") ? "AGREEMENT" : "PARTY");
        setRole(roleCode, invoiceId > 0 ? preferredFactoringTypeCode : "");
        loadCounterpartyBankParties(counterpartyBankPartyId);
        initializeCounterpartySearch(counterpartyBankPartyId, 0);

        const selectedType = value("InvoiceFactoringTypeCode");
        if (!roleCode || !selectedType) {
            refreshCascadeForFactoringType("", null, "");
            syncFormMode();
            return;
        }

        refreshCascadeForFactoringType(
            selectedType,
            pick(row, "RecoursePolicyCode"),
            pick(row, "RiskBearerCode"));
        syncFormMode();
        syncRequestedAmountConstraints();
        validateRequestedAmount();
        refreshInvoiceTenorDisplay();
        scheduleInvoicePricingRecalculation();
    }

    function collectModel() {
        const decimalOrNull = id => {
            const raw = value(id);
            return raw === "" || raw === null ? null : Number(raw);
        };

        const intOrZero = id => {
            const raw = value(id);
            return raw === "" || raw === null ? 0 : Number(raw);
        };

        return {
            FactoringInvoiceId: intOrZero("FactoringInvoiceId"),
            CreditCaseId: intOrZero("CreditCaseId"),
            FactoringAgreementId: decimalOrNull("FactoringAgreementId"),
            FactoringBankPartyId: decimalOrNull("FactoringBankPartyId"),
            FactoringPartyRoleCode: emptyToNull(value("FactoringPartyRoleCode")),
            RiskBearerCode: emptyToNull(value("RiskBearerCode")),
            RequestedAmount: decimalOrNull("RequestedAmount") ?? 0,
            NetDisbursementAmount: decimalOrNull("NetDisbursementAmount"),
            NotificationRequired: !!value("NotificationRequired"),
            CaseOwnerBankPartyId: intOrZero("CaseOwnerBankPartyId"),
            CaseOwnerIsIndividual: !!value("CaseOwnerIsIndividual"),
            InvoicePath: emptyToNull(value("InvoicePath")),
            FactoringInvoiceStatusCode: value("FactoringInvoiceStatusCode") || "DRAFT",
            InvoiceNumber: value("InvoiceNumber") || "",
            ExternalInvoiceRef: emptyToNull(value("ExternalInvoiceRef")),
            FactoringTypeCode: emptyToNull(value("InvoiceFactoringTypeCode")),
            RecoursePolicyCode: emptyToNull(value("InvoiceRecoursePolicyCode")),
            InvoiceCurrencyCode: value("InvoiceCurrencyCode") || "EUR",
            InvoiceIssueDate: toIsoDate(value("InvoiceIssueDate")),
            InvoiceDueDate: toIsoDate(value("InvoiceDueDate")),
            BankInvoiceDueDate: toIsoDate(value("BankInvoiceDueDate")) || toIsoDate(value("InvoiceDueDate")),
            DisbursementDate: toIsoDate(value("DisbursementDate")),
            InvoiceAmount: Number(value("InvoiceAmount") || 0),
            EligibleAmount: decimalOrNull("EligibleAmount"),
            ApprovedAmount: decimalOrNull("ApprovedAmount"),
            DilutionReservePercent: decimalOrNull("DilutionReservePercent"),
            AdvanceAmount: decimalOrNull("AdvanceAmount"),
            RetentionAmount: decimalOrNull("RetentionAmount"),
            DilutionReserveAmount: decimalOrNull("DilutionReserveAmount"),
            DiscountRatePercent: decimalOrNull("DiscountRatePercent"),
            CommissionPercent: decimalOrNull("CommissionPercent"),
            CommissionAmount: decimalOrNull("CommissionAmount"),
            DiscountAmount: decimalOrNull("DiscountAmount"),
            IsNotified: false,
            IsOverdue: false,
            IsDisputed: false,
            FactoringIDecisionReasonCode: emptyToNull(value("FactoringIDecisionReasonCode")),
            FactoringIReasonNotes: emptyToNull(value("FactoringIReasonNotes")),
            BankCode: emptyToNull(value("BankCode")),
            DisbursementAccountNumber: emptyToNull(value("DisbursementAccountNumber"))
        };
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

    async function collectModelWithUpload() {
        const model = collectModel();
        const fileInput = field("InvoicePath_File");

        if (fileInput?.files?.length) {
            const file = fileInput.files[0];
            model.InvoicePathBase64 = await fileToDataUrl(file);
            model.InvoicePathOriginalFileName = file.name;
            model.InvoicePathContentType = file.type || "application/octet-stream";
        }

        return model;
    }

    form.querySelectorAll("[data-source-type]").forEach(button => {
        button.addEventListener("click", function () {
            setSourceType(this.dataset.sourceType);
        });
    });

    form.querySelectorAll("[data-role-code]").forEach(button => {
        button.addEventListener("click", function () {
            setRole(this.dataset.roleCode);
        });
    });

    field("InvoiceFactoringTypeCode")?.addEventListener("change", function () {
        refreshCascadeForFactoringType(this.value, null, "");
    });

    field("InvoiceRecoursePolicyCode")?.addEventListener("change", function () {
        syncRiskBearerFromPolicy(this.value);
        syncNotificationFromPolicy(this.value);
    });

    field("RiskBearerCode")?.addEventListener("change", function () {
        refreshRecoursePolicySelect("", this.value);
    });

    field("FactoringInvoiceStatusCode")?.addEventListener("change", syncFormMode);

    field("FactoringCounterpartyBankPartySelect")?.addEventListener("change", function () {
        setCounterpartyBankPartyId(Number(this.value || 0));
    });

    function bindSyncfusionControlChange(id, handler) {
        const el = document.getElementById(id);
        const instance = el?.ej2_instances?.[0];
        if (!instance || el?.dataset?.factoringInvoiceChangeBound === "true") {
            return false;
        }

        el.dataset.factoringInvoiceChangeBound = "true";
        const previous = instance.change;
        instance.change = function (args) {
            if (typeof previous === "function") {
                previous.call(this, args);
            }

            handler(args);
        };
        return true;
    }

    function onBankInvoiceDueDateChange() {
        refreshInvoiceTenorDisplay();
        scheduleInvoicePricingRecalculation();
    }

    function onInvoiceDueDateChange() {
        syncBankDueFromInvoiceDue();
        refreshInvoiceTenorDisplay();
        scheduleInvoicePricingRecalculation();
    }

    function initializeFactoringInvoiceControls(attempt) {
        if (form.dataset.factoringInvoiceControlsInitialized === "true") {
            return;
        }

        attempt = attempt || 0;
        const requiredIds = [
            "InvoiceAmount",
            "RequestedAmount",
            "InvoiceIssueDate",
            "InvoiceDueDate",
            "BankInvoiceDueDate",
            "DisbursementDate"
        ];
        const allReady = requiredIds.every(id => !!document.getElementById(id)?.ej2_instances?.[0]);

        if (!allReady && attempt < 20) {
            setTimeout(function () {
                initializeFactoringInvoiceControls(attempt + 1);
            }, 50);
            return;
        }

        form.dataset.factoringInvoiceControlsInitialized = "true";

        syncDefaultDisbursementDate();
        syncRequestedAmountConstraints();
        validateRequestedAmount();
        refreshInvoiceTenorDisplay();

        bindSyncfusionControlChange("InvoiceAmount", scheduleInvoicePricingRecalculation);
        bindSyncfusionControlChange("RequestedAmount", function () {
            validateRequestedAmount();
            scheduleInvoicePricingRecalculation();
        });
        bindSyncfusionControlChange("DilutionReservePercent", scheduleInvoicePricingRecalculation);
        bindSyncfusionControlChange("DilutionReserveAmount", function () {
            syncDilutionPercentFromAmount();
            scheduleInvoicePricingRecalculation();
        });
        bindSyncfusionControlChange("AdvanceAmount", recalculateNetDisbursement);
        bindSyncfusionControlChange("DiscountAmount", recalculateNetDisbursement);
        bindSyncfusionControlChange("CommissionAmount", recalculateNetDisbursement);
        bindSyncfusionControlChange("InvoiceIssueDate", function () {
            refreshInvoiceTenorDisplay();
            scheduleInvoicePricingRecalculation();
        });
        bindSyncfusionControlChange("InvoiceDueDate", onInvoiceDueDateChange);
        bindSyncfusionControlChange("BankInvoiceDueDate", onBankInvoiceDueDateChange);
        bindSyncfusionControlChange("DisbursementDate", function () {
            refreshInvoiceTenorDisplay();
            scheduleInvoicePricingRecalculation();
        });
    }

    form.querySelector("[data-factoring-invoice-save]")?.addEventListener("click", async function () {
        if (!validateBeforeSave()) {
            return;
        }

        const model = await collectModelWithUpload();

        if (config.isGridDialog) {
            saveGridPartialDialogEditor(config.gridName, model);
            return;
        }

        if (typeof ds?.entity?.save === "function") {
            ds.entity.collectModel = function () {
                return model;
            };
            ds.entity.save();
        }
    });

    form.querySelector("[data-factoring-invoice-cancel]")?.addEventListener("click", function () {
        if (config.isGridDialog) {
            closeGridPartialDialogEditor(config.gridName);
        }
    });

    const creditCaseId = resolveCreditCaseId();
    setValue("CreditCaseId", creditCaseId || pick(config.initialModel, "CreditCaseId") || 0);

    function startForm() {
        if (!Object.keys(getFkData()).length) {
            showLookupError("Foreign key data is not loaded for this form session.");
            return;
        }

        registerCounterpartySelectHandler();
        bindModel(config.initialModel || {});
        initializeFactoringInvoiceControls(0);
        loadInvoicePricingContext();
        loadCounterpartyBankParties(Number(value("FactoringBankPartyId") || 0));
    }

    if (Object.keys(getFkData()).length) {
        startForm();
    } else {
        setTimeout(startForm, 0);
    }
};
