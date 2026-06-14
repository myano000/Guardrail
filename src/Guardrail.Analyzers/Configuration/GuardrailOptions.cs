using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Guardrail.Analyzers.Configuration;

/// <summary>
/// guardrail.json (AdditionalFiles) から読み取った設定。
/// 見つからない場合や解析エラーの場合は <see cref="Default"/> を使用する。
/// </summary>
internal sealed class GuardrailOptions
{
    // ----------------------------------------------------------------
    // デフォルト値
    // ----------------------------------------------------------------

    private static readonly string[] s_defaultTestAttributes =
    [
        "Test", "TestCase", "TestCaseSource",   // NUnit
        "Theory", "InlineData",                 // xUnit
        "Fact",                                 // xUnit
        "TestMethod", "DataTestMethod",         // MSTest
    ];

    private static readonly string[] s_defaultAssertionTypeNames =
    [
        "Assert", "ClassicAssert",              // NUnit
        "StringAssert", "CollectionAssert",     // NUnit
        "Warn",                                 // NUnit
    ];

    private static readonly string[] s_defaultAssertionMethodPatterns =
    [
        "Should",   // FluentAssertions / Shouldly
    ];

    // ----------------------------------------------------------------
    // プロパティ
    // ----------------------------------------------------------------

    /// <summary>テストメソッドを示す属性名（短縮形、Attribute サフィックスなし）。</summary>
    public IReadOnlyList<string> TestAttributes { get; }

    /// <summary>アサーションとみなす型名（Assert, ClassicAssert 等）。</summary>
    public IReadOnlyList<string> AssertionTypeNames { get; }

    /// <summary>アサーションとみなすメソッド名のパターン（"Should" 等、Contains 判定）。</summary>
    public IReadOnlyList<string> AssertionMethodPatterns { get; }

    /// <summary>ref/out パラメータを許可するメソッド名（完全一致）。</summary>
    public IReadOnlyList<string> AllowedRefOutMethods { get; }

    /// <summary>コンストラクタ内で許可するメソッド呼び出し名（完全一致）。</summary>
    public IReadOnlyList<string> AllowedCtorInvocations { get; }

    /// <summary>bool パラメータを許可するメソッド名（完全一致）。</summary>
    public IReadOnlyList<string> BoolParameterAllowedMethods { get; }

    /// <summary>ダウンキャストを除外するファイルパスのパターン（部分一致、大小無視）。</summary>
    public IReadOnlyList<string> DowncastExcludedFilePatterns { get; }

    /// <summary>ダウンキャストを除外する名前空間のパターン（部分一致、大小無視）。</summary>
    public IReadOnlyList<string> DowncastExcludedNamespacePatterns { get; }

    // ----------------------------------------------------------------
    // コンストラクタ
    // ----------------------------------------------------------------

    private GuardrailOptions(
        IReadOnlyList<string> testAttributes,
        IReadOnlyList<string> assertionTypeNames,
        IReadOnlyList<string> assertionMethodPatterns,
        IReadOnlyList<string> allowedRefOutMethods,
        IReadOnlyList<string> allowedCtorInvocations,
        IReadOnlyList<string> boolParameterAllowedMethods,
        IReadOnlyList<string> downcastExcludedFilePatterns,
        IReadOnlyList<string> downcastExcludedNamespacePatterns)
    {
        TestAttributes                    = testAttributes;
        AssertionTypeNames                = assertionTypeNames;
        AssertionMethodPatterns           = assertionMethodPatterns;
        AllowedRefOutMethods              = allowedRefOutMethods;
        AllowedCtorInvocations            = allowedCtorInvocations;
        BoolParameterAllowedMethods       = boolParameterAllowedMethods;
        DowncastExcludedFilePatterns      = downcastExcludedFilePatterns;
        DowncastExcludedNamespacePatterns = downcastExcludedNamespacePatterns;
    }

    /// <summary>設定ファイルなしのデフォルト。</summary>
    public static readonly GuardrailOptions Default = new(
        s_defaultTestAttributes,
        s_defaultAssertionTypeNames,
        s_defaultAssertionMethodPatterns,
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>());

    // ----------------------------------------------------------------
    // ロード
    // ----------------------------------------------------------------

    /// <summary>
    /// AdditionalFiles から <c>guardrail.json</c> を探してロードする。
    /// 見つからない / 解析失敗の場合は <see cref="Default"/> を返す。
    /// </summary>
    public static GuardrailOptions Load(ImmutableArray<AdditionalText> additionalFiles)
    {
        foreach (var file in additionalFiles)
        {
            var fileName = Path.GetFileName(file.Path);
            if (!string.Equals(fileName, "guardrail.json", StringComparison.OrdinalIgnoreCase))
                continue;

            var text = file.GetText()?.ToString();
            if (string.IsNullOrWhiteSpace(text))
                return Default;

            return Parse(text!);
        }
        return Default;
    }

    // ----------------------------------------------------------------
    // 解析
    // ----------------------------------------------------------------

    private static GuardrailOptions Parse(string json)
    {
        try
        {
            var root = MiniJson.ParseObject(json);
            if (root == null) return Default;

            return new GuardrailOptions(
                testAttributes:                    GetStringArray(root, "testMethodMustAssert",       "testAttributes")            ?? s_defaultTestAttributes,
                assertionTypeNames:                GetStringArray(root, "testMethodMustAssert",       "assertionTypeNames")        ?? s_defaultAssertionTypeNames,
                assertionMethodPatterns:           GetStringArray(root, "testMethodMustAssert",       "assertionMethodPatterns")   ?? s_defaultAssertionMethodPatterns,
                allowedRefOutMethods:              GetStringArray(root, "noRefOutParameter",          "allowedMethods")            ?? Array.Empty<string>(),
                allowedCtorInvocations:            GetStringArray(root, "constructorOnlyAssignments", "allowedInvocations")       ?? Array.Empty<string>(),
                boolParameterAllowedMethods:       GetStringArray(root, "boolParameter",             "allowedMethods")            ?? Array.Empty<string>(),
                downcastExcludedFilePatterns:      GetStringArray(root, "noDowncast",                "excludedFilePatterns")      ?? Array.Empty<string>(),
                downcastExcludedNamespacePatterns: GetStringArray(root, "noDowncast",                "excludedNamespacePatterns") ?? Array.Empty<string>());
        }
        catch
        {
            return Default;
        }
    }

    private static string[]? GetStringArray(
        Dictionary<string, object?> root,
        string sectionKey,
        string arrayKey)
    {
        if (!root.TryGetValue(sectionKey, out var sectionObj) ||
            sectionObj is not Dictionary<string, object?> section)
            return null;

        if (!section.TryGetValue(arrayKey, out var listObj) ||
            listObj is not List<object?> list)
            return null;

        return list.OfType<string>().ToArray();
    }
}
