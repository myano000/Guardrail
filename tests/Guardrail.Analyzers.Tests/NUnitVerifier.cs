using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Guardrail.Analyzers.Tests;

/// <summary>
/// <see cref="IVerifier"/> の NUnit 実装。
/// <para>
/// <c>Microsoft.CodeAnalysis.CSharp.Analyzer.Testing.NUnit</c> パッケージは
/// Roslyn 1.0.x 向けにビルドされており Roslyn 4.x とバージョン衝突するため、
/// このプロジェクトで独自実装する。
/// </para>
/// </summary>
internal sealed class NUnitVerifier : IVerifier
{
    private readonly ImmutableStack<string> _context;

    public NUnitVerifier()
        : this(ImmutableStack<string>.Empty) { }

    private NUnitVerifier(ImmutableStack<string> context) =>
        _context = context;

    // ----------------------------------------------------------------
    // IVerifier 実装
    // ----------------------------------------------------------------

    public void Empty<T>(string collection, IEnumerable<T> actual) =>
        Assert.That(actual, Is.Empty, MsgOf($"'{collection}' should be empty"));

    public void Equal<T>(T expected, T actual, string? message = null) =>
        Assert.That(actual, Is.EqualTo(expected), MsgOf(message));

    public void True(bool actual, string? message = null) =>
        Assert.That(actual, Is.True, MsgOf(message));

    public void False(bool actual, string? message = null) =>
        Assert.That(actual, Is.False, MsgOf(message));

    public void Fail(string? message = null) =>
        Assert.Fail(MsgOf(message));

    public void LanguageIsSupported(string language) =>
        Assert.That(language, Is.EqualTo(LanguageNames.CSharp),
            MsgOf($"Language '{language}' is not supported."));

    public void NotEmpty<T>(string collection, IEnumerable<T> actual) =>
        Assert.That(actual, Is.Not.Empty, MsgOf($"'{collection}' should not be empty"));

    public void SequenceEqual<T>(
        IEnumerable<T> expected,
        IEnumerable<T> actual,
        IEqualityComparer<T>? equalityComparer = null,
        string? reason = null)
    {
        var exp = expected.ToArray();
        var act = actual.ToArray();
        var cmp = equalityComparer ?? EqualityComparer<T>.Default;

        if (exp.SequenceEqual(act, cmp)) return;

        var expStr = string.Join(", ", exp.Select(x => x?.ToString()));
        var actStr = string.Join(", ", act.Select(x => x?.ToString()));
        Assert.Fail(MsgOf(reason ?? $"Expected [{expStr}] but got [{actStr}]"));
    }

    public IVerifier PushContext(string context) =>
        new NUnitVerifier(_context.Push(context));

    // ----------------------------------------------------------------
    // ヘルパー
    // ----------------------------------------------------------------

    private string MsgOf(string? message)
    {
        var ctx = string.Join(" → ", _context.Reverse());
        return string.IsNullOrEmpty(ctx) ? (message ?? "") : $"[{ctx}] {message}";
    }
}
