using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using ProcessorTycoonRussian;
class Program
{
    static int count;
    static void Check(bool condition, string message) { count++; if (!condition) throw new Exception(message); }
    static void Main(string[] args)
    {
        var folder = args[0];
        var translator = new Translator(Path.Combine(folder, "translations"));
        Check(translator.ExactCount > 700, "dictionary coverage");
        Check(translator.RuleCount > 100, "template coverage");
        Check(translator.Translate("New Game") == "Новая игра", "menu");
        Check(translator.Translate("Fullscreen") != "Fullscreen", "display fullscreen translated");
        Check(translator.Translate("  Settings  ") == "  Настройки  ", "whitespace");
        Check(translator.Translate("John Smith") == "John Smith", "person name");
        Check(translator.Translate("Rozan 7 7700") == "Rozan 7 7700", "cpu name");
        Check(translator.Translate("CPU - My Custom CPU") == "CPU - My Custom CPU", "custom name");
        Check(translator.Translate(null) == null, "null");
        Check(translator.Translate("1920x1080") == "1920x1080", "resolution");
        foreach (var file in new[] {"ui.json", "code.json", "extra.json"})
        {
            var dict = JsonConvert.DeserializeObject<Dictionary<string,string>>(File.ReadAllText(Path.Combine(folder,"translations",file)));
            foreach (var item in dict)
            {
                var tags = new Func<string,string>(s => string.Join("|", Regex.Matches(s, @"<[^>]*>").Select(m => m.Value)));
                Check(tags(item.Key) == tags(item.Value), "markup: " + item.Key);
                var tokens = new Func<string,string>(s => string.Join("|", Regex.Matches(s, @"#[A-Za-z_]+(?!=)").Select(m => m.Value).OrderBy(s => s)));
                Check(tokens(item.Key) == tokens(item.Value), "placeholder: " + item.Key);
            }
        }
        var probes = Path.Combine(folder,"translations/context-probes.json");
        if (File.Exists(probes))
            foreach (var probe in JsonConvert.DeserializeObject<List<Dictionary<string,string>>>(File.ReadAllText(probes)))
            {
                Check(ContextTranslation.Translate(probe["source"], translator.Translate) == probe["expected"], "context probe: " + probe["source"]);
                if (probe["source"].Contains("\n") || probe["source"].Contains("Required") || probe["source"].Contains("Hiding"))
                    Check(translator.Translate(probe["source"]) == probe["expected"], "production context: " + probe["source"]);
            }
        Console.WriteLine($"PASS: {count} assertions; exact={translator.ExactCount}; templates={translator.RuleCount}");
    }
}
