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
        Check(translator.Translate("Cost:") == "Цена:", "compact research price caption");
        Check(translator.Translate("Automate") == "Автоматизация", "compact automation caption");
        Check(translator.Translate("Upgrade lines") == "Модернизация", "compact upgrade caption");
        Check(translator.Translate("6µm yield:") == "Выход (6µm):", "compact yield caption preserves process node");
        Check(translator.Translate("Royalties:") == "Роялти:", "single-line finance royalties caption");
        Check(translator.Translate("Monthly Cost:") == "Расходы:", "compact monthly expense caption");
        Check(translator.Translate("Order by:") == "Сортировать", "compact sorting caption");
        Check(translator.Translate("High End") == "Премиум", "compact high-end filter");
        Check(translator.Translate("Mid Range") == "Средний", "compact mid-range filter");
        Check(translator.Translate("Low End") == "Бюджет", "compact low-end filter");
        Check(translator.Translate("Industries") == "Пром.", "compact industry filter");
        Check(translator.Translate("Net Worth:") == "Активы:", "compact company assets caption");
        Check(translator.Translate("Net worth") == "Активы", "compact company assets caption without colon");
        Check(translator.Translate("Sold Last Month:") == "За месяц:", "compact monthly sales caption");
        Check(translator.Translate("Total Profit:") == "Прибыль:", "compact total profit caption");
        Check(translator.Translate("New Game") == "Новая игра", "menu");
        Check(translator.Translate("Fullscreen") != "Fullscreen", "display fullscreen translated");
        Check(translator.Translate("  Settings  ") == "  Настройки  ", "whitespace");
        Check(translator.Translate("John Smith") == "John Smith", "person name");
        Check(translator.Translate("Rozan 7 7700") == "Rozan 7 7700", "cpu name");
        Check(translator.Translate("CPU - My Custom CPU") == "CPU - My Custom CPU", "custom name");
        Check(translator.Translate(null) == null, "null");
        Check(translator.Translate("1920x1080") == "1920x1080", "resolution");
        Check(translator.Translate("Time left: 64 days") == "Осталось: 64 дн.", "dynamic remaining time");
        Check(translator.Translate("Testing") == "Тестирование", "processor testing state");
        Check(translator.Translate("Designing") == "Проектирование", "processor design state");
        Check(translator.Translate("Developing") == "Разработка", "development state");
        Check(translator.Translate("Calculator") == "Калькулятор", "calculator option");
        Check(translator.Translate("Console") == "Консоль", "console option");
        Check(translator.Translate("Transistor Density: 16.00K/mm²") == "Плотность транзисторов: 16.00K/mm²", "transistor density");
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
