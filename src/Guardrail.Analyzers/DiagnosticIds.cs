namespace Guardrail.Analyzers;

/// <summary>Guardrail ルール ID と共通カテゴリの定数。</summary>
internal static class DiagnosticIds
{
    /// <summary>GRD001: コンストラクタは代入のみ。</summary>
    public const string ConstructorOnlyAssignments = "GRD001";

    /// <summary>GRD002: インスタンスコンストラクタは 1 つまで。</summary>
    public const string SingleConstructor          = "GRD002";

    /// <summary>GRD003: ref / out 引数禁止。</summary>
    public const string NoRefOutParameter          = "GRD003";

    /// <summary>GRD004: テストメソッドはアサーション必須。</summary>
    public const string TestMethodMustAssert       = "GRD004";

    /// <summary>GRD005: bool パラメータ禁止（flag argument）。</summary>
    public const string NoBoolParameter            = "GRD005";

    /// <summary>GRD006: ダウンキャスト禁止（UI層は設定で除外可）。</summary>
    public const string NoDowncast                 = "GRD006";

    /// <summary>GRD007: 無意味な null チェック禁止（new 直後の null 判定等）。</summary>
    public const string MeaninglessNullCheck       = "GRD007";

    /// <summary>全ルール共通カテゴリ。</summary>
    public const string Category = "Guardrail";
}
