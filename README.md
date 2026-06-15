# Guardrail

**AIエージェント（バイブコーディング）の品質を、コンパイル時に自動で守るRoslynアナライザ集。**

---

## 背景：なぜGuardrailが必要か

CLAUDE.mdにコーディングポリシーを書いても、AIエージェントはたいてい忘れます。  
その都度「bool引数はやめて」「ダウンキャストしないで」と指摘するのは、レビュアーの時間とストレスを消耗します。

Guardrailはその問題をコンパイラエラー／警告に変換します。  
エージェントはエラーメッセージに書かれた**違反内容と修正方法**を読んで自己修正するため、  
人間のレビューなしにコーディングポリシーが守られます。

```
Warning GRD005: パラメータ 'isUrgent' が bool です。
  列挙型・メソッドの分割・オプション型で意図を表現してください。
```

エージェントがこのエラーを受け取れば、あとはコーヒーを飲んでいるだけで済みます。

---

## 現在実装済みのルール

| ID     | ルール                         | 概要                                                                 |
|--------|-------------------------------|----------------------------------------------------------------------|
| GRD001 | コンストラクタは代入のみ         | コンストラクタ内にメソッド呼び出し・制御フローを書くと警告              |
| GRD002 | インスタンスコンストラクタは1つまで | 複数コンストラクタはオーバーロード地獄の元。ファクトリメソッドで代替   |
| GRD003 | ref / out 引数禁止              | 戻り値タプルや出力専用クラスで代替                                    |
| GRD004 | テストメソッドはアサーション必須  | Assert/Should系なしのテストは「成功し続けるゾンビ」                   |
| GRD005 | bool パラメータ禁止              | `Process(true)` は意味不明。列挙型かメソッド分割で表現               |
| GRD006 | ダウンキャスト禁止               | `(Dog)animal` は実行時例外の元。ポリモーフィズムで代替               |
| GRD007 | 無意味な null チェック禁止       | `new Foo() == null` は常にfalse。エージェントが防御的に書きがちな癖   |

---

## クイックスタート

### 1. アナライザをプロジェクトに追加

```xml
<!-- .csproj -->
<ItemGroup>
  <ProjectReference Include="path/to/Guardrail.Analyzers.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

NuGetパッケージ版（準備中）:
```
dotnet add package Guardrail.Analyzers
```

### 2. 設定ファイル（任意）

プロジェクトルートに `guardrail.json` を置き、AdditionalFilesに追加すると挙動をカスタマイズできます。

```xml
<!-- .csproj -->
<ItemGroup>
  <AdditionalFiles Include="guardrail.json" />
</ItemGroup>
```

```json
{
  "boolParameter": {
    "allowedMethods": ["TryParse", "IsEnabled"]
  },
  "noDowncast": {
    "excludedFilePatterns": ["/Ui/", "/Views/"],
    "excludedNamespacePatterns": ["MyApp.Ui"]
  },
  "constructorOnlyAssignments": {
    "allowedInvocations": ["InitializeComponent"]
  },
  "noRefOutParameter": {
    "allowedMethods": ["TryGetValue", "TryParse"]
  }
}
```

### 3. ビルドして確認

```bash
dotnet build
```

ポリシー違反はビルド時に警告として出力されます。`TreatWarningsAsErrors` を設定すればエラーに昇格できます。

```xml
<PropertyGroup>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <!-- または特定ルールだけ -->
  <WarningsAsErrors>GRD001;GRD002;GRD003;GRD004;GRD005;GRD006;GRD007</WarningsAsErrors>
</PropertyGroup>
```

---

## ルール詳細

### GRD001 — コンストラクタは代入のみ

コンストラクタ内の処理を代入に限定することで、オブジェクトのライフサイクルを透明にし、テスト容易性を高めます。

```csharp
// 違反
public OrderService(ILogger logger)
{
    _logger = logger;
    _logger.Log("initialized");  // メソッド呼び出し → GRD001
}

// 準拠
public OrderService(ILogger logger)
{
    _logger = logger;
}

public static OrderService Create(ILogger logger)
{
    var svc = new OrderService(logger);
    svc.Initialize();
    return svc;
}
```

### GRD002 — インスタンスコンストラクタは1つまで

複数コンストラクタは呼び出し側が「どれを使うべきか」を把握しなければならず、依存関係が不透明になります。

```csharp
// 違反
public class Report
{
    public Report() { }
    public Report(string title) { _title = title; }  // 2つ目 → GRD002
}

// 準拠
public class Report
{
    private Report(string title) { _title = title; }

    public static Report Untitled()        => new Report("");
    public static Report WithTitle(string t) => new Report(t);
}
```

### GRD003 — ref / out 引数禁止

`ref`/`out` は副作用を見えにくくします。戻り値タプルで複数値を返してください。

```csharp
// 違反
bool TryParse(string s, out int result) { ... }  // GRD003

// 準拠
(bool success, int result) TryParse(string s) { ... }
```

### GRD004 — テストメソッドはアサーション必須

アサーションのないテストはビルドが通り続けるだけで何も検証しません。

```csharp
// 違反
[Test]
public void CalculateTotal_ReturnsValue()
{
    var result = _calc.Total();  // assertなし → GRD004
}

// 準拠
[Test]
public void CalculateTotal_ReturnsExpectedSum()
{
    var result = _calc.Total();
    Assert.That(result, Is.EqualTo(100));
}
```

### GRD005 — bool パラメータ禁止（flag argument）

`Send(message, true)` の `true` が何を意味するか、呼び出し側だけを見ても分かりません。

```csharp
// 違反
void Send(string message, bool isUrgent) { ... }  // GRD005

// 準拠（列挙型で意図を表現）
void Send(string message, Priority priority) { ... }

// 準拠（メソッドを分割）
void Send(string message) { ... }
void SendUrgent(string message) { ... }
```

### GRD006 — ダウンキャスト禁止

ダウンキャストは型システムの保証を破り、`InvalidCastException` の原因になります。

```csharp
// 違反
Animal a = GetAnimal();
var dog = (Dog)a;    // GRD006
var cat = a as Cat;  // GRD006

// 準拠
animal.Speak();           // ポリモーフィズム
animal.Accept(visitor);   // Visitor パターン
```

UIフレームワーク等でどうしても必要な場合は `guardrail.json` の `excludedFilePatterns` で除外できます。

### GRD007 — 無意味な null チェック禁止

`new` は常に非nullを返します。AIエージェントが防御的に書きがちなパターンを検出します。

```csharp
// 違反
var order = new Order();
if (order == null) { ... }  // GRD007: order は null になれない

// 違反
if (new Foo() != null) { ... }  // GRD007: 常に true

// 準拠
Order? order = GetOrderOrNull();
if (order == null) { ... }  // 外部から受け取った値 → 正当なチェック
```

---

## 将来の展開

現在はC# (Roslyn) のみですが、同じ考え方はほかの言語にも適用できます。

- **未使用プロパティ・変数の検知** — エージェントが「とりあえず追加」したコードの除去
- **JavaScript / TypeScript** — ESLintカスタムルールで同様のポリシー適用
- **Python** — flake8 / pylint プラグイン

バイブコーディングのガードレールを言語・エコシステムを問わず整備していく予定です。

---

## 開発

```bash
# ビルド
dotnet build Guardrail.sln

# テスト
dotnet test

# サンプル（準拠コード）
dotnet build samples/Guardrail.Sample/Guardrail.Sample.csproj

# サンプル（違反コード — 警告が出ることを確認）
dotnet build samples/Guardrail.Sample.Violations/Guardrail.Sample.Violations.csproj
```

## ライセンス

MIT
