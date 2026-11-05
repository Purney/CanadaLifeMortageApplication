using Microsoft.AspNetCore.Http.HttpResults;
using System.ComponentModel.DataAnnotations;
using MortgageApplication.Api.Contracts;
using MortgageApplication.Api.Services;

namespace MortgageApplication.Api.Endpoints;

public static class MortgageApplicationEndpoints
{
    public static void MapMortgageApplications(this WebApplication app)
    {
        var group = app.MapGroup("/applications").WithTags("Mortgage applications");
        group.MapPost("", Create).WithSummary("Submit a mortgage application")
            .WithDescription("Valid inputs are persisted even if affordability later declines them. Use one currency. Monthly expenses means continuing debt payments, excluding living costs and housing replaced by this mortgage.")
            .ProducesValidationProblem().ProducesProblem(400);
        group.MapGet("/{id:guid}", Get).WithSummary("Retrieve a saved application").ProducesProblem(404);
        group.MapPost("/{applicationId:guid}/assessments", Assess)
            .WithSummary("Create and save a new affordability assessment")
            .WithDescription("No request body. Every request creates a new immutable assessment, including declines. Returns input/rule snapshots. Retrying can create another record.").ProducesProblem(404).ProducesProblem(500);
        group.MapGet("/{applicationId:guid}/assessments/{assessmentId:guid}", GetAssessment)
            .WithSummary("Retrieve a saved assessment without recalculating").ProducesProblem(404);
        group.MapGet("/{applicationId:guid}/assessments", ListAssessments)
            .WithSummary("List saved assessments with paging, decision filtering and creation-time sorting")
            .WithDescription("Returns full immutable snapshots. offset defaults to 0 (nonnegative); limit to 10 (1–100); status to all (all, approved, declined); sort to desc (asc or desc). totalCount counts the filtered matches before paging. Timestamp ties use insertion sequence in the same direction. Existing applications with no matches return an empty page; unknown applications return 404.")
            .ProducesValidationProblem().ProducesProblem(404);
    }

    private static async Task<Results<Created<ApplicationResponse>, ValidationProblem>> Create(
        CreateApplicationRequest request, MortgageApplicationService service, CancellationToken cancellationToken)
    {
        var errors = ApplicationValidator.Validate(request);
        if (errors.Count != 0) return TypedResults.ValidationProblem(errors);
        var response = await service.CreateAsync(request.ToInputs(), cancellationToken);
        return TypedResults.Created($"/applications/{response.Id}", response);
    }

    private static async Task<Results<Ok<ApplicationResponse>, ProblemHttpResult>> Get(
        Guid id, MortgageApplicationService service, CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);
        return result is null ? NotFound("Application not found.") : TypedResults.Ok(result);
    }

    private static async Task<Results<Created<AssessmentResponse>, ProblemHttpResult>> Assess(
        Guid applicationId, MortgageApplicationService service, CancellationToken cancellationToken)
    {
        var result = await service.AssessAsync(applicationId, cancellationToken);
        return result is null ? NotFound("Application not found.") :
            TypedResults.Created($"/applications/{applicationId}/assessments/{result.Id}", result);
    }

    private static async Task<Results<Ok<AssessmentResponse>, ProblemHttpResult>> GetAssessment(
        Guid applicationId, Guid assessmentId, MortgageApplicationService service, CancellationToken cancellationToken)
    {
        var result = await service.GetAssessmentAsync(applicationId, assessmentId, cancellationToken);
        return result is null ? NotFound("Assessment not found for this application.") : TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<AssessmentPage>, ValidationProblem, ProblemHttpResult>> ListAssessments(
        Guid applicationId, MortgageApplicationService service, CancellationToken cancellationToken,
        [Range(0, int.MaxValue)] int offset = 0, [Range(1, 100)] int limit = 10,
        string status = "all", string sort = "desc")
    {
        var errors = new Dictionary<string, string[]>();
        if (offset < 0)
            errors["offset"] = ["Offset must be a whole number greater than or equal to zero."];
        if (limit is < 1 or > 100)
            errors["limit"] = ["Limit must be a whole number from 1 to 100."];
        if (status is not ("all" or "approved" or "declined")) errors["status"] = ["Status must be all, approved, or declined."];
        if (sort is not ("asc" or "desc")) errors["sort"] = ["Sort must be asc or desc."];
        if (errors.Count > 0) return TypedResults.ValidationProblem(errors);
        var result = await service.ListAssessmentsAsync(applicationId, offset, limit, status, sort, cancellationToken);
        return result is null ? NotFound("Application not found.") : TypedResults.Ok(result);
    }

    private static ProblemHttpResult NotFound(string detail) => TypedResults.Problem(statusCode: 404, title: "Not found", detail: detail);
}
