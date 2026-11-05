using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MortgageApplication.Api.Contracts;
using MortgageApplication.Api.Data;
using MortgageApplication.Api.Models;
using ApplicationEntity = MortgageApplication.Api.Models.MortgageApplication;

namespace MortgageApplication.Api.Services;

public sealed class MortgageApplicationService(MortgageDbContext db, AffordabilityCalculator calculator,
    AssessmentPolicy policy, TimeProvider time)
{
    private static readonly JsonSerializerOptions SnapshotOptions = new(JsonSerializerDefaults.Web);

    public async Task<ApplicationResponse> CreateAsync(ApplicationInputs inputs, CancellationToken cancellationToken)
    {
        var entity = new ApplicationEntity
        {
            Id = Guid.NewGuid(), CreatedAtUtc = time.GetUtcNow().UtcDateTime,
            AnnualIncome = inputs.AnnualIncome, MonthlyExpenses = inputs.MonthlyExpenses,
            DepositAmount = inputs.DepositAmount, PropertyValue = inputs.PropertyValue, TermYears = inputs.TermYears
        };
        db.Applications.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.ToResponse();
    }

    public async Task<ApplicationResponse?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        (await db.Applications.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken))?.ToResponse();

    public async Task<AssessmentResponse?> AssessAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        var application = await db.Applications.AsNoTracking().SingleOrDefaultAsync(x => x.Id == applicationId, cancellationToken);
        if (application is null) return null;
        var inputs = application.ToInputs();
        var now = time.GetUtcNow();
        var snapshot = new AssessmentResponse(Guid.NewGuid(), applicationId, now,
            AffordabilityCalculator.CalculationVersion, inputs, policy, calculator.Calculate(inputs, policy));
        db.Assessments.Add(new AffordabilityAssessment
        {
            Id = snapshot.Id, ApplicationId = applicationId, CreatedAtUtcTicks = now.UtcTicks,
            SnapshotJson = JsonSerializer.Serialize(snapshot, SnapshotOptions)
        });
        await db.SaveChangesAsync(cancellationToken);
        return snapshot.Rounded();
    }

    public async Task<AssessmentResponse?> GetAssessmentAsync(Guid applicationId, Guid assessmentId, CancellationToken cancellationToken)
    {
        var entity = await db.Assessments.AsNoTracking().SingleOrDefaultAsync(
            x => x.ApplicationId == applicationId && x.Id == assessmentId, cancellationToken);
        return Read(entity);
    }

    public async Task<AssessmentPage?> ListAssessmentsAsync(Guid applicationId, int offset, int limit,
        string status, string sort, CancellationToken cancellationToken)
    {
        if (!await db.Applications.AnyAsync(x => x.Id == applicationId, cancellationToken)) return null;

        // The JSON path and SQL structure are fixed; the decision is a parameter.
        // Compose filtering, counting, ordering and paging in SQLite before deserializing snapshots.
        IQueryable<AffordabilityAssessment> source = status == "all" ? db.Assessments :
            db.Assessments.FromSqlInterpolated($"SELECT * FROM \"Assessments\" WHERE json_extract(\"SnapshotJson\", '$.result.decision') = {status}");
        var query = source.AsNoTracking().Where(x => x.ApplicationId == applicationId);
        var totalCount = await query.CountAsync(cancellationToken);
        var ordered = sort == "asc"
            ? query.OrderBy(x => x.CreatedAtUtcTicks).ThenBy(x => x.Sequence)
            : query.OrderByDescending(x => x.CreatedAtUtcTicks).ThenByDescending(x => x.Sequence);
        var page = await ordered.Skip(offset).Take(limit).ToListAsync(cancellationToken);
        return new(page.Select(entity => Read(entity)!).ToArray(), offset, limit, totalCount);
    }

    private static AssessmentResponse? Read(AffordabilityAssessment? entity) => entity is null ? null :
        (JsonSerializer.Deserialize<AssessmentResponse>(entity.SnapshotJson, SnapshotOptions)
            ?? throw new InvalidOperationException("Assessment snapshot is invalid.")).Rounded();
}
