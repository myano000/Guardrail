// ====================================================================
// Guardrail.Sample.Violations — 故意の違反コード
//
// このファイルをビルドすると GRD001–GRD007 がビルドエラーになります。
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
