using System.Collections.Immutable;
using Guardrail.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Guardrail.Analyzers.Analyzers;

/// <summary>
/// GRD003: ref / out 引数禁止。
///
/// ref / out は呼び出し元の変数を書き換える副作用を生み、
/// コードの追跡を困難にする。複数値を返したい場合はタプルや専用の結果型を使う。
///
/// 違反例:
///   void Parse(string s, out int result)    // out → 違反
///   void Increment(ref int counter)         // ref → 違反
///
/// 準拠例:
///   (bool Success, int Value) TryParse(string s)   // タプルで返す
///   int Increment(int counter) => counter + 1;     // 値を返す
///
/// 対象外: in 修飾子（読み取り専用参照）、params、拡張メソッドの this。
///
/// 設定 (guardrail.json):
///   noRefOutParameter.allowedMethods にメソッド名を列挙することで
///   特定のメソッド（例: "TryParse" 系のラッパー）を例外扱いにできる。
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NoRefOutParameterAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor Rule = new(
        id:                 DiagnosticIds.NoRefOutParameter,
        title:              "ref / out 引数は使用しないでください",
        messageFormat:      "パラメータ '{0}' に '{1}' 修飾子があります。タプルや結果型で複数値を返すようにしてください。",
        category:           DiagnosticIds.Category,
        defaultSeverity:    DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:        "ref / out による呼び出し元変数の書き換えを禁止し、関数を純粋な入出力関係に保ちます.");

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

        // ref / out を走査（LINQ 非依存: netstandard2.0 + ImplicitUsings=disable でも安定）
        bool hasRef = false, hasOut = false;
        foreach (var mod in param.Modifiers)
        {
            if (mod.IsKind(SyntaxKind.RefKeyword)) hasRef = true;
            if (mod.IsKind(SyntaxKind.OutKeyword)) hasOut = true;
        }
        if (!hasRef && !hasOut) return;

        // 拡張メソッドの最初のパラメータ（this 修飾子）は対象外
        if (param.Modifiers.Any(SyntaxKind.ThisKeyword)) return;

        // allowedMethods に記載されたメソッドは対象外
        if (options.AllowedRefOutMethods.Count > 0)
        {
            var methodName = GetContainingMethodName(param);
            if (methodName != null)
            {
                foreach (var allowed in options.AllowedRefOutMethods)
                {
                    if (string.Equals(allowed, methodName, System.StringComparison.OrdinalIgnoreCase))
                        return;
                }
            }
        }

        var modifierText = hasRef ? "ref" : "out";
        var paramName    = param.Identifier.Text;

        // 識別子（パラメータ名）の位置に診断を報告する
        // → IDE でパラメータ名がハイライトされ、修正箇所が一目でわかる
        ctx.ReportDiagnostic(
            Diagnostic.Create(Rule, param.Identifier.GetLocation(), paramName, modifierText));
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
