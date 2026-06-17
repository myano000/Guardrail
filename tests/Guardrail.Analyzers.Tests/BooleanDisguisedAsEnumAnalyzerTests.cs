using Verify = Microsoft.CodeAnalysis.CSharp.Testing
    .CSharpAnalyzerVerifier<
        Guardrail.Analyzers.Analyzers.BooleanDisguisedAsEnumAnalyzer,
        Guardrail.Analyzers.Tests.NUnitVerifier>;

namespace Guardrail.Analyzers.Tests;

[TestFixture]
public class BooleanDisguisedAsEnumAnalyzerTests
{
    // ----------------------------------------------------------------
    // 準拠ケース（診断なし）
    // ----------------------------------------------------------------

    [Test]
    public async Task MeaningfulTwoMemberEnum_NoDiagnostic()
    {
        var source = """
            enum Priority { Normal, Urgent }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task ThreeMemberEnum_NoDiagnostic()
    {
        var source = """
            enum Status { Pending, Active, Closed }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task SingleMemberEnum_NoDiagnostic()
    {
        var source = """
            enum Singleton { Only }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    // ----------------------------------------------------------------
    // 違反ケース（診断あり）
    // ----------------------------------------------------------------

    [Test]
    public async Task TrueFalseEnum_ReportsDiagnostic()
    {
        var source = """
            enum {|GRD008:Boolean|} { TRUE, FALSE }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task TrueFalseMixedCase_ReportsDiagnostic()
    {
        var source = """
            enum {|GRD008:MyBool|} { True, False }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task YesNoEnum_ReportsDiagnostic()
    {
        var source = """
            enum {|GRD008:Answer|} { Yes, No }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task OnOffEnum_ReportsDiagnostic()
    {
        var source = """
            enum {|GRD008:Switch|} { On, Off }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task EnabledDisabledEnum_ReportsDiagnostic()
    {
        var source = """
            enum {|GRD008:Toggle|} { Enabled, Disabled }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task ActiveInactiveEnum_ReportsDiagnostic()
    {
        var source = """
            enum {|GRD008:State|} { Active, Inactive }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task ReversedOrder_ReportsDiagnostic()
    {
        // 順序が逆でも検出
        var source = """
            enum {|GRD008:Answer|} { No, Yes }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }
}
