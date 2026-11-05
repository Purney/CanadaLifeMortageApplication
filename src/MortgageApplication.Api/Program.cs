using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MortgageApplication.Api.Configuration;
using MortgageApplication.Api.Contracts;
using MortgageApplication.Api.Data;
using MortgageApplication.Api.Endpoints;
using MortgageApplication.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IValidateOptions<AffordabilityOptions>, AffordabilityOptionsValidator>();
builder.Services.AddOptions<AffordabilityOptions>().BindConfiguration("Affordability").ValidateOnStart();
builder.Services.AddSingleton<AssessmentPolicy>(services => services.GetRequiredService<IOptions<AffordabilityOptions>>().Value.ToPolicy());
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<AffordabilityCalculator>();
builder.Services.AddScoped<MortgageApplicationService>();
builder.Services.AddDbContext<MortgageDbContext>((services, options) => options.UseSqlite(
    services.GetRequiredService<IConfiguration>().GetConnectionString("MortgageDatabase")
        ?? throw new InvalidOperationException("ConnectionStrings:MortgageDatabase is required.")));

var app = builder.Build();
// Freeze the effective policy before serving requests or migrating.
_ = app.Services.GetRequiredService<AssessmentPolicy>();
app.UseExceptionHandler();
app.UseStatusCodePages();
if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<MortgageDbContext>().Database.MigrateAsync();
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.MapMortgageApplications();
await app.RunAsync();

public partial class Program;

internal sealed class ApiExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var badRequest = exception as BadHttpRequestException;
        context.Response.StatusCode = badRequest?.StatusCode ?? StatusCodes.Status500InternalServerError;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new()
            {
                Status = context.Response.StatusCode,
                Title = badRequest is null ? "An unexpected error occurred." : "Invalid request.",
                Detail = badRequest is null ? "The request could not be completed." : "Check the request body, route values, and query parameters for the documented format."
            }
        });
    }
}
