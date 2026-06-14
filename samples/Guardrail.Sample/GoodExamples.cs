// ====================================================================
// Guardrail.Sample — 準拠コード集
//
// このファイルの全クラスは GRD001–GRD004 に準拠しており、
// .editorconfig で GRD*=error に設定してもビルドが成功します。
// ====================================================================

namespace Guardrail.Sample;

// ----------------------------------------------------------------
// GRD001 準拠: コンストラクタは代入のみ
// GRD002 準拠: コンストラクタは 1 つ
// ----------------------------------------------------------------

/// <summary>
/// 注文エンティティ。コンストラクタは private 1 つ。
/// 生成パターンの出し分けは static ファクトリメソッドで表現する。
/// バリデーションもファクトリ内で行うため、コンストラクタは純粋な代入のみ。
/// </summary>
public sealed class Order
{
    private readonly string _id;
    private readonly decimal _amount;
    private readonly string _currency;

    // コンストラクタ: 代入のみ（GRD001 準拠）。インスタンスは 1 つ（GRD002 準拠）。
    private Order(string id, decimal amount, string currency)
    {
        _id       = id;
        _amount   = amount;
        _currency = currency;
    }

    // 生成パターン 1: 通常注文
    public static Order Create(string id, decimal amount, string currency = "JPY")
    {
        if (string.IsNullOrWhiteSpace(id))    throw new ArgumentException("id is required", nameof(id));
        if (amount < 0)                        throw new ArgumentOutOfRangeException(nameof(amount));
        if (string.IsNullOrWhiteSpace(currency)) throw new ArgumentException("currency is required", nameof(currency));
        return new Order(id, amount, currency);
    }

    // 生成パターン 2: 無料注文（ファクトリで意図を名前付き表現）
    public static Order CreateFree(string id) => new Order(id, 0m, "JPY");

    public string  Id       => _id;
    public decimal Amount   => _amount;
    public string  Currency => _currency;

    public override string ToString() => $"Order({_id}, {_amount} {_currency})";
}

// ----------------------------------------------------------------
// GRD003 準拠: ref / out 引数禁止
// ----------------------------------------------------------------

/// <summary>
/// 計算ユーティリティ。複数値の返却にはタプルを使う。
/// </summary>
public static class MathHelper
{
    /// <summary>out 引数を使わずタプルで商と余りを返す。</summary>
    public static (int Quotient, int Remainder) DivRem(int dividend, int divisor) =>
        (dividend / divisor, dividend % divisor);

    /// <summary>ref 引数を使わず新しい値を返す。</summary>
    public static int Increment(int value) => value + 1;
}

// ----------------------------------------------------------------
// GRD004 準拠: テストメソッドにはアサーション必須
//   ※ この例では NUnit を参照していないため、最小限のスタブを定義
//      実際のテストプロジェクトでは NUnit / xUnit / MSTest を使う。
// ----------------------------------------------------------------

// テスト属性スタブ（通常は NUnit.Framework.TestAttribute 等を使う）
[AttributeUsage(AttributeTargets.Method)]
public sealed class TestAttribute : Attribute { }

// アサーションスタブ（通常は NUnit.Framework.Assert 等を使う）
public static class Assert
{
    public static void That(bool condition, string? message = null)
    {
        if (!condition) throw new InvalidOperationException(message ?? "Assertion failed.");
    }
    public static void AreEqual<T>(T expected, T actual) =>
        That(EqualityComparer<T>.Default.Equals(expected, actual),
             $"Expected {expected} but was {actual}");
}

/// <summary>MathHelper のテストクラス（GRD004 準拠: アサーションあり）。</summary>
public class MathHelperTests
{
    [Test]
    public void DivRem_Returns_CorrectQuotientAndRemainder()
    {
        var (q, r) = MathHelper.DivRem(10, 3);

        // ← GRD004 準拠: アサーションが存在する
        Assert.AreEqual(3, q);
        Assert.AreEqual(1, r);
    }
}
