using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Guardrail.Analyzers.Analyzers;

/// <summary>
/// GRD008: bool を偽装した enum 禁止。
///
/// GRD005 の抜け穴対策。bool パラメータを避けるために
/// メンバーが 2 つだけの enum を定義し、実質的に bool と同じ使い方をするパターンを検出する。
///
/// 違反例:
///   enum Boolean  { TRUE, FALSE }
///   enum Switch   { On, Off }
///   enum Answer   { Yes, No }
///   enum Toggle   { Enabled, Disabled }
///
/// 準拠例:
///   enum Priority  { Normal, Urgent }   // 意味のある選択肢 → OK
///   enum Direction { Left, Right }      // ドメイン概念     → OK
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BooleanDisguisedAsEnumAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor Rule = new(
        id:                 DiagnosticIds.BooleanDisguisedAsEnum,
        title:              "bool を偽装した enum は使用しないでください",
        messageFormat:      "enum '{0}' はメンバー '{1}' と '{2}' を持ち、bool の言い換えです。bool を使うか、ドメインの概念を表す別の名前を選んでください。",
        category:           DiagnosticIds.Category,
        defaultSeverity:    DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:        "TRUE/FALSE, YES/NO, ON/OFF などの 2 値 enum は bool の偽装です. bool を直接使うか、意味のあるドメイン名で代替してください.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    // bool の言い換えとみなすメンバー名ペア（正規化済み小文字）
    private static readonly HashSet<(string, string)> s_boolPairs =
    [
        ("true",    "false"),
        ("yes",     "no"),
        ("on",      "off"),
        ("enabled", "disabled"),
        ("enable",  "disable"),
        ("active",  "inactive"),
    ];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.EnumDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext ctx)
    {
        var enumDecl = (EnumDeclarationSyntax)ctx.Node;
        var members  = enumDecl.Members;

        if (members.Count != 2) return;

        var a = members[0].Identifier.Text;
        var b = members[1].Identifier.Text;

        if (!IsBoolPair(a, b)) return;

        ctx.ReportDiagnostic(
            Diagnostic.Create(Rule,
                enumDecl.Identifier.GetLocation(),
                enumDecl.Identifier.Text, a, b));
    }

    private static bool IsBoolPair(string a, string b)
    {
        var al = a.ToLowerInvariant();
        var bl = b.ToLowerInvariant();

        return s_boolPairs.Contains((al, bl))
            || s_boolPairs.Contains((bl, al));
    }
}
