using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Guardrail.Analyzers.Configuration;

/// <summary>
/// 依存ゼロの最小 JSON パーサ。
/// guardrail.json のような平坦なスキーマを対象とする。
/// System.Text.Json を使わない理由: アナライザホスト上でアセンブリ衝突が発生しやすいため。
/// </summary>
internal sealed class MiniJson
{
    private readonly string _src;
    private int _pos;

    private MiniJson(string src) { _src = src; _pos = 0; }

    // ----------------------------------------------------------------
    // 公開 API
    // ----------------------------------------------------------------

    /// <summary>JSON 文字列をパースしてトップレベルのオブジェクトを返す。失敗時は null。</summary>
    public static Dictionary<string, object?>? ParseObject(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var parser = new MiniJson(json);
            parser.SkipWs();
            return parser.ReadObject();
        }
        catch
        {
            return null;
        }
    }

    // ----------------------------------------------------------------
    // 内部実装
    // ----------------------------------------------------------------

    private char Current => _pos < _src.Length ? _src[_pos] : '\0';

    private void SkipWs()
    {
        while (_pos < _src.Length && char.IsWhiteSpace(_src[_pos]))
            _pos++;
    }

    private void Expect(char c)
    {
        if (Current != c)
            throw new FormatException($"Expected '{c}' but found '{Current}' at {_pos}");
        _pos++;
    }

    private Dictionary<string, object?> ReadObject()
    {
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        Expect('{');
        SkipWs();
        if (Current == '}') { _pos++; return dict; }

        while (true)
        {
            SkipWs();
            var key = ReadString();
            SkipWs();
            Expect(':');
            SkipWs();
            var value = ReadValue();
            dict[key] = value;
            SkipWs();
            if (Current == ',') { _pos++; continue; }
            break;
        }
        SkipWs();
        Expect('}');
        return dict;
    }

    private List<object?> ReadArray()
    {
        var list = new List<object?>();
        Expect('[');
        SkipWs();
        if (Current == ']') { _pos++; return list; }

        while (true)
        {
            SkipWs();
            list.Add(ReadValue());
            SkipWs();
            if (Current == ',') { _pos++; continue; }
            break;
        }
        SkipWs();
        Expect(']');
        return list;
    }

    private object? ReadValue()
    {
        SkipWs();
        switch (Current)
        {
            case '{': return ReadObject();
            case '[': return ReadArray();
            case '"': return ReadString();
            case 't': return ReadLiteral("true",  true);
            case 'f': return ReadLiteral("false", false);
            case 'n': return ReadLiteral("null",  null);
            default:
                if (char.IsDigit(Current) || Current == '-')
                    return ReadNumber();
                throw new FormatException($"Unexpected '{Current}' at {_pos}");
        }
    }

    private string ReadString()
    {
        Expect('"');
        var sb = new StringBuilder();
        while (_pos < _src.Length && _src[_pos] != '"')
        {
            if (_src[_pos] == '\\')
            {
                _pos++;
                if (_pos >= _src.Length) break;
                var escaped = _src[_pos] switch
                {
                    '"'  => '"',
                    '\\' => '\\',
                    '/'  => '/',
                    'n'  => '\n',
                    'r'  => '\r',
                    't'  => '\t',
                    'b'  => '\b',
                    'f'  => '\f',
                    _    => _src[_pos],
                };
                sb.Append(escaped);
            }
            else
            {
                sb.Append(_src[_pos]);
            }
            _pos++;
        }
        Expect('"');
        return sb.ToString();
    }

    private object? ReadLiteral(string literal, object? value)
    {
        if (_pos + literal.Length <= _src.Length &&
            _src.Substring(_pos, literal.Length) == literal)
        {
            _pos += literal.Length;
            return value;
        }
        throw new FormatException($"Expected '{literal}' at {_pos}");
    }

    private double ReadNumber()
    {
        var start = _pos;
        if (Current == '-') _pos++;
        while (_pos < _src.Length && (char.IsDigit(_src[_pos]) || _src[_pos] == '.'))
            _pos++;
        if (_pos < _src.Length && (_src[_pos] == 'e' || _src[_pos] == 'E'))
        {
            _pos++;
            if (_pos < _src.Length && (_src[_pos] == '+' || _src[_pos] == '-')) _pos++;
            while (_pos < _src.Length && char.IsDigit(_src[_pos])) _pos++;
        }
        return double.Parse(_src.Substring(start, _pos - start), CultureInfo.InvariantCulture);
    }
}
