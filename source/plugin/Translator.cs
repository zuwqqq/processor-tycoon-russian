using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace ProcessorTycoonRussian
{
    public sealed class Rule
    {
        public string pattern { get; set; }
        public string replacement { get; set; }
    }

    public sealed class Translator
    {
        readonly Dictionary<string, string> exact = new Dictionary<string, string>(StringComparer.Ordinal);
        readonly List<KeyValuePair<Regex, string>> rules = new List<KeyValuePair<Regex, string>>();
        readonly Dictionary<string, string> cache = new Dictionary<string, string>(StringComparer.Ordinal);
        public int ExactCount => exact.Count;
        public int RuleCount => rules.Count;
        public long Hits { get; private set; }
        public Action<string> Missing;

        public Translator(string folder)
        {
            foreach (var name in new[] { "ui.json", "code.json", "extra.json" })
            {
                var path = Path.Combine(folder, name);
                if (!File.Exists(path)) continue;
                foreach (var entry in JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path)))
                    exact[entry.Key] = entry.Value;
            }
            var templatePath = Path.Combine(folder, "templates.json");
            if (File.Exists(templatePath))
                foreach (var rule in JsonConvert.DeserializeObject<Rule[]>(File.ReadAllText(templatePath)))
                    rules.Add(new KeyValuePair<Regex, string>(new Regex(rule.pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(25)), rule.replacement));
        }

        public string Translate(string source)
        {
            if (string.IsNullOrEmpty(source)) return source;
            if (cache.TryGetValue(source, out var cached)) return cached;
            var result = ContextTranslation.Translate(TranslateUncached(source), TranslateUncached);
            if (cache.Count >= 8192) cache.Clear();
            cache[source] = result;
            if (!string.Equals(result, source, StringComparison.Ordinal)) Hits++;
            else if (Regex.IsMatch(source, "[A-Za-z]{3}")) Missing?.Invoke(source);
            return result;
        }

        string TranslateUncached(string source)
        {
            if (exact.TryGetValue(source, out var result)) return result;
            var trimmed = source.Trim();
            if (exact.TryGetValue(trimmed, out result))
                return source.Substring(0, source.IndexOf(trimmed, StringComparison.Ordinal)) + result + source.Substring(source.IndexOf(trimmed, StringComparison.Ordinal) + trimmed.Length);
            foreach (var rule in rules)
            {
                try
                {
                    if (rule.Key.IsMatch(source)) return rule.Key.Replace(source, rule.Value);
                }
                catch (RegexMatchTimeoutException) { }
            }
            return source;
        }
    }
}
