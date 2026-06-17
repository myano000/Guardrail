// ====================================================================
// Guardrail.Sample.Violations — 故意の違反コード
//
// このファイルをビルドすると GRD001–GRD009 がビルドエラーになります。
// .editorconfig で severity=error に設定しているため。
//
// 確認方法:
//   dotnet build samples/Guardrail.Sample.Violations/Guardrail.Sample.Violations.csproj
//
// Guardrail.sln の全体ビルド (dotnet build Guardrail.sln) は
// このプロジェクトをビルド対象から除外しているため緑になります。
// ====================================================================

namespace Guardrail.Sample.Violations;

// ----------------------------------------------------------------
// GRD001 違反: コンストラクタで仕事をしている
// ----------------------------------------------------------------

/// <summary>
/// コンストラクタ内でメソッド呼び出しを行っている。
/// 初期化ロジックはファクトリメソッドへ移すべき。
/// </summary>
public class ViolationGrd001
{
    private readonly string _value;

    public ViolationGrd001(string input)
    {
        _value = Transform(input); // ← 代入の右辺のメソッド呼び出しは OK
        Log("initialized");        // ← GRD001: 代入以外のメソッド呼び出し文
    }

    private static string Transform(string s) => s.Trim().ToUpperInvariant();
    private static void Log(string msg)        => Console.WriteLine(msg);
}

// ----------------------------------------------------------------
// GRD002 違反: コンストラクタが 2 つある
// ----------------------------------------------------------------

/// <summary>
/// インスタンスコンストラクタが 2 つ存在する。
/// デフォルト引数を持つコンストラクタをファクトリメソッドで代替すべき。
/// </summary>
public class ViolationGrd002
{
    private readonly int _value;

    public ViolationGrd002(int value)
    {
        _value = value;
    }

    // ← GRD002: 2 つ目のインスタンスコンストラクタ
    public ViolationGrd002() : this(0) { }

    public int Value => _value;
}

// ----------------------------------------------------------------
// GRD003 違反: ref / out 引数を使っている
// ----------------------------------------------------------------

/// <summary>
/// out / ref 引数を使って複数値を返している。
/// タプルや専用の結果型で代替すべき。
/// </summary>
public static class ViolationGrd003
{
    // ← GRD003: out 引数
    public static bool TryParse(string input, out int result)
        => int.TryParse(input, out result);

    // ← GRD003: ref 引数
    public static void Increment(ref int counter)
        => counter++;
}

// ----------------------------------------------------------------
// GRD004 違反: テストメソッドにアサーションがない
// ----------------------------------------------------------------

/// <summary>
/// テスト属性を宣言するための最小スタブ（NUnit なしで動作確認するため）。
/// 実際のテストでは NUnit.Framework の TestAttribute を使う。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class TestAttribute : Attribute { }

/// <summary>
/// アサーションなしのテストメソッドを含むクラス。
/// 常に緑になるため、バグを見逃す危険なパターン。
/// </summary>
public class ViolationGrd004
{
    // ← GRD004: [Test] が付いているがアサーションが 1 つもない
    [Test]
    public void SomeBusinessRule_IsAlwaysGreen()
    {
        var order = new ViolationGrd002(100);
        var _ = order.Value; // 何かを計算しているが assert していない
    }
}

// ----------------------------------------------------------------
// GRD005 違反: bool パラメータ（flag argument）を使っている
// ----------------------------------------------------------------

/// <summary>
/// bool パラメータによって 1 つのメソッドが 2 つの振る舞いを持つ。
/// 呼び出し側では Send(msg, true) と書くしかなく、意図が不明瞭。
/// </summary>
public static class ViolationGrd005
{
    // ← GRD005: bool パラメータ isUrgent
    public static void Send(string message, bool isUrgent)
        => Console.WriteLine(isUrgent ? $"[URGENT] {message}" : message);

    // ← GRD005: bool? パラメータ
    public static void Toggle(string feature, bool? enable)
        => Console.WriteLine($"{feature}: {enable}");
}

// ----------------------------------------------------------------
// GRD006 違反: ダウンキャストを使っている（UI層ではない）
// ----------------------------------------------------------------

