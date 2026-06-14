using Verify = Microsoft.CodeAnalysis.CSharp.Testing
    .CSharpAnalyzerVerifier<
        Guardrail.Analyzers.Analyzers.MeaninglessNullCheckAnalyzer,
        Guardrail.Analyzers.Tests.NUnitVerifier>;

namespace Guardrail.Analyzers.Tests;

[TestFixture]
public class MeaninglessNullCheckAnalyzerTests
{
    // ----------------------------------------------------------------
    // 準拠ケース（診断なし）
    // ----------------------------------------------------------------

    [Test]
    public async Task NullCheckOnParameter_NoDiagnostic()
    {
        // 引数として受け取った値の null チェックは意味がある
        var source = """
            class Foo { }
            class Service
            {
                void Process(Foo? foo)
                {
                    if (foo == null) return;
                }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task NullCheckOnFactoryResult_NoDiagnostic()
    {
        // ファクトリ/外部メソッドが null を返す可能性があるので意味がある
        var source = """
            class Foo { }
            static class Factory { public static Foo? Create() => null; }
            class Service
            {
                void Process()
                {
                    var foo = Factory.Create();
                    if (foo == null) return;  // factory は null を返す可能性あり → OK
                }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task NullCheckOnReassignedLocal_NoDiagnostic()
    {
        // new で初期化した後に再代入されている場合は保守的にスキップ
        var source = """
            class Foo { }
            class Service
            {
                void Process(bool flag)
                {
                    Foo? x = new Foo();
                    if (flag) x = null;          // 再代入 → null になり得る
                    if (x == null) return;       // 安全なので報告しない
                }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task RegularEqualityCheck_NoDiagnostic()
    {
        // null と比較していない通常の等値チェックは対象外
        var source = """
            class Foo { }
            class Service
            {
                void Process(Foo a, Foo b)
                {
                    if (a == b) return;   // null との比較ではない
                }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task IsPatternWithNonNullPattern_NoDiagnostic()
    {
        // is SomeType の型パターンは対象外（null チェックではない）
        var source = """
            class Animal { }
            class Dog : Animal { public void Bark() { } }
            class Service
            {
                void Process(Animal a)
                {
                    if (a is Dog dog) dog.Bark();   // 型パターン → 対象外
                }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    // ----------------------------------------------------------------
    // 違反ケース — パターン1: 生成式を直接 null 判定
    // ----------------------------------------------------------------

    [Test]
    public async Task DirectNewEqualsNull_ReportsDiagnostic()
    {
        var source = """
            class Foo { }
            class Test
            {
                void M()
                {
                    if ({|GRD007:new Foo() == null|}) { }
                }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task DirectNewNotEqualsNull_ReportsDiagnostic()
    {
        var source = """
            class Foo { }
            class Test
            {
                void M()
                {
                    if ({|GRD007:new Foo() != null|}) { }
                }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task NullEqualsDirectNew_ReportsDiagnostic()
    {
        // null が左側に来るパターンも検出
        var source = """
            class Foo { }
            class Test
            {
                void M()
                {
                    if ({|GRD007:null == new Foo()|}) { }
                }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task DirectNewIsNullPattern_ReportsDiagnostic()
    {
        var source = """
            class Foo { }
            class Test
            {
                void M()
                {
                    if ({|GRD007:new Foo() is null|}) { }
                }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task DirectNewIsNotNullPattern_ReportsDiagnostic()
    {
        // C# 9 の is not null パターン
        var source = """
            class Foo { }
            class Test
            {
                void M()
                {
                    if ({|GRD007:new Foo() is not null|}) { }
                }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task DirectArrayCreationEqualsNull_ReportsDiagnostic()
    {
        // 配列生成も常に非 null
        var source = """
            class Test
            {
                void M()
                {
                    if ({|GRD007:new int[5] == null|}) { }
                }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    // ----------------------------------------------------------------
    // 違反ケース — パターン2: new 初期化ローカルの null 判定
    // ----------------------------------------------------------------

    [Test]
    public async Task LocalNewEqualsNull_ReportsDiagnostic()
    {
        var source = """
            class Foo { }
            class Test
            {
                void M()
                {
                    var x = new Foo();
                    if ({|GRD007:x == null|}) { }   // x は絶対に null にならない
                }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task LocalNewNotEqualsNull_ReportsDiagnostic()
    {
        var source = """
            class Foo { }
            class Test
            {
                void M()
                {
                    var x = new Foo();
                    if ({|GRD007:x != null|}) { }   // 常に true
                }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task LocalNewIsNullPattern_ReportsDiagnostic()
    {
        var source = """
            class Foo { }
            class Test
            {
                void M()
                {
                    var x = new Foo();
                    if ({|GRD007:x is null|}) { }   // 常に false
                }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task LocalNewIsNotNullPattern_ReportsDiagnostic()
    {
        var source = """
            class Foo { }
            class Test
            {
                void M()
                {
                    var x = new Foo();
                    if ({|GRD007:x is not null|}) { }   // 常に true
                }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }
}
