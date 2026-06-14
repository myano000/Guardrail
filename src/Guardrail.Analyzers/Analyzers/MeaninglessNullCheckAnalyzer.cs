using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Guardrail.Analyzers.Analyzers;

/// <summary>
/// GRD007: 無意味な null チェック禁止（new 直後の null 判定等）。
///
/// オブジェクト生成式（new）は常に非 null を返す。
/// その直接の結果、あるいはその値を代入した（再代入されていない）ローカル変数に対して
/// null チェックを行うのは、本質的に意味がない。
///
/// 違反例（パターン1 — 生成式を直接 null 判定）:
///   if (new Foo() == null) ...      // 常に false
///   if (new Foo() != null) ...      // 常に true
///   new Foo() ?? fallback           // 右辺は絶対に使われない
///   if (new Foo() is null) ...      // 常に false
///
/// 違反例（パターン2 — new で初期化したローカルの null 判定）:
///   var x = new Foo();
///   if (x == null) ...              // x は null になれない
///   if (x is not null) ...          // 常に true
///
/// 準拠例:
///   Foo? x = GetFooOrNull();
///   if (x == null) ...              // 外部から受け取った値 → 検査が必要
///
///   Foo? x = new Foo();
///   x = null;                       // 再代入がある → 安全のためスキップ
///   if (x == null) ...
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MeaninglessNullCheckAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor Rule = new(
        id:                 DiagnosticIds.MeaninglessNullCheck,
        title:              "この null チェックは無意味です",
        messageFormat:      "この値は null になり得ません。不要な null チェックを削除してください。",
        category:           DiagnosticIds.Category,
        defaultSeverity:    DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:        "new で生成した値は常に非 null です. null チェックを削除し、コードの意図を明確にしてください.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.EqualsExpression,
            SyntaxKind.NotEqualsExpression,
            SyntaxKind.IsPatternExpression,
            SyntaxKind.CoalesceExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext ctx)
    {
        switch (ctx.Node)
        {
            case BinaryExpressionSyntax binary:
                AnalyzeBinary(ctx, binary);
                break;

            case IsPatternExpressionSyntax isPattern:
                AnalyzeIsPattern(ctx, isPattern);
                break;
        }
    }

    // ----------------------------------------------------------------
    // x == null / null == x / x != null / null != x / x ?? y
    // ----------------------------------------------------------------

    private static void AnalyzeBinary(SyntaxNodeAnalysisContext ctx, BinaryExpressionSyntax binary)
    {
        ExpressionSyntax? candidate = null;

        if (binary.IsKind(SyntaxKind.EqualsExpression) ||
            binary.IsKind(SyntaxKind.NotEqualsExpression))
        {
            if (IsNullLiteral(binary.Right))  candidate = binary.Left;
            else if (IsNullLiteral(binary.Left)) candidate = binary.Right;
        }
        else if (binary.IsKind(SyntaxKind.CoalesceExpression))
        {
            candidate = binary.Left;
        }

        if (candidate == null) return;
        CheckCandidate(ctx, candidate, binary.GetLocation());
    }

    // ----------------------------------------------------------------
    // x is null / x is not null
    // ----------------------------------------------------------------

    private static void AnalyzeIsPattern(SyntaxNodeAnalysisContext ctx, IsPatternExpressionSyntax isPattern)
    {
        if (!IsNullPattern(isPattern.Pattern)) return;
        CheckCandidate(ctx, isPattern.Expression, isPattern.GetLocation());
    }

    /// <summary>パターンが null または not null を表すかどうかを判定する。</summary>
    private static bool IsNullPattern(PatternSyntax pattern)
    {
        // x is null
        if (pattern is ConstantPatternSyntax constant &&
            IsNullLiteral(constant.Expression))
            return true;

        // x is not null（C# 9+）
        if (pattern is UnaryPatternSyntax unary &&
            unary.OperatorToken.IsKind(SyntaxKind.NotKeyword) &&
            unary.Pattern is ConstantPatternSyntax innerConstant &&
            IsNullLiteral(innerConstant.Expression))
            return true;

        return false;
    }

    // ----------------------------------------------------------------
    // 候補式の評価
    // ----------------------------------------------------------------

    private static void CheckCandidate(
        SyntaxNodeAnalysisContext ctx,
        ExpressionSyntax candidate,
        Location reportLocation)
    {
        // パターン1: 生成式そのものを null 判定している
        if (IsCreationExpression(candidate))
        {
            ctx.ReportDiagnostic(Diagnostic.Create(Rule, reportLocation));
            return;
        }

        // パターン2: new で初期化したローカル変数（再代入なし）を null 判定している
        if (candidate is IdentifierNameSyntax identifier)
        {
            CheckLocalVariable(ctx, identifier, reportLocation);
        }
    }

    private static void CheckLocalVariable(
        SyntaxNodeAnalysisContext ctx,
        IdentifierNameSyntax identifier,
        Location reportLocation)
    {
        // シンボルを取得してローカル変数か確認
        var symbol = ctx.SemanticModel.GetSymbolInfo(identifier, ctx.CancellationToken).Symbol
            as ILocalSymbol;
        if (symbol == null) return;

        // 宣言の初期化式を取得
        VariableDeclaratorSyntax? declarator = null;
        foreach (var syntaxRef in symbol.DeclaringSyntaxReferences)
        {
            if (syntaxRef.GetSyntax(ctx.CancellationToken) is VariableDeclaratorSyntax vd)
            {
                declarator = vd;
                break;
            }
        }

        if (declarator?.Initializer?.Value == null) return;
        if (!IsCreationExpression(declarator.Initializer.Value)) return;

        // 含まれるスコープを探す
        var scope = GetContainingScope(identifier);
        if (scope == null) return;

        // スコープ内で当該ローカルへの再代入がなければ報告
        if (!IsReassigned(symbol, scope, ctx.SemanticModel, ctx.CancellationToken))
        {
            ctx.ReportDiagnostic(Diagnostic.Create(Rule, reportLocation));
        }
    }

    // ----------------------------------------------------------------
    // ヘルパー
    // ----------------------------------------------------------------

    private static bool IsCreationExpression(ExpressionSyntax expr) =>
        expr is ObjectCreationExpressionSyntax
             or ImplicitObjectCreationExpressionSyntax
             or ArrayCreationExpressionSyntax
             or ImplicitArrayCreationExpressionSyntax;

    private static bool IsNullLiteral(ExpressionSyntax expr) =>
        expr is LiteralExpressionSyntax lit &&
        lit.IsKind(SyntaxKind.NullLiteralExpression);

    /// <summary>ローカル変数を囲む最も近いメソッド/コンストラクタ/ローカル関数/ラムダを返す。</summary>
    private static SyntaxNode? GetContainingScope(SyntaxNode node)
    {
        for (var current = node.Parent; current != null; current = current.Parent)
        {
            switch (current)
            {
                case MethodDeclarationSyntax:
                case ConstructorDeclarationSyntax:
                case LocalFunctionStatementSyntax:
                case AnonymousFunctionExpressionSyntax:
                    return current;
            }
        }
        return null;
    }

    /// <summary>スコープ内でローカル変数 <paramref name="local"/> に再代入があるかを確認する。</summary>
    private static bool IsReassigned(
        ILocalSymbol local,
        SyntaxNode scope,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        foreach (var node in scope.DescendantNodes())
        {
            if (node is AssignmentExpressionSyntax assignment &&
                assignment.Left is IdentifierNameSyntax leftId)
            {
                var sym = semanticModel.GetSymbolInfo(leftId, cancellationToken).Symbol;
                if (SymbolEqualityComparer.Default.Equals(sym, local))
                    return true;
            }
        }
        return false;
    }
}
