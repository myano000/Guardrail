using Verify = Microsoft.CodeAnalysis.CSharp.Testing
    .CSharpAnalyzerVerifier<
        Guardrail.Analyzers.Analyzers.TestMethodMustAssertAnalyzer,
        Guardrail.Analyzers.Tests.NUnitVerifier>;

namespace Guardrail.Analyzers.Tests;

[TestFixture]
public class TestMethodMustAssertAnalyzerTests
{
    // ----------------------------------------------------------------
    // テスト属性スタブ + アサーションスタブを含む共通プリアンブル
    //
    // 実際のプロジェクトでは NUnit.Framework.TestAttribute / Assert を使うが、
    // テストコード内で完結させるためにスタブを inline 定義する。
    // アナライザは属性名 "Test" を構文的に検出するため、スタブでも動作する。
    // ----------------------------------------------------------------
    private const string Preamble = """
        using System;

        [AttributeUsage(AttributeTargets.Method)]
        class TestAttribute : Attribute { }

        [AttributeUsage(AttributeTargets.Method)]
        class FactAttribute : Attribute { }

        [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
        class TestCaseAttribute : Attribute
        {
            public TestCaseAttribute(params object[] args) { }
        }

        static class Assert
        {
            public static void That(bool condition) { }
            public static void AreEqual<T>(T expected, T actual) { }
            public static void IsTrue(bool condition) { }
        }

        """;

    // ----------------------------------------------------------------
    // 準拠ケース（診断なし）
    // ----------------------------------------------------------------

    [Test]
    public async Task TestWithAssertThat_NoDiagnostic()
    {
        var source = Preamble + """
            class Tests
            {
                [Test]
                public void Add_ReturnsCorrectSum()
                {
                    int result = 1 + 1;
                    Assert.That(result == 2);
                }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task TestWithAreEqual_NoDiagnostic()
    {
        var source = Preamble + """
            class Tests
            {
                [Test]
                public void Add_ReturnsTwo()
                {
                    Assert.AreEqual(2, 1 + 1);
                }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task FactWithAssert_NoDiagnostic()
    {
        // xUnit スタイルの [Fact] も検出する
        var source = Preamble + """
            class Tests
            {
                [Fact]
                public void Fact_WithAssert()
                {
                    Assert.IsTrue(true);
                }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task NonTestMethod_NoDiagnostic()
    {
        // テスト属性なし → GRD004 の対象外
        var source = Preamble + """
            class Foo
            {
                public void JustAHelper()
                {
                    int x = 1 + 1;
                    // アサーションなし、でも [Test] ないので OK
                }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task TestWithFluentAssertion_NoDiagnostic()
    {
        // "Should" を含むメソッド名はアサーションとみなす（assertionMethodPatterns）
        var source = Preamble + """
            static class FluentExtensions
            {
                public static bool ShouldBe(this int actual, int expected) =>
                    actual == expected;
            }

            class Tests
            {
                [Test]
                public void Add_ShouldReturnTwo()
                {
                    (1 + 1).ShouldBe(2);
                }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task TestCaseWithAssert_NoDiagnostic()
    {
        var source = Preamble + """
            class Tests
            {
                [TestCase(1, 2, 3)]
                public void Add_ReturnsSum(int a, int b, int expected)
                {
                    Assert.AreEqual(expected, a + b);
                }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    // ----------------------------------------------------------------
    // 違反ケース（診断あり）
    // ----------------------------------------------------------------

    [Test]
    public async Task TestWithNoAssert_ReportsDiagnostic()
    {
        var source = Preamble + """
            class Tests
            {
                [Test]
                public void {|GRD004:AlwaysGreen|}()
                {
                    int x = 1 + 1;
                    // アサーションなし → テストは常に緑 → バグ見逃し
                }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task TestWithOnlyLocalLogic_ReportsDiagnostic()
    {
        var source = Preamble + """
            class Tests
            {
                [Test]
                public void {|GRD004:EmptyArrange|}()
                {
                    var result = Compute();
                    // result を使っているが assert していない
                }

                static int Compute() => 42;
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task FactWithNoAssert_ReportsDiagnostic()
    {
        var source = Preamble + """
            class Tests
            {
                [Fact]
                public void {|GRD004:FactWithNoAssert|}() { }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task TestWithCustomAssertionType_NoDiagnostic_WhenConfigured()
    {
        // guardrail.json で "MyAssert" をアサーション型として設定した場合
        var source = Preamble + """
            static class MyAssert
            {
                public static void Check(bool condition) { }
            }

            class Tests
            {
                [Test]
                public void CustomAssertionTest()
                {
                    MyAssert.Check(1 + 1 == 2);
                }
            }
            """;

        // guardrail.json を AdditionalFiles として渡す
        var guardrailJson = """
            {
              "testMethodMustAssert": {
                "testAttributes": ["Test"],
                "assertionTypeNames": ["Assert", "MyAssert"],
                "assertionMethodPatterns": ["Should"]
              }
            }
            """;

        var test = new CSharpAnalyzerTest<
            Guardrail.Analyzers.Analyzers.TestMethodMustAssertAnalyzer,
            Guardrail.Analyzers.Tests.NUnitVerifier>
        {
            TestCode = source,
        };
        test.TestState.AdditionalFiles.Add(("guardrail.json", guardrailJson));
        await test.RunAsync();
    }
}
