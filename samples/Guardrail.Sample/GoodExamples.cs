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

// ----------------------------------------------------------------
// GRD005 準拠: bool パラメータ禁止
// ----------------------------------------------------------------

/// <summary>
/// メール送信サービス。bool フラグの代わりに列挙型で意図を表現する。
/// </summary>
public enum MailPriority { Normal, Urgent }

public static class MailService
{
    // bool isUrgent の代わりに MailPriority 列挙型を使う（GRD005 準拠）
    public static void Send(string message, MailPriority priority)
    {
        // priority に応じた処理...
        _ = message;
        _ = priority;
    }

    // あるいはメソッドを分割する（GRD005 準拠）
    public static void SendNormal(string message)  { _ = message; }
    public static void SendUrgent(string message)  { _ = message; }
}

// ----------------------------------------------------------------
// GRD006 準拠: ダウンキャスト禁止（ポリモーフィズムで代替）
// ----------------------------------------------------------------

/// <summary>
/// 動物の基底クラス。振る舞いはポリモーフィズムで表現し、
/// 呼び出し側がダウンキャストしなくて済む設計。
/// </summary>
public abstract class Animal
{
    public abstract string Speak();
}

public class GoodDog : Animal
{
    public override string Speak() => "Woof";
}

public class GoodCat : Animal
{
    public override string Speak() => "Meow";
}

public static class AnimalHelper
{
    // ダウンキャスト不要: 仮想メソッドに委譲するだけ（GRD006 準拠）
    public static void MakeSpeak(Animal a) => _ = a.Speak();
}

// ----------------------------------------------------------------
// GRD007 準拠: 無意味な null チェック禁止
// ----------------------------------------------------------------

public static class NullCheckExamples
{
    // ← GRD007 準拠: 外部から受け取った値の null チェックは意味がある
    public static void ProcessName(string? name)
    {
        if (name == null) throw new ArgumentNullException(nameof(name));
        _ = name.Length;
    }

    // ← GRD007 準拠: ファクトリがnullを返す可能性があるローカルのチェックも意味がある
    public static void ProcessOrder(string id)
    {
        Order? order = id.Length > 0 ? Order.Create(id, 100m) : null;
        if (order == null) return;   // null になり得る → チェックは意味がある
        _ = order.Amount;
    }
}
