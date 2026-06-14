// ====================================================================
// Guardrail.Sample.Violations — 故意の違反コード
//
// このファイルをビルドすると GRD001–GRD004 がビルドエラーになります。
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
