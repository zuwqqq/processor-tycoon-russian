using System;
using System.Text.RegularExpressions;

namespace ProcessorTycoonRussian
{
    public static class ContextTranslation
    {
        const string Number = @"[+\-]?[0-9][0-9.,]*(?:[KMBT])?";
        const string Color = @"<color=#[A-Fa-f0-9]{6,8}>";
        const string Month = @"(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)";

        static Regex Pattern(string pattern)
        {
            return new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
        }

        static readonly Regex Report = Pattern(@"\A(?:We grossed [^\r\n]+ at the year of \d{4},|Наша выручка за \d{4} год составила )");
        static readonly Regex Taxes = Pattern(@"We payed (" + Color + @"\$" + Number + @"</color>) in taxes\.");
        static readonly Regex ReportLabel = Pattern(@"(?m)^(Revenue Growth|Profit Margin|Profit): (?=" + Color + @"[+\-]?(?:\$)?[0-9])");
        static readonly Regex Contract = Pattern(@"(?m)^<style=Bold>([^\r\n]*?) \((Client|Provider)\)</style>(\r?\n)- (Foundry Services|Услуги контрактного производства), until (" + Month + @") (\d{4})(?=\r?$)");
        static readonly Regex HiddenNodes = Pattern(@"(?m)^(" + Color + @")Hiding</color> (\d+) (" + Color + @")fully optimized nodes?</color>(?=\r?$)");
        static readonly Regex Node = Pattern(@"(?m)^([0-9]+(?:\.[0-9]+)?(?:nm|µm|μm): [0-9.,]+% \(" + Color + @"\+[0-9.,]+)/m(</color>\))(?=\r?$)");
        static readonly Regex DescriptionMarker = Pattern(@"(?m)^(?:Yield Rate|Wafer Cost|Transistor Density|Fees|Bottleneck|Cache Cost|Die Size|Cache Capacity|Safe Temperature|Multiple Cores|Cost|Time|Выход годных|Стоимость пластины|Плотность транзисторов|Плата|Стоимость|Время): ");
        static readonly Regex DescriptionLine = Pattern(@"\A(?:Yield Rate|Wafer Cost|Transistor Density|Fees|Bottleneck|Cache Cost|Die Size|Cache Capacity|Safe Temperature|Multiple Cores|Cost|Time): [^\r\n]*\z");
        static readonly Regex CapacityLine = Pattern(@"\A[0-9.,]+[KMBT]? lines?, [0-9.,]+ mm\z");
        static readonly Regex RequiredCache = Pattern(@"\A(<color=#[A-Fa-f0-9]{6,8}>[^<>\r\n]+) \(([^<>\r\n]+) Required\)</color>\z");
        static readonly Regex WarningCost = Pattern(@"(?m)^Remaining Cost: (?=<style=(?:Normal|Medium)><color=#[A-Fa-f0-9]{6,8}>\$[0-9])");

        public static string Translate(string source, Func<string, string> translate)
        {
            if (string.IsNullOrEmpty(source) || translate == null) return source;
            try
            {
                string result = source;
                if (Report.IsMatch(result))
                {
                    result = Taxes.Replace(result, "Мы уплатили $1 налогов.");
                    result = result.Replace("We didn't pay any taxes.", "Мы не платили налоги.");
                    result = ReportLabel.Replace(result, m => m.Groups[1].Value == "Revenue Growth" ? "Рост выручки: " : m.Groups[1].Value == "Profit Margin" ? "Рентабельность: " : "Прибыль: ");
                }
                result = Contract.Replace(result, m => "<style=Bold>" + m.Groups[1].Value + (m.Groups[2].Value == "Client" ? " (Клиент)" : " (Поставщик)") + "</style>" + m.Groups[3].Value + "- Услуги контрактного производства, до " + m.Groups[5].Value + " " + m.Groups[6].Value);
                const string yieldHeader = "It's the percentage of <style=Bold>functional dies</style> on a wafer.\n\n";
                if (result.StartsWith(yieldHeader, StringComparison.Ordinal))
                {
                    string header = translate(yieldHeader + "#nodeList");
                    if (header != null && header.EndsWith("#nodeList", StringComparison.Ordinal))
                        result = header.Substring(0, header.Length - "#nodeList".Length) + result.Substring(yieldHeader.Length);
                }
                result = HiddenNodes.Replace(result, "$1Скрыто</color> $2 $3полностью оптимизированных техпроцессов</color>");
                result = Node.Replace(result, "$1/мес.$2");
                if (DescriptionMarker.Matches(result).Count >= 2)
                {
                    var lines = result.Split('\n');
                    for (int i = 0; i < lines.Length; i++)
                    {
                        bool cr = lines[i].EndsWith("\r", StringComparison.Ordinal);
                        string line = cr ? lines[i].Substring(0, lines[i].Length - 1) : lines[i];
                        // Descriptions start with a hardware name, even if the player named it like a UI label.
                        if (i > 0 && (DescriptionLine.IsMatch(line) || CapacityLine.IsMatch(line)))
                            lines[i] = (translate(line) ?? line) + (cr ? "\r" : "");
                    }
                    result = string.Join("\n", lines);
                }
                result = RequiredCache.Replace(result, "$1 (требуется $2)</color>");
                if (result.StartsWith("Money already spent on this project won't be recovered.", StringComparison.Ordinal) || result.StartsWith("Деньги, уже потраченные на этот проект", StringComparison.Ordinal))
                {
                    result = result.Replace("Money already spent on this project won't be recovered.", "Деньги, уже потраченные на этот проект, не будут возвращены.");
                    result = WarningCost.Replace(result, "Оставшаяся стоимость: ");
                }
                return result;
            }
            catch (RegexMatchTimeoutException)
            {
                return source;
            }
        }
    }
}
