using Verify = Microsoft.CodeAnalysis.CSharp.Testing
    .CSharpAnalyzerVerifier<
        Guardrail.Analyzers.Analyzers.NoDowncastAnalyzer,
        Guardrail.Analyzers.Tests.NUnitVerifier>;

namespace Guardrail.Analyzers.Tests;

[TestFixture]
public class NoDowncastAnalyzerTests
{
    // ----------------------------------------------------------------
    // 共通の型定義プリアンブル
    // ----------------------------------------------------------------

    private const string Preamble = """
        class Animal { }
        class Dog : Animal { }
        class Cat : Animal { }
        interface IShape { }
        class Circle : IShape { public double Radius { get; set; } }

        """;

    // ----------------------------------------------------------------
    // 準拠ケース（診断なし）
    // ----------------------------------------------------------------

    [Test]
    public async Task PolymorphicCall_NoDiagnostic()
    {
        // ポリモーフィズムで振る舞いを委譲 — ダウンキャスト不要
        var source = Preamble + """
            abstract class SpeakAnimal { public abstract string Speak(); }
            class GoodDog : SpeakAnimal { public override string Speak() => "Woof"; }

            class Handler
            {
                void M(SpeakAnimal a) { var _ = a.Speak(); }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task NumericCast_NoDiagnostic()
    {
        // 数値変換は参照変換ではないので対象外
        var source = """
            class Foo
            {
                void M(double d) { int x = (int)d; }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task IdentityCast_NoDiagnostic()
    {
        // 同じ型への冗長なキャストはダウンキャストではない
        var source = Preamble + """
            class Foo
            {
                void M(Dog d) { var x = (Dog)d; }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task ImplicitUpcast_NoDiagnostic()
    {
        // アップキャスト（Dog → Animal）は暗黙なのでダウンキャストではない
        var source = Preamble + """
            class Foo
            {
                void M(Dog d)
                {
                    Animal a = d;           // 暗黙のアップキャスト → OK
                    Animal b = (Animal)d;   // 明示的でも変換は implicit → OK
                }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task NamespaceExclusion_NoDiagnostic_WhenConfigured()
    {
        // UI 名前空間を excludedNamespacePatterns で除外
        var source = """
            namespace App.Ui
            {
                class Animal { }
                class Dog : Animal { }

                class SampleView
                {
                    void OnEvent(Animal a)
                    {
                        var d = a as Dog;   // UI 層は除外設定で GRD006 を出さない
                    }
                }
            }
            """;
        var guardrailJson = """
            {
              "noDowncast": {
                "excludedNamespacePatterns": ["App.Ui"]
              }
            }
            """;

        var test = new CSharpAnalyzerTest<
            Guardrail.Analyzers.Analyzers.NoDowncastAnalyzer,
            Guardrail.Analyzers.Tests.NUnitVerifier>
        {
            TestCode = source,
        };
        test.TestState.AdditionalFiles.Add(("guardrail.json", guardrailJson));
        await test.RunAsync();
    }

    [Test]
    public async Task FilePathExclusion_NoDiagnostic_WhenConfigured()
    {
        // テストファイルのパス（Test0.cs 等）が pattern に一致すれば除外
        var source = Preamble + """
            class Foo
            {
                void M(Animal a) { var d = a as Dog; }
            }
            """;
        var guardrailJson = """
            {
              "noDowncast": {
                "excludedFilePatterns": ["Test0"]
              }
            }
            """;

        var test = new CSharpAnalyzerTest<
            Guardrail.Analyzers.Analyzers.NoDowncastAnalyzer,
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
    public async Task CastDowncast_ReportsDiagnostic()
    {
        var source = Preamble + """
            class Foo
            {
                void M(Animal a)
                {
                    var dog = {|GRD006:(Dog)a|};
                }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task AsDowncast_ReportsDiagnostic()
    {
        var source = Preamble + """
            class Foo
            {
                void M(Animal a)
                {
                    var dog = {|GRD006:a as Dog|};
                }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task InterfaceDowncast_ReportsDiagnostic()
    {
        // インターフェース→実装型へのキャストもダウンキャスト
        var source = Preamble + """
            class Foo
            {
                void M(IShape s)
                {
                    var c = {|GRD006:(Circle)s|};
                }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task MultipleDowncasts_ReportsDiagnosticOnEach()
    {
        var source = Preamble + """
            class Foo
            {
                void M(Animal a)
                {
                    var dog = {|GRD006:(Dog)a|};
                    var cat = {|GRD006:a as Cat|};
                }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task ObjectToStringDowncast_ReportsDiagnostic()
    {
        // object → string もダウンキャスト
        var source = """
            class Foo
            {
                void M(object o)
                {
                    var s = {|GRD006:o as string|};
                }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task DowncastWithoutExclusion_InUiNamespace_ReportsDiagnostic()
    {
        // excludedNamespacePatterns が設定されていなければ UI 名前空間でも違反
        var source = """
            namespace App.Ui
            {
                class Animal { }
                class Dog : Animal { }

                class SampleView
                {
                    void OnEvent(Animal a)
                    {
                        var d = {|GRD006:a as Dog|};
                    }
                }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }
}
