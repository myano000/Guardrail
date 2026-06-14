using System.Collections.Immutable;
using Guardrail.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Guardrail.Analyzers.Analyzers;

/// <summary>
/// GRD006: ダウンキャスト禁止（UI層は設定で除外可）。
///
/// ダウンキャスト（派生型への型変換）は型システムの保証を破り、
/// 実行時例外（InvalidCastException）の原因になる。
/// 「型で分岐する」設計はオープン・クローズド原則に反し、拡張時に散弾銃手術を生む。
///
/// 違反例:
///   Animal a = GetAnimal();
///   var dog = (Dog)a;          // CastExpression ダウンキャスト → 違反
///   var cat = a as Cat;        // AsExpression ダウンキャスト   → 違反
///
/// 準拠例:
///   animal.Speak();            // ポリモーフィズムで振る舞いを委譲
///   animal.Accept(visitor);    // Visitor パターン
///
/// 対象外: 数値変換（(int)3.14）、アップキャスト（(Animal)dog）、unboxing（(int)obj）。
///
/// 設定 (guardrail.json):
///   noDowncast.excludedFilePatterns      — ファイルパスに部分一致したら除外（UI層等）
///   noDowncast.excludedNamespacePatterns — 名前空間に部分一致したら除外
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NoDowncastAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor Rule = new(
        id:                 DiagnosticIds.NoDowncast,
        title:              "ダウンキャストは使用しないでください",
        messageFormat:      "'{0}' へのダウンキャストです。型で分岐せず、ポリモーフィズムや専用メソッドで表現してください（UI層は設定で除外可）。",
        category:           DiagnosticIds.Category,
        defaultSeverity:    DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:        "ダウンキャストは型システムの保証を破り、実行時例外の原因になります. ポリモーフィズムや専用メソッドで代替してください.");

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
                nodeCtx => AnalyzeCast(nodeCtx, options),
                SyntaxKind.CastExpression);
            compilationCtx.RegisterSyntaxNodeAction(
                nodeCtx => AnalyzeAs(nodeCtx, options),
                SyntaxKind.AsExpression);
        });
    }

    // ----------------------------------------------------------------
    // Cast: (Dog)animal
    // ----------------------------------------------------------------

    private static void AnalyzeCast(SyntaxNodeAnalysisContext ctx, GuardrailOptions options)
    {
        var cast = (CastExpressionSyntax)ctx.Node;

        if (IsExcluded(ctx, options)) return;

        var targetType = ctx.SemanticModel.GetTypeInfo(cast.Type).Type;
        if (targetType == null || targetType.Kind == SymbolKind.ErrorType) return;

        var conversion = ctx.SemanticModel.ClassifyConversion(cast.Expression, targetType);
        if (!IsReferenceDowncast(conversion)) return;

        ctx.ReportDiagnostic(
            Diagnostic.Create(Rule, cast.GetLocation(), targetType.Name));
    }

    // ----------------------------------------------------------------
    // As: animal as Dog
    // ----------------------------------------------------------------

    private static void AnalyzeAs(SyntaxNodeAnalysisContext ctx, GuardrailOptions options)
    {
        var binary = (BinaryExpressionSyntax)ctx.Node;

        if (IsExcluded(ctx, options)) return;

        // binary.Right は型名として使われているが ExpressionSyntax としてパース済み
        var targetType = ctx.SemanticModel.GetTypeInfo(binary.Right).Type;
        if (targetType == null || targetType.Kind == SymbolKind.ErrorType) return;

        var conversion = ctx.SemanticModel.ClassifyConversion(binary.Left, targetType);
        if (!IsReferenceDowncast(conversion)) return;

        ctx.ReportDiagnostic(
            Diagnostic.Create(Rule, binary.GetLocation(), targetType.Name));
    }

    // ----------------------------------------------------------------
    // ダウンキャスト判定: 明示的な参照変換のみ（数値・unboxing は除外）
    // ----------------------------------------------------------------

    private static bool IsReferenceDowncast(Microsoft.CodeAnalysis.CSharp.Conversion conversion) =>
        conversion.IsExplicit && conversion.IsReference;

    // ----------------------------------------------------------------
    // 除外チェック（ファイルパスおよび名前空間パターン）
    // ----------------------------------------------------------------

    private static bool IsExcluded(SyntaxNodeAnalysisContext ctx, GuardrailOptions options)
    {
        // ファイルパスによる除外（パス区切り文字を / に正規化してから一致確認）
        if (options.DowncastExcludedFilePatterns.Count > 0)
        {
            var filePath = ctx.Node.SyntaxTree.FilePath.Replace('\\', '/');
            foreach (var pattern in options.DowncastExcludedFilePatterns)
            {
                if (filePath.IndexOf(pattern, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
        }

        // 名前空間パターンによる除外
        if (options.DowncastExcludedNamespacePatterns.Count > 0)
        {
            var ns = GetContainingNamespace(ctx.Node);
            if (ns != null)
            {
                foreach (var pattern in options.DowncastExcludedNamespacePatterns)
                {
                    if (ns.IndexOf(pattern, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }
        }

        return false;
    }

    /// <summary>ノードを囲む最も内側の名前空間宣言の名前を返す（file-scoped 対応）。</summary>
    private static string? GetContainingNamespace(SyntaxNode node)
    {
        for (var current = node.Parent; current != null; current = current.Parent)
        {
            if (current is BaseNamespaceDeclarationSyntax ns)
                return ns.Name.ToString();
        }
        return null;
    }
}
