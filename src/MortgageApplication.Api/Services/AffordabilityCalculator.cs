using System.Globalization;
using MortgageApplication.Api.Contracts;

namespace MortgageApplication.Api.Services;

public sealed class AffordabilityCalculator
{
    public const string CalculationVersion = "1";

    public AssessmentResult Calculate(ApplicationInputs inputs, AssessmentPolicy policy)
    {
        // Discounted monthly payments sum to the loan's present value. The recurrence
        // avoids subtracting nearly equal values at small interest rates and works at zero.
        var discount = 1m / (1m + policy.AnnualInterestRatePercentage / 1200m);
        var factor = 0m;
        var discountedPayment = 1m;
        for (var month = 0; month < inputs.TermYears * 12; month++)
        {
            discountedPayment *= discount;
            factor += discountedPayment;
        }

        var loan = inputs.PropertyValue - inputs.DepositAmount;
        var repayment = loan / factor;
        var ltv = loan * 100m / inputs.PropertyValue;
        // Cross-multiply policy comparisons before dividing. AnnualIncome / 12 can
        // repeat in decimal and incorrectly reject exact DTI equality on small inputs.
        var discountedDebt = inputs.MonthlyExpenses * factor;
        var dtiNumerator = (discountedDebt + loan) * 1200m;
        var dtiDenominator = inputs.AnnualIncome * factor;
        var dtiLimit = policy.MaxDtiPercentage * dtiDenominator;
        var dti = dtiNumerator / dtiDenominator;
        var paymentLimitedLoan = Math.Max(0m, (dtiLimit - discountedDebt * 1200m) / 1200m);
        var incomeCap = policy.MaxIncomeMultiple * inputs.AnnualIncome;
        var propertyCap = policy.MaxLtvPercentage * inputs.PropertyValue / 100m;
        var capacity = Math.Min(paymentLimitedLoan, incomeCap);
        var reasons = new List<string>();
        if (loan * 100m > policy.MaxLtvPercentage * inputs.PropertyValue) reasons.Add($"LTV exceeds {Format(policy.MaxLtvPercentage)}%.");
        if (dtiNumerator > dtiLimit) reasons.Add($"DTI exceeds {Format(policy.MaxDtiPercentage)}%.");
        if (loan > incomeCap) reasons.Add($"Requested loan exceeds {Format(policy.MaxIncomeMultiple)} times annual income.");
        var approved = reasons.Count == 0;
        return new(loan, repayment, ltv, dti, approved ? "approved" : "declined", capacity,
            Math.Min(capacity, propertyCap), approved ? "All configured affordability limits are met." : string.Join(" ", reasons));
    }

    private static string Format(decimal value) => value.ToString("0.############################", CultureInfo.InvariantCulture);
}
