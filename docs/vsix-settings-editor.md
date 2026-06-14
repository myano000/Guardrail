# VSIX 設定エディタ — 設計メモ（将来実装）

> **ステータス**: 未実装。本ドキュメントは設計検討用のメモです。

## 目的

`guardrail.json` を GUI で編集できる Visual Studio 拡張機能を提供し、
"設定を触る" 敷居を下げること。

## VSIX と NuGet アナライザの役割分担

| 機能 | 担当 |
|------|------|
| ビルドエラー化・CI でのポリシー強制 | NuGet アナライザ（現在実装済み） |
| IDE 上のリアルタイム赤波線 | NuGet アナライザ（VS が自動的に読み込む） |
| `guardrail.json` の GUI 編集 | **VSIX 拡張（このドキュメントの対象）** |

VSIX はあくまで「設定ファイルを編集するための UI」であり、
ビルドエラー化の仕組みには関与しない。

## 実装方針

### プロジェクト種別
`Microsoft.VSSDK.BuildTools` を使った VSIX プロジェクト。
VS 2022 以降を対象とする（`source.extension.vsixmanifest` で指定）。

### 提供する UI
- **Tools > Guardrail Settings** に WPF ツールウィンドウを追加
- `guardrail.json` の各セクションを対応するコントロールで表示
  - テスト属性リスト → `ListBox` + 追加/削除ボタン
  - アサーション型名リスト → 同上
  - severity 設定 → `.editorconfig` への書き込みも検討
- ファイルの保存場所はソリューションルートまたは選択したプロジェクトルート

### 技術スタック
- MEF コンポーネントとして `IVsWindowPane` 実装
- `Microsoft.VisualStudio.Shell.15.0` (VSSDK)
- WPF + MVVM (Toolkit.Mvvm 等)
- `System.Text.Json` (VSIX プロセス内なのでアセンブリ衝突なし)

### 配布
VSIX を Visual Studio Marketplace に公開し、
「拡張機能の管理」からインストール可能にする。

## 注意事項

- VSIX 経由のアナライザはコンパイラビルドに関与しない。
  CI でビルドエラーにするには、必ず NuGet アナライザの参照が必要。
- VSIX の自動更新は VS の拡張機能更新機能に依存する。
  バージョン管理は `source.extension.vsixmanifest` の `Version` で行う。
- `guardrail.json` のスキーマを変更する際は、
  アナライザ側の `GuardrailOptions` と VSIX 設定 UI を同期させること。
