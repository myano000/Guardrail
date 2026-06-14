using Verify = Microsoft.CodeAnalysis.CSharp.Testing
    .CSharpAnalyzerVerifier<
        Guardrail.Analyzers.Analyzers.NoRefOutParameterAnalyzer,
        Guardrail.Analyzers.Tests.NUnitVerifier>;

namespace Guardrail.Analyzers.Tests;

[TestFixture]
public class NoRefOutParameterAnalyzerTests
{
    // ----------------------------------------------------------------
    // 準拠ケース（診断なし）
    // ----------------------------------------------------------------

    [Test]
    public async Task NormalParameter_NoDiagnostic()
    {
        var source = """
            class Foo
            {
                void Bar(int x, string y) { }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task InParameter_NoDiagnostic()
    {
        // 'in' は読み取り専用参照。ref/out ではないので対象外
        var source = """
            class Foo
            {
                void Bar(in int x) { }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task ParamsParameter_NoDiagnostic()
    {
        var source = """
            class Foo
            {
                void Bar(params int[] xs) { }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task ExtensionMethodThisParameter_NoDiagnostic()
    {
        // 拡張メソッドの 'this' は対象外
        var source = """
            static class Extensions
            {
                public static int Double(this int x) => x * 2;
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task TupleReturnType_NoDiagnostic()
    {
        // ref/out の代わりにタプルで複数値を返す — 準拠パターン
        var source = """
            class Foo
            {
                (int Quotient, int Remainder) DivRem(int a, int b) => (a / b, a % b);
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    // ----------------------------------------------------------------
    // 違反ケース（診断あり）
    // ----------------------------------------------------------------

    [Test]
    public async Task OutParameter_ReportsDiagnostic()
    {
        var source = """
            class Foo
            {
                void TryParse(string s, out int {|GRD003:result|}) { result = 0; }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task RefParameter_ReportsDiagnostic()
    {
        var source = """
            class Foo
            {
                void Increment(ref int {|GRD003:counter|}) { counter++; }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task MultipleRefOutParameters_ReportsDiagnosticOnEach()
    {
        var source = """
            class Foo
            {
                void Swap(ref int {|GRD003:a|}, ref int {|GRD003:b|}) { (a, b) = (b, a); }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task OutParameterInConstructor_ReportsDiagnostic()
    {
        // コンストラクタの out パラメータも対象
        var source = """
            class Foo
            {
                public Foo(out int {|GRD003:result|}) { result = 0; }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task OutParameterInLocalFunction_ReportsDiagnostic()
    {
        var source = """
            class Foo
            {
                void Bar()
                {
                    void Local(out int {|GRD003:x|}) { x = 0; }
                }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }
}
