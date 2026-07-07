using System.Globalization;
using System.Text;
using Faolline.GraphCore;

namespace Faolline.GraphDialogue
{
    /// <summary>
    /// Substitutes <c>{key}</c> tokens in already-resolved dialogue text with values from the runtime
    /// <see cref="BaseContext"/> blackboard (e.g. <c>"Hello {playerName}, score {score}"</c>). Numbers use
    /// invariant culture. Unknown tokens are left untouched (so they stay visible to authors). Use
    /// <c>{{</c> / <c>}}</c> to emit literal braces. Runs after localization, so it is provider-agnostic.
    /// </summary>
    public static class DialogueTextInterpolator
    {
        public static string Interpolate(string text, BaseContext context)
        {
            if (string.IsNullOrEmpty(text) || context == null || text.IndexOf('{') < 0)
                return text;

            var values = context.GetAllVariables();
            var sb = new StringBuilder(text.Length);

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '{')
                {
                    if (i + 1 < text.Length && text[i + 1] == '{') { sb.Append('{'); i++; continue; } // {{ → {

                    int end = text.IndexOf('}', i + 1);
                    if (end < 0) { sb.Append(c); continue; } // unterminated → literal

                    var key = text.Substring(i + 1, end - i - 1).Trim();
                    if (values != null && values.TryGetValue(key, out var value))
                        sb.Append(Format(value));
                    else
                        sb.Append(text, i, end - i + 1); // unknown token: keep literal "{key}"

                    i = end;
                    continue;
                }

                if (c == '}' && i + 1 < text.Length && text[i + 1] == '}') { sb.Append('}'); i++; continue; } // }} → }

                sb.Append(c);
            }

            return sb.ToString();
        }

        private static string Format(object value)
        {
            switch (value)
            {
                case null: return string.Empty;
                case float f: return f.ToString(CultureInfo.InvariantCulture);
                case double d: return d.ToString(CultureInfo.InvariantCulture);
                case int n: return n.ToString(CultureInfo.InvariantCulture);
                default: return value.ToString();
            }
        }
    }
}
