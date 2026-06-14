using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Guardrail.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Guardrail.Analyzers.Analyzers;

/// <summary>
/// GRD004: テストメソッドにはアサーションが必須。
///
/// テスト属性（[Test], [Fact] 等）が付いたメソッドの本体に
/// アサーション呼び出しが 1 件も無い場合に警告する。
/// 「アサーションなし＝常に緑」というフォールスポジティブなテストを防ぐ。
///
/// アサーションの判定基準（いずれかに該当すれば OK）:
///   1. 呼び出しレシーバの型名が assertionTypeNames に含まれる
///      例: Assert.That(...), ClassicAssert.AreEqual(...)
///   2. メソッド名が assertionMethodPatterns のいずれかを含む
///      例: result.Should().Be(5) → "Should" を含む
///
/// 設定 (guardrail.json):
///   testMethodMustAssert.testAttributes        - テスト属性名リスト
///   testMethodMustAssert.assertionTypeNames    - アサーション型名リスト
///   testMethodMustAssert.assertionMethodPatterns - アサーションパターンリスト
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TestMethodMustAssertAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor Rule = new(
        id:                 DiagnosticIds.TestMethodMustAssert,
        title:              "テストメソッドにはアサーションが必要です",
        messageFormat:      "テストメソッド '{0}' にアサーション呼び出しがありません。Assert.That(...) 等でアサーションを追加してください。",
        category:           DiagnosticIds.Category,
        defaultSeverity:    DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:        "テストにアサーションがないと常に緑になり、バグを見逃します.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(compilationCtx =>
        {
            var options = GuardrailOptions.Load(compilationCtx.Options.AdditionalFiles);
            compilationCtx.RegisterSyntaxNodeAction(
                nodeCtx => Analyze(nodeCtx, options),
                SyntaxKind.MethodDeclaration);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext ctx, GuardrailOptions options)
    {
        var method = (MethodDeclarationSyntax)ctx.Node;

        // テスト属性が付いているか確認
        if (!HasTestAttribute(method, options.TestAttributes)) return;

        // ブロック本体または式本体の中でアサーション呼び出しを探す
        IEnumerable<InvocationExpressionSyntax> invocations =
            method.Body?.DescendantNodes().OfType<InvocationExpressionSyntax>()
            ?? method.ExpressionBody?.DescendantNodes().OfType<InvocationExpressionSyntax>()
            ?? Enumerable.Empty<InvocationExpressionSyntax>();

        if (ContainsAssertion(invocations, options)) return;

        ctx.ReportDiagnostic(
            Diagnostic.Create(Rule,
                method.Identifier.GetLocation(),
                method.Identifier.Text));
    }

    // ----------------------------------------------------------------
    // テスト属性の検出（構文ベース）
    // ----------------------------------------------------------------

    private static bool HasTestAttribute(
        MethodDeclarationSyntax method,
        IReadOnlyList<string> testAttributes)
    {
        foreach (var attrList in method.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var name = NormalizeAttributeName(attr.Name.ToString());
                if (ContainsIgnoreCase(testAttributes, name))
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 属性名を正規化する。
    ///   "NUnit.Framework.TestAttribute" → "Test"
    ///   "TestCase"                      → "TestCase"
    /// </summary>
    private static string NormalizeAttributeName(string name)
    {
        // 修飾子を除去（最後のセグメントを取得）
        var dot = name.LastIndexOf('.');
        if (dot >= 0) name = name.Substring(dot + 1);

        // "Attribute" サフィックスを除去
        if (name.EndsWith("Attribute", System.StringComparison.Ordinal) && name.Length > "Attribute".Length)
            name = name.Substring(0, name.Length - "Attribute".Length);

        return name;
    }

    // ----------------------------------------------------------------
    // アサーション呼び出しの検出（構文ベース）
    // ----------------------------------------------------------------

    private static bool ContainsAssertion(
        IEnumerable<InvocationExpressionSyntax> invocations,
        GuardrailOptions options)
    {
        foreach (var inv in invocations)
        {
            if (IsAssertionInvocation(inv, options))
                return true;
        }
        return false;
    }

    private static bool IsAssertionInvocation(
        InvocationExpressionSyntax inv,
        GuardrailOptions options)
    {
        switch (inv.Expression)
        {
            // Assert.That(...) / ClassicAssert.AreEqual(...) 等
            case MemberAccessExpressionSyntax ma:
            {
                // レシーバ名でアサーション型チェック
                var receiverText = ma.Expression.ToString();
                var receiverSimple = SimpleName(receiverText);
                if (ContainsIgnoreCase(options.AssertionTypeNames, receiverSimple))
                    return true;

                // メソッド名パターンチェック（Should 等）
                var methodName = ma.Name.Identifier.Text;
                if (MatchesPattern(methodName, options.AssertionMethodPatterns))
                    return true;

                break;
            }

            // Assert(condition) のような直接呼び出し
            case IdentifierNameSyntax id:
            {
                if (ContainsIgnoreCase(options.AssertionTypeNames, id.Identifier.Text))
                    return true;
                if (MatchesPattern(id.Identifier.Text, options.AssertionMethodPatterns))
                    return true;
                break;
            }
        }

        return false;
    }

    private static string SimpleName(string qualifiedName)
    {
        var dot = qualifiedName.LastIndexOf('.');
        return dot >= 0 ? qualifiedName.Substring(dot + 1) : qualifiedName;
    }

    private static bool MatchesPattern(string methodName, IReadOnlyList<string> patterns)
    {
        foreach (var pattern in patterns)
        {
            if (methodName.IndexOf(pattern, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    private static bool ContainsIgnoreCase(IReadOnlyList<string> list, string value)
    {
        foreach (var item in list)
        {
            if (string.Equals(item, value, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
