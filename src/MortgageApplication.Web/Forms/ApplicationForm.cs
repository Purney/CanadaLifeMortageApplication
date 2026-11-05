namespace MortgageApplication.Web.Forms;

// Preserve entered text. The API owns binding, required fields and business validation.
public sealed class ApplicationForm
{
    public string? AnnualIncome { get; set; }
    public string? MonthlyExpenses { get; set; }
    public string? DepositAmount { get; set; }
    public string? PropertyValue { get; set; }
    public string? TermYears { get; set; }

    public void FillExample()
    {
        AnnualIncome = "60000";
        MonthlyExpenses = "500";
        DepositAmount = "20000";
        PropertyValue = "200000";
        TermYears = "25";
    }
}
