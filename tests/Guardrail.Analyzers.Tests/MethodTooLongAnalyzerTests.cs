using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Verify = Microsoft.CodeAnalysis.CSharp.Testing
    .CSharpAnalyzerVerifier<
        Guardrail.Analyzers.Analyzers.MethodTooLongAnalyzer,
        Guardrail.Analyzers.Tests.NUnitVerifier>;

namespace Guardrail.Analyzers.Tests;

[TestFixture]
public class MethodTooLongAnalyzerTests
{
    // ----------------------------------------------------------------
    // テスト用ヘルパー: N 個のステートメントを持つメソッド本体を生成
    // ----------------------------------------------------------------

    /// <summary>指定したステートメント数の int 宣言ブロックを生成する。</summary>
    private static string MakeStatements(int count)
    {
        var lines = new System.Text.StringBuilder();
        for (var i = 0; i < count; i++)
            lines.AppendLine($"        int s{i} = {i};");
        return lines.ToString();
    }

    // ----------------------------------------------------------------
    // 準拠ケース（診断なし）
    // ----------------------------------------------------------------

    [Test]
    public async Task ShortMethod_NoDiagnostic()
    {
        // デフォルトしきい値（30 ステートメント / 40 行）を超えない短いメソッド
        var source = """
            class Foo
            {
                void Bar()
                {
                    int a = 1;
                    int b = 2;
                }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task ExpressionBodiedMethod_NoDiagnostic()
    {
        // 式形式（body == null）はスキップ
        var source = """
            class Foo
            {
                int Add(int a, int b) => a + b;
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task AbstractMethod_NoDiagnostic()
    {
        // abstract メソッド（body 無し）はスキップ
        var source = """
            abstract class Foo
            {
                protected abstract void Process();
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task InterfaceMethod_NoDiagnostic()
    {
        // interface のデフォルト実装なしメソッドはスキップ
        var source = """
            interface IFoo
            {
                void Process();
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task ExactlyAtThreshold_NoDiagnostic()
    {
        // ちょうど 30 ステートメント（超過ではない）は診断なし
        var statements = MakeStatements(30);
        var source = $$"""
            class Foo
            {
                void Bar()
                {
            {{statements}}    }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    // ----------------------------------------------------------------
    // 違反ケース: ステートメント数超過
    // ----------------------------------------------------------------

    [Test]
    public async Task TooManyStatements_ReportsDiagnostic()
    {
        // 31 ステートメント → デフォルトしきい値 30 を超える
        var statements = MakeStatements(31);
        var source = $$"""
            class Foo
            {
                void {|GRD009:DoEverything|}()
                {
            {{statements}}    }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task TooManyStatements_InLocalFunction_ReportsDiagnostic()
    {
        // ローカル関数でも発火する
        var statements = MakeStatements(31);
        var source = $$"""
            class Foo
            {
                void Outer()
                {
                    void {|GRD009:Inner|}()
                    {
            {{statements}}        }
                }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    // ----------------------------------------------------------------
    // 違反ケース: 物理行数超過（ステートメントは少ないが空行/コメントが多い）
    // ----------------------------------------------------------------

    [Test]
    public async Task TooManyLines_ReportsDiagnostic()
    {
        // ステートメント数は 5 だがコメント・空行で 41 行超え
        var padding = new System.Text.StringBuilder();
        for (var i = 0; i < 38; i++)
            padding.AppendLine($"        // line {i}");

        var source = $$"""
            class Foo
            {
                void {|GRD009:BigComment|}()
                {
                    int a = 1;
                    int b = 2;
                    int c = 3;
                    int d = 4;
                    int e = 5;
            {{padding}}    }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    // ----------------------------------------------------------------
    // 設定テスト: guardrail.json でしきい値を変更
    // ----------------------------------------------------------------

    [Test]
    public async Task SmallThreshold_ShortMethodExceedingConfig_ReportsDiagnostic()
    {
        // maxStatements=3 に設定 → 4 ステートメントで発火
        var source = """
            class Foo
            {
                void {|GRD009:TinyLimit|}()
                {
                    int a = 1;
                    int b = 2;
                    int c = 3;
                    int d = 4;
                }
            }
            """;
        var guardrailJson = """
            {
              "methodLength": {
                "maxStatements": 3,
                "maxLines": 100
              }
            }
            """;

        var test = new CSharpAnalyzerTest<
            Guardrail.Analyzers.Analyzers.MethodTooLongAnalyzer,
            Guardrail.Analyzers.Tests.NUnitVerifier>
        {
            TestCode = source,
        };
        test.TestState.AdditionalFiles.Add(("guardrail.json", guardrailJson));
        await test.RunAsync();
    }

    [Test]
    public async Task LargeThreshold_LongMethodBelowConfig_NoDiagnostic()
    {
        // maxStatements=100 に設定 → 31 ステートメントでは発火しない
        var statements = MakeStatements(31);
        var source = $$"""
            class Foo
            {
                void NotTooLong()
                {
            {{statements}}    }
            }
            """;
        var guardrailJson = """
            {
              "methodLength": {
                "maxStatements": 100,
                "maxLines": 200
              }
            }
            """;

        var test = new CSharpAnalyzerTest<
            Guardrail.Analyzers.Analyzers.MethodTooLongAnalyzer,
            Guardrail.Analyzers.Tests.NUnitVerifier>
        {
            TestCode = source,
        };
        test.TestState.AdditionalFiles.Add(("guardrail.json", guardrailJson));
        await test.RunAsync();
    }
}
