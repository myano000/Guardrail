using System.Collections.Immutable;
using Guardrail.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Guardrail.Analyzers.Analyzers;

/// <summary>
/// GRD001: コンストラクタ本体は単純な代入のみ許可する。
///
/// 違反例:
///   public Foo() { Log("created"); }      // メソッド呼び出し → 違反
///   public Foo() { if (x &gt; 0) { ... } }  // 制御フロー → 違反
///
/// 準拠例:
///   public Foo(int x) { _x = x; }        // 代入のみ → OK
///   public Foo(int x) => _x = x;         // 式本体で代入 → OK
///
/// 設定 (guardrail.json):
///   constructorOnlyAssignments.allowedInvocations に許可メソッド名を列挙することで
///   特定の初期化メソッド呼び出し（例: "InitializeComponent"）だけを例外扱いにできる。
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConstructorOnlyAssignmentsAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor Rule = new(
        id:                 DiagnosticIds.ConstructorOnlyAssignments,
        title:              "コンストラクタには代入のみ記述してください",
        messageFormat:      "コンストラクタに代入以外の処理があります ({0})。初期化ロジックはファクトリメソッドか別メソッドに移してください。",
        category:           DiagnosticIds.Category,
        defaultSeverity:    DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:        "コンストラクタ本体はフィールド/プロパティへの代入のみに限定することで、テスト容易性とライフサイクルの透明性を高めます.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        // オプションはコンパイル開始時に一度だけ読み込む
        context.RegisterCompilationStartAction(compilationCtx =>
        {
            var options = GuardrailOptions.Load(compilationCtx.Options.AdditionalFiles);
            compilationCtx.RegisterSyntaxNodeAction(
                nodeCtx => Analyze(nodeCtx, options),
                SyntaxKind.ConstructorDeclaration);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext ctx, GuardrailOptions options)
    {
        var ctor = (ConstructorDeclarationSyntax)ctx.Node;

        // --- 式本体コンストラクタ (public Foo() => expr;) ---
        if (ctor.ExpressionBody is { } exprBody)
        {
            if (exprBody.Expression is not AssignmentExpressionSyntax)
            {
                ReportOn(ctx, exprBody.Expression, "式本体が代入ではありません");
            }
            return;
        }

        // --- ブロック本体 ---
        if (ctor.Body is null) return;

        foreach (var stmt in ctor.Body.Statements)
        {
            if (IsAllowedStatement(stmt, options)) continue;
            ReportOn(ctx, stmt, DescribeStatement(stmt));
        }
    }

    /// <summary>許可された文かどうかを判定する。</summary>
    private static bool IsAllowedStatement(StatementSyntax stmt, GuardrailOptions options)
    {
        // 代入式文は常に OK
        if (stmt is ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax })
            return true;

        // allowedInvocations に列挙されたメソッド呼び出し式文は OK
        if (stmt is ExpressionStatementSyntax { Expression: InvocationExpressionSyntax invocation })
        {
            var methodName = GetMethodName(invocation);
            if (methodName != null && ContainsIgnoreCase(options.AllowedCtorInvocations, methodName))
                return true;
        }

        return false;
    }

    private static string? GetMethodName(InvocationExpressionSyntax invocation) =>
        invocation.Expression switch
        {
            IdentifierNameSyntax id       => id.Identifier.Text,
            MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
            _                             => null,
        };

    private static string DescribeStatement(StatementSyntax stmt) =>
        stmt switch
        {
            ExpressionStatementSyntax e when e.Expression is InvocationExpressionSyntax
                => $"メソッド呼び出し: {e.Expression.ToString().TrimToLength(40)}",
            IfStatementSyntax         => "if 文",
            WhileStatementSyntax      => "while 文",
            ForStatementSyntax        => "for 文",
            ForEachStatementSyntax    => "foreach 文",
            ThrowStatementSyntax      => "throw 文（バリデーションはファクトリで行う）",
            ReturnStatementSyntax     => "return 文",
            _                         => stmt.Kind().ToString(),
        };

    private static void ReportOn(SyntaxNodeAnalysisContext ctx, SyntaxNode node, string reason)
    {
        ctx.ReportDiagnostic(
            Diagnostic.Create(Rule, node.GetLocation(), reason));
    }

    private static bool ContainsIgnoreCase(System.Collections.Generic.IReadOnlyList<string> list, string value)
    {
        foreach (var item in list)
            if (string.Equals(item, value, System.StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}

internal static class StringExtensions
{
    public static string TrimToLength(this string s, int maxLength) =>
        s.Length <= maxLength ? s : s.Substring(0, maxLength) + "…";
}
