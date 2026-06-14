; Unshipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTracking.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
GRD001 | Guardrail | Warning | ConstructorOnlyAssignmentsAnalyzer, コンストラクタは代入のみ
GRD002 | Guardrail | Warning | SingleConstructorAnalyzer, インスタンスコンストラクタは1つまで
GRD003 | Guardrail | Warning | NoRefOutParameterAnalyzer, ref/out 引数禁止
GRD004 | Guardrail | Warning | TestMethodMustAssertAnalyzer, テストメソッドはAssert必須
