using Verify = Microsoft.CodeAnalysis.CSharp.Testing
    .CSharpAnalyzerVerifier<
        Guardrail.Analyzers.Analyzers.SingleConstructorAnalyzer,
        Guardrail.Analyzers.Tests.NUnitVerifier>;

namespace Guardrail.Analyzers.Tests;

[TestFixture]
public class SingleConstructorAnalyzerTests
{
    // ----------------------------------------------------------------
    // 準拠ケース（診断なし）
    // ----------------------------------------------------------------

    [Test]
    public async Task NoConstructor_NoDiagnostic()
    {
        var source = """
            class Foo { }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task SingleInstanceConstructor_NoDiagnostic()
    {
        var source = """
            class Foo
            {
                public Foo(int x) { }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task StaticConstructorOnly_NoDiagnostic()
    {
        // static コンストラクタはカウントしない
        var source = """
            class Foo
            {
                static Foo() { }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task StaticPlusOneInstanceConstructor_NoDiagnostic()
    {
        // static + インスタンス 1 つ → OK
        var source = """
            class Foo
            {
                static Foo() { }
                public Foo(int x) { }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    // ----------------------------------------------------------------
    // 違反ケース（診断あり）
    // ----------------------------------------------------------------

    [Test]
    public async Task TwoInstanceConstructors_ReportsDiagnosticOnSecond()
    {
        var source = """
            class Foo
            {
                public Foo(int x) { }
                public {|GRD002:Foo|}() { }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task ThreeInstanceConstructors_ReportsDiagnosticOnSecondAndThird()
    {
        var source = """
            class Foo
            {
                public Foo(int x, string y) { }
                public {|GRD002:Foo|}(int x) { }
                public {|GRD002:Foo|}() { }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task StaticPlusTwoInstanceConstructors_ReportsDiagnosticOnlyOnSecondInstance()
    {
        // static はカウントしない → インスタンスが 2 つあるので 2 つ目に GRD002
        var source = """
            class Foo
            {
                static Foo() { }
                public Foo(int x) { }
                public {|GRD002:Foo|}() { }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }
}
