using Guardrail.Analyzers.Analyzers;

// C# 12 ジェネリック型エイリアス: アナライザごとに Verify 型を定義する
using Verify = Microsoft.CodeAnalysis.CSharp.Testing
    .CSharpAnalyzerVerifier<
        Guardrail.Analyzers.Analyzers.ConstructorOnlyAssignmentsAnalyzer,
        Guardrail.Analyzers.Tests.NUnitVerifier>;

namespace Guardrail.Analyzers.Tests;

[TestFixture]
public class ConstructorOnlyAssignmentsAnalyzerTests
{
    // ----------------------------------------------------------------
    // 準拠ケース（診断なし）
    // ----------------------------------------------------------------

    [Test]
    public async Task EmptyConstructor_NoDiagnostic()
    {
        var source = """
            class Foo
            {
                public Foo() { }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task FieldAssignments_NoDiagnostic()
    {
        var source = """
            class Foo
            {
                private int _x;
                private string _y = "";

                public Foo(int x, string y)
                {
                    _x = x;
                    _y = y;
                }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task CompoundAssignment_NoDiagnostic()
    {
        // += 等の複合代入も AssignmentExpressionSyntax なので OK
        var source = """
            class Foo
            {
                private int _count;

                public Foo(int delta)
                {
                    _count += delta;
                }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task ExpressionBodyWithAssignment_NoDiagnostic()
    {
        var source = """
            class Foo
            {
                private int _x;
                public Foo(int x) => _x = x;
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task PropertyAssignment_NoDiagnostic()
    {
        var source = """
            class Foo
            {
                public int Value { get; }

                public Foo(int v)
                {
                    Value = v;
                }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    // ----------------------------------------------------------------
    // 違反ケース（診断あり）
    // ----------------------------------------------------------------

    [Test]
    public async Task MethodCall_ReportsDiagnostic()
    {
        // "DoSomething();" は代入ではないので GRD001
        var source = """
            class Foo
            {
                public Foo()
                {
                    {|GRD001:DoSomething();|}
                }

                void DoSomething() { }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task ThrowStatement_ReportsDiagnostic()
    {
        // コンストラクタ内の throw はファクトリで行う
        var source = """
            using System;
            class Foo
            {
                public Foo(int x)
                {
                    _x = x;
                    {|GRD001:throw new InvalidOperationException("bad");|}
                }
                int _x;
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task IfStatement_ReportsDiagnostic()
    {
        var source = """
            class Foo
            {
                private int _x;

                public Foo(int x)
                {
                    {|GRD001:if (x < 0) { _x = 0; } else { _x = x; }|}
                }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task ExpressionBodyNonAssignment_ReportsDiagnostic()
    {
        // 式本体が代入でない場合も GRD001
        var source = """
            class Foo
            {
                public Foo() => {|GRD001:DoSomething()|};
                void DoSomething() { }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    [Test]
    public async Task MultipleStatements_OnlyNonAssignmentFlagged()
    {
        // 代入はOK、メソッド呼び出しのみ GRD001
        var source = """
            class Foo
            {
                private int _x;

                public Foo(int x)
                {
                    _x = x;
                    {|GRD001:Initialize();|}
                }

                void Initialize() { }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }
}
