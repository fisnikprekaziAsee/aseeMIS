using System;
using System.Collections.Generic;

namespace aseeMIS.Areas.Credit.ViewModels
{
    public class CreditFinancialAnalysisMasterViewModel
    {
        public long CreditCaseId { get; set; }
        public long? FrameworkId { get; set; }
        public string? CaseNumber { get; set; }
        public string? CurrencyCode { get; set; }
        public string FormSessionId { get; set; } = string.Empty;
        public DateOnly PeriodDate1 { get; set; }
        public DateOnly PeriodDate2 { get; set; }
        public List<CreditFinancialAnalysisBlockViewModel> BalanceLeftBlocks { get; set; } = new();
        public List<CreditFinancialAnalysisBlockViewModel> BalanceRightBlocks { get; set; } = new();
        public List<CreditFinancialAnalysisBlockViewModel> IncomeLeftBlocks { get; set; } = new();
        public List<CreditFinancialAnalysisBlockViewModel> IncomeRightBlocks { get; set; } = new();
        public List<CreditFinancialDynamicEntryViewModel> SalesRows { get; set; } = new();
        public List<CreditFinancialDynamicEntryViewModel> ClientRows { get; set; } = new();
        public List<CreditFinancialDynamicEntryViewModel> SupplierRows { get; set; } = new();
        public List<CreditFinancialDynamicEntryViewModel> ObligationRows { get; set; } = new();
        public List<CreditFinancialMetricRowViewModel> MarginRows { get; set; } = new();
        public List<CreditFinancialMetricRowViewModel> RatioRows { get; set; } = new();
        public CreditFinancialCommitteeSummaryViewModel CommitteeSummary { get; set; } = new();
    }

    public class CreditFinancialAnalysisBlockViewModel
    {
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public List<CreditFinancialStaticEntryViewModel> Rows { get; set; } = new();
        public decimal Period1Total { get; set; }
        public decimal Period2Total { get; set; }
    }

    public class CreditFinancialStaticEntryViewModel
    {
        public long Period1ValueId { get; set; }
        public long Period2ValueId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public decimal Period1Amount { get; set; }
        public decimal Period2Amount { get; set; }
    }

    public class CreditFinancialDynamicEntryViewModel
    {
        public long Period1ValueId { get; set; }
        public long Period2ValueId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string EntryKey { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Type { get; set; }
        public decimal Period1Amount { get; set; }
        public decimal Period2Amount { get; set; }
        public decimal? ExtraDecimal { get; set; }
        public DateOnly? ExtraDate { get; set; }
    }

    public class CreditFinancialMetricRowViewModel
    {
        public string MetricCode { get; set; } = string.Empty;
        public string MetricName { get; set; } = string.Empty;
        public bool IsPercentage { get; set; }
        public decimal Period1Value { get; set; }
        public decimal Period2Value { get; set; }
    }

    public class CreditFinancialCommitteeSummaryViewModel
    {
        public decimal TotalAssets { get; set; }
        public decimal TotalLiabilities { get; set; }
        public decimal Equity { get; set; }
        public decimal NetIncome { get; set; }
        public decimal CurrentRatio { get; set; }
        public decimal DebtToEquity { get; set; }
        public decimal ProfitMarginPercent { get; set; }
        public decimal RoaPercent { get; set; }
        public string? CommitteeNotes { get; set; }
    }

    public class CreditFinancialAnalysisSaveViewModel
    {
        public long CreditCaseId { get; set; }
        public long? FrameworkId { get; set; }
        public string? CurrencyCode { get; set; }
        public DateOnly PeriodDate1 { get; set; }
        public DateOnly PeriodDate2 { get; set; }
        public string? CommitteeNotes { get; set; }
        public List<CreditFinancialStaticEntryViewModel> StaticValues { get; set; } = new();
        public List<CreditFinancialDynamicEntryViewModel> SalesRows { get; set; } = new();
        public List<CreditFinancialDynamicEntryViewModel> ClientRows { get; set; } = new();
        public List<CreditFinancialDynamicEntryViewModel> SupplierRows { get; set; } = new();
        public List<CreditFinancialDynamicEntryViewModel> ObligationRows { get; set; } = new();
    }
}
