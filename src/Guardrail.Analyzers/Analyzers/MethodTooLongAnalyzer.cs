using System.Collections.Immutable;
using System.Text;
using Guardrail.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Guardrail.Analyzers.Analyzers;

/// <summary>
/// GRD009: メソッドが長すぎる（責務を見直し分割を検討）。
///
/// メソッドが長くなるほど「複数の責務を持っている」可能性が高くなる。
/// AI 支援コーディングでは指示しなければメソッドに処理が追加され続ける傾向があるため、
/// 機械的にしきい値を超えた段階で「単一責務か見直し・分割」を促す。
///
/// 判定: ステートメント数 OR 物理行数のどちらかがしきい値を超えた場合に発火。
/// expression-bodied メソッド・abstract/extern（body 無し）はスキップ。
///
/// 違反例:
///   void Process() { /* 31 ステートメント以上 */ }   // ステートメント数超過
///   void Render()  { /* 41 行以上 */               }  // 行数超過
///
/// 準拠例:
///   void Validate() { /* 数ステートメント */ }   // 短いメソッド
///   bool IsValid(string s) => !string.IsNullOrEmpty(s);  // 式形式はスキップ
///
/// 設定 (guardrail.json):
///   "methodLength": {
///     "maxStatements": 30,   // ステートメント数しきい値（デフォルト 30）
///     "maxLines":      40    // 物理行数しきい値（デフォルト 40）
///   }
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MethodTooLongAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor Rule = new(
        id:                 DiagnosticIds.MethodTooLong,
        title:              "Method is too long",
        messageFormat:      "Method '{0}' is too long ({1}). Review whether it has a single responsibility and consider splitting it into meaningful, focused methods.",
        category:           DiagnosticIds.Category,
        defaultSeverity:    DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:        "As methods grow longer they are more likely to have multiple responsibilities. Split them into focused units so each method does one thing.");

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
                SyntaxKind.MethodDeclaration,
                SyntaxKind.LocalFunctionStatement);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext ctx, GuardrailOptions options)
    {
        BlockSyntax? body;
        SyntaxToken  identifier;

        switch (ctx.Node)
        {
            case MethodDeclarationSyntax method:
                body       = method.Body;
                identifier = method.Identifier;
                break;
            case LocalFunctionStatementSyntax localFunc:
                body       = localFunc.Body;
                identifier = localFunc.Identifier;
                break;
            default:
                return;
        }

        // expression-bodied / abstract / extern / partial 宣言など body 無しはスキップ
        if (body == null) return;

        var statementCount = body.Statements.Count;
        var lineSpan       = body.GetLocation().GetLineSpan();
        var lineCount      = lineSpan.EndLinePosition.Line - lineSpan.StartLinePosition.Line + 1;

        var statementsOver = statementCount > options.MethodMaxStatements;
        var linesOver      = lineCount      > options.MethodMaxLines;

        if (!statementsOver && !linesOver) return;

        var detail = BuildDetail(
            statementsOver, statementCount, options.MethodMaxStatements,
            linesOver,      lineCount,      options.MethodMaxLines);

        ctx.ReportDiagnostic(
            Diagnostic.Create(Rule, identifier.GetLocation(), identifier.Text, detail));
    }

    private static string BuildDetail(
        bool statementsOver, int statementCount, int maxStatements,
        bool linesOver,      int lineCount,      int maxLines)
    {
        var sb = new StringBuilder();

        if (statementsOver)
            sb.Append($"statements {statementCount} > {maxStatements}");

        if (linesOver)
        {
            if (sb.Length > 0) sb.Append(", ");
            sb.Append($"lines {lineCount} > {maxLines}");
        }

        return sb.ToString();
    }
}
