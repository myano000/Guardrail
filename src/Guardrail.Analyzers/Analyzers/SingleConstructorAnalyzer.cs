using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Guardrail.Analyzers.Analyzers;

/// <summary>
/// GRD002: インスタンスコンストラクタは 1 つまで。
///
/// 複数のコンストラクタオーバーロードによる「何を初期化すべきか」の曖昧さを防ぐ。
/// パターン違いの生成が必要なら、目的の名前を持つファクトリメソッドで表現する。
///
/// 違反例:
///   public Foo(int x)   { _x = x; }
///   public Foo()        { _x = 0; }   // 2 つ目 → GRD002
///
/// 準拠例:
///   private Foo(int x)        { _x = x; }
///   public static Foo Create(int x) => new Foo(x);
///   public static Foo Default()     => new Foo(0);
///
/// 注意: static コンストラクタはカウント対象外。
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SingleConstructorAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor Rule = new(
        id:                 DiagnosticIds.SingleConstructor,
        title:              "インスタンスコンストラクタは 1 つまでにしてください",
        messageFormat:      "クラス '{0}' にインスタンスコンストラクタが複数あります。生成パターンの出し分けにはファクトリメソッドを使ってください。",
        category:           DiagnosticIds.Category,
        defaultSeverity:    DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:        "コンストラクタオーバーロードの乱立を防ぎ、生成の意図を名前付きファクトリメソッドで明示します.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ClassDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext ctx)
    {
        var classDecl = (ClassDeclarationSyntax)ctx.Node;

        // インスタンスコンストラクタのみ対象（static コンストラクタは除外）
        var instanceCtors = classDecl.Members
            .OfType<ConstructorDeclarationSyntax>()
            .Where(c => !c.Modifiers.Any(SyntaxKind.StaticKeyword))
            .ToList();

        if (instanceCtors.Count <= 1) return;

        var className = classDecl.Identifier.Text;

        // 2 つ目以降のコンストラクタに診断を報告
        for (var i = 1; i < instanceCtors.Count; i++)
        {
            ctx.ReportDiagnostic(
                Diagnostic.Create(Rule,
                    instanceCtors[i].Identifier.GetLocation(),
                    className));
        }
    }
}
