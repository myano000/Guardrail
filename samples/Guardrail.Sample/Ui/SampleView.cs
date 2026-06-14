// ====================================================================
// Guardrail.Sample — UI層のダウンキャスト除外デモ
//
// このファイルは意図的にダウンキャスト（GRD006）を含みます。
// しかし guardrail.json の noDowncast.excludedFilePatterns に "/Ui/" を
// 設定しているため、GRD006 は報告されません。
//
// → サンプルプロジェクト全体は .editorconfig で GRD006=error でも GREEN にビルドされます。
//
// なぜ UI 層だけ除外するのか:
//   MVC/MVVM フレームワーク（WPF, WinForms, ASP.NET 等）では
//   EventArgs のダウンキャストやフレームワーク API の利用がやむを得ない。
//   コアドメイン層は守りつつ、UI層だけ例外扱いにする。
// ====================================================================

namespace Guardrail.Sample.Ui;

// ダウンキャストのデモ用型
public abstract class BaseEventArgs
{
    public string Source { get; set; } = string.Empty;
}

public class ButtonClickEventArgs : BaseEventArgs
{
    public string ButtonName { get; set; } = string.Empty;
}

/// <summary>
/// UI ビュークラス。フレームワークが BaseEventArgs を渡してくる場合の
/// ダウンキャストをデモする。
///
/// このコードは GRD006 の対象外（/Ui/ パスが excludedFilePatterns に含まれる）。
/// </summary>
public class SampleView
{
    /// <summary>
    /// フレームワークから BaseEventArgs を受け取り、
    /// 具体型にキャストする（UI 層ではやむを得ないパターン）。
    /// </summary>
    public void OnEvent(BaseEventArgs args)
    {
        // UI 層では as を使って安全にダウンキャストすることがある。
        // guardrail.json の excludedFilePatterns: ["/Ui/"] により GRD006 は除外される。
        var clickArgs = args as ButtonClickEventArgs;
        if (clickArgs != null)
        {
            HandleButtonClick(clickArgs.ButtonName);
        }
    }

    private static void HandleButtonClick(string buttonName)
    {
        // ボタンのクリック処理...
        _ = buttonName;
    }
}