public abstract class Shape { }
public class Circle : Shape { public double Radius { get; set; } }
public class Rectangle : Shape { public double Width { get; set; } }

/// <summary>
/// 型でスイッチする設計はオープン・クローズド原則に反する。
/// 新しい Shape を追加するたびにこのクラスの修正が必要になる。
/// </summary>
public static class ViolationGrd006
{
    public static double GetArea(Shape shape)
    {
        // ← GRD006: ダウンキャスト（CastExpression）
        if (shape is Circle)
        {
            var circle = (Circle)shape;
            return Math.PI * circle.Radius * circle.Radius;
        }

        // ← GRD006: ダウンキャスト（AsExpression）
        var rect = shape as Rectangle;
        if (rect != null)
            return rect.Width * rect.Width;

        return 0;
    }
}

// ----------------------------------------------------------------
// GRD007 違反: 無意味な null チェック
// ----------------------------------------------------------------

/// <summary>
/// new で生成したオブジェクトは常に非 null。
/// それに対する null チェックは常に false/true になり意味がない。
/// </summary>
public static class ViolationGrd007
{
    // ← GRD007: new 直後に == null（常に false）
    public static void PatternOne_DirectCheck()
    {
        if (new ViolationGrd002(0) == null) // 絶対に null にならない
            Console.WriteLine("never reached");
    }

    // ← GRD007: new で初期化したローカルに != null チェック（常に true）
    public static void PatternTwo_LocalVariable()
    {
        var order = new ViolationGrd002(100); // new で初期化
        if (order != null) // ← order は絶対に null にならない
        {
            Console.WriteLine(order.Value);
        }
    }

    // ← GRD007: is null パターン
    public static void PatternTwo_IsNull()
    {
        var order = new ViolationGrd002(50);
        if (order is null) // ← 絶対に true にならない
            throw new InvalidOperationException("unreachable");
    }
}

// ----------------------------------------------------------------
// GRD009 違反: メソッドが長すぎる（責務を見直し分割を検討）
// ----------------------------------------------------------------

/// <summary>
/// 注文処理・在庫確認・配送手配・通知送信・ログ記録を 1 つのメソッドで行っている。
/// AI は指示しなければ同じメソッドに処理を追加し続けるため、
/// しきい値を超えたタイミングで分割を促す必要がある。
/// </summary>
public static class ViolationGrd009
{
    // ← GRD009: 31 ステートメント以上のメソッド（デフォルトしきい値 30 を超える）
    public static void ProcessOrder(string orderId)
    {
        // --- 注文検証フェーズ ---
        if (string.IsNullOrWhiteSpace(orderId)) throw new ArgumentException("orderId is required");
        var normalized = orderId.Trim().ToUpperInvariant();
        Console.WriteLine($"[検証] orderId={normalized}");

        // --- 在庫確認フェーズ ---
        var stock = 10; // 実際はリポジトリ呼び出し
        if (stock <= 0) throw new InvalidOperationException("在庫不足");
        Console.WriteLine($"[在庫] 残数={stock}");
        var reserved = stock - 1;
        Console.WriteLine($"[在庫] 引き当て後={reserved}");

        // --- 価格計算フェーズ ---
        var unitPrice = 1000m;
        var quantity  = 1;
        var subtotal  = unitPrice * quantity;
        var tax       = subtotal * 0.1m;
        var total     = subtotal + tax;
        Console.WriteLine($"[価格] 小計={subtotal} 税={tax} 合計={total}");

        // --- 配送手配フェーズ ---
        var shippingCode = $"SHIP-{normalized}";
        Console.WriteLine($"[配送] コード={shippingCode}");
        var estimatedDays = 3;
        Console.WriteLine($"[配送] 推定日数={estimatedDays}");

        // --- 通知送信フェーズ ---
        var message = $"注文 {normalized} が確定しました。合計: {total:C}";
        Console.WriteLine($"[通知] {message}");

        // --- ログ記録フェーズ ---
        Console.WriteLine($"[ログ] {DateTime.UtcNow:O} ORDER_CONFIRMED id={normalized} total={total}");
    }
}
