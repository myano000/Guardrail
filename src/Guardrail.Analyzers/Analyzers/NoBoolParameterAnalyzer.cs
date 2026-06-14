using System.Collections.Immutable;
using Guardrail.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Guardrail.Analyzers.Analyzers;

/// <summary>
/// GRD005: bool パラメータ禁止（flag argument）。
///
/// bool 引数は「このメソッドが 2 つの異なる振る舞いを持つ」ことを示す匂い。
/// 呼び出し側では <c>Process(true)</c> のようにリテラルが並び、意図が不明瞭になる。
///
/// 違反例:
///   void Send(string message, bool isUrgent)   // bool → 違反
///   void Enable(bool? feature)                 // bool? → 違反
///
/// 準拠例:
///   void Send(string message, Priority priority)  // 列挙型で意図を表現
///   void SendUrgent(string message)               // メソッドを分割
///
/// 対象外: 拡張メソッドの this 修飾子パラメータ。
///
/// 設定 (guardrail.json):
///   boolParameter.allowedMethods にメソッド名を列挙することで
///   既存の bool パラメータメソッド（例: ページング等）を例外扱いにできる。
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NoBoolParameterAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor Rule = new(
        id:                 DiagnosticIds.NoBoolParameter,
        title:              "bool パラメータは使用しないでください",
        messageFormat:      "パラメータ '{0}' が bool です。列挙型・メソッドの分割・オプション型で意図を表現してください。",
        category:           DiagnosticIds.Category,
        defaultSeverity:    DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:        "bool パラメータ（flag argument）はメソッドの意図を不明確にし、呼び出し側の可読性を下げます.");

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
                SyntaxKind.Parameter);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext ctx, GuardrailOptions options)
    {
        var param = (ParameterSyntax)ctx.Node;

        // 拡張メソッドの最初のパラメータ（this 修飾子）は対象外
        if (param.Modifiers.Any(SyntaxKind.ThisKeyword)) return;

        // bool / bool? 型でなければスキップ
        if (!IsBoolType(param.Type)) return;

        // allowedMethods に記載されたメソッドは対象外
        if (options.BoolParameterAllowedMethods.Count > 0)
        {
            var methodName = GetContainingMethodName(param);
            if (methodName != null)
            {
                foreach (var allowed in options.BoolParameterAllowedMethods)
                {
                    if (string.Equals(allowed, methodName, System.StringComparison.OrdinalIgnoreCase))
                        return;
                }
            }
        }

        ctx.ReportDiagnostic(
            Diagnostic.Create(Rule, param.Identifier.GetLocation(), param.Identifier.Text));
    }

    /// <summary>パラメータ型が bool または bool? かどうかを判定する（構文ベース）。</summary>
    private static bool IsBoolType(TypeSyntax? type)
    {
        if (type == null) return false;

        // bool
        if (type is PredefinedTypeSyntax predefined &&
            predefined.Keyword.IsKind(SyntaxKind.BoolKeyword))
            return true;

        // bool?
        if (type is NullableTypeSyntax nullable &&
            nullable.ElementType is PredefinedTypeSyntax nullableElement &&
            nullableElement.Keyword.IsKind(SyntaxKind.BoolKeyword))
            return true;

        return false;
    }

    private static string? GetContainingMethodName(ParameterSyntax param)
    {
        var list   = param.Parent as ParameterListSyntax;
        var method = list?.Parent;
        return method switch
        {
            MethodDeclarationSyntax m       => m.Identifier.Text,
            ConstructorDeclarationSyntax c  => c.Identifier.Text,
            LocalFunctionStatementSyntax lf => lf.Identifier.Text,
            _                               => null,
        };
    }
}
