using Verify = Microsoft.CodeAnalysis.CSharp.Testing
    .CSharpAnalyzerVerifier<
        Guardrail.Analyzers.Analyzers.NoBoolParameterAnalyzer,
        Guardrail.Analyzers.Tests.NUnitVerifier>;

namespace Guardrail.Analyzers.Tests;

[TestFixture]
public class NoBoolParameterAnalyzerTests
{
    // ----------------------------------------------------------------
    // 準拠ケース（診断なし）
    // ----------------------------------------------------------------

    [Test]
    public async Task NormalParameters_NoDiagnostic()
    {
        var source = """
            class Foo
            {
                void Bar(int x, string y, double z) { }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task EnumParameter_NoDiagnostic()
    {
        // 列挙型でフラグを表現する — GRD005 の推奨パターン
        var source = """
            enum Priority { Normal, Urgent }
            class Mailer
            {
                void Send(string msg, Priority priority) { }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task ExtensionMethodThisParameter_NoDiagnostic()
    {
        // bool の拡張メソッドの 'this' は対象外
        var source = """
            static class BoolExtensions
            {
                public static string ToYesNo(this bool value) => value ? "Yes" : "No";
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task BoolReturnType_NoDiagnostic()
    {
        // bool を返す（パラメータではない）は対象外
        var source = """
            class Foo
            {
                bool IsValid(string s) => !string.IsNullOrEmpty(s);
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task AllowedMethod_NoDiagnostic_WhenConfigured()
    {
        // guardrail.json で allowedMethods に設定したメソッドは除外
        var source = """
            class Foo
            {
                void LegacyToggle(string name, bool enabled) { }
            }
            """;
        var guardrailJson = """
            {
              "boolParameter": {
                "allowedMethods": ["LegacyToggle"]
              }
            }
            """;

        var test = new CSharpAnalyzerTest<
            Guardrail.Analyzers.Analyzers.NoBoolParameterAnalyzer,
            Guardrail.Analyzers.Tests.NUnitVerifier>
        {
            TestCode = source,
        };
        test.TestState.AdditionalFiles.Add(("guardrail.json", guardrailJson));
        await test.RunAsync();
    }

    // ----------------------------------------------------------------
    // 違反ケース（診断あり）
    // ----------------------------------------------------------------

    [Test]
    public async Task BoolParameter_ReportsDiagnostic()
    {
        var source = """
            class Foo
            {
                void Send(string msg, bool {|GRD005:isUrgent|}) { }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task NullableBoolParameter_ReportsDiagnostic()
    {
        // bool? もフラグ引数の臭い
        var source = """
            class Foo
            {
                void Toggle(string feature, bool? {|GRD005:enable|}) { }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task MultipleBoolParameters_ReportsDiagnosticOnEach()
    {
        var source = """
            class Foo
            {
                void Configure(bool {|GRD005:enableA|}, bool {|GRD005:enableB|}) { }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task BoolParameterInConstructor_ReportsDiagnostic()
    {
        // コンストラクタの bool パラメータも対象
        var source = """
            class Foo
            {
                public Foo(int id, bool {|GRD005:isActive|}) { }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task BoolParameterInLocalFunction_ReportsDiagnostic()
    {
        var source = """
            class Foo
            {
                void Bar()
                {
                    void Local(bool {|GRD005:flag|}) { }
                }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task MixedParameters_ReportsDiagnosticOnlyOnBool()
    {
        // int と bool が混在: bool だけに報告
        var source = """
            class Foo
            {
                void Process(int count, string name, bool {|GRD005:verbose|}) { }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }
}
