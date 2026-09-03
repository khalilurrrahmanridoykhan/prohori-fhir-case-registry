using Hl7.Fhir.Model;
using Prohori.Api.Fhir;

namespace Prohori.Api.Tests;

public class OperationOutcomeMapperTests
{
    [Fact]
    public void Error_issue_diagnostics_become_the_problem_detail()
    {
        var outcome = new OperationOutcome
        {
            Issue =
            [
                new OperationOutcome.IssueComponent
                {
                    Severity = OperationOutcome.IssueSeverity.Error,
                    Code = OperationOutcome.IssueType.CodeInvalid,
                    Diagnostics = "Unknown AdministrativeGender code 'banana'",
                },
            ],
        };

        var problem = OperationOutcomeMapper.ToProblemDetails(outcome, 422);

        problem.Status.ShouldBe(422);
        problem.Detail.ShouldContain("banana");
        problem.Extensions.ShouldContainKey("issues");
    }

    [Fact]
    public void Information_only_outcome_yields_a_generic_detail()
    {
        var outcome = new OperationOutcome
        {
            Issue =
            [
                new OperationOutcome.IssueComponent
                {
                    Severity = OperationOutcome.IssueSeverity.Information,
                    Code = OperationOutcome.IssueType.Informational,
                    Diagnostics = "all good",
                },
            ],
        };

        var problem = OperationOutcomeMapper.ToProblemDetails(outcome, 502);

        problem.Detail.ShouldBe("The server returned no error detail.");
    }

    [Fact]
    public void Null_outcome_is_tolerated()
    {
        var problem = OperationOutcomeMapper.ToProblemDetails(null, 502);

        problem.Status.ShouldBe(502);
        problem.Extensions.ShouldContainKey("issues");
    }
}
