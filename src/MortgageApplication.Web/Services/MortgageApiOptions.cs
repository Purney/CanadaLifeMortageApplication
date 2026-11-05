namespace MortgageApplication.Web.Services;

public sealed class MortgageApiOptions
{
    public string ApiBaseUrl { get; set; } = "";

    public static bool IsValid(MortgageApiOptions options) =>
        Uri.TryCreate(options.ApiBaseUrl, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https"
        && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)
        && string.IsNullOrEmpty(uri.UserInfo);
}
