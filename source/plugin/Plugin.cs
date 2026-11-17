using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using TMPro;
using UnityEngine;

namespace ProcessorTycoonRussian
{
    [BepInPlugin("ru.processortycoon.localization", "Processor Tycoon Russian", "1.0.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal Translator Translator;
        readonly HashSet<string> missing = new HashSet<string>();
        readonly Dictionary<TMP_Text, DisplayProcessor> processors = new Dictionary<TMP_Text, DisplayProcessor>();
        string folder;
        float nextReport, nextScan;
        bool captured;

        void Awake()
        {
            folder = Path.GetDirectoryName(Info.Location);
            try
            {
                Translator = new Translator(Path.Combine(folder, "translations"));
                if (Translator.ExactCount == 0) throw new InvalidDataException("Translation dictionary is empty");
                Translator.Missing = value => { if (missing.Count < 4000) missing.Add(value); };
                Logger.LogInfo($"RU_READY exact={Translator.ExactCount} templates={Translator.RuleCount}; TMP display preprocessing; backing values preserved");
            }
            catch (Exception ex)
            {
                Logger.LogError("Russian localization disabled: " + ex);
                enabled = false;
            }
        }

        sealed class DisplayProcessor : ITextPreprocessor
        {
            readonly Plugin plugin;
            readonly TMP_Text owner;
            internal readonly ITextPreprocessor Original;
            readonly float originalFontSize, originalMin, originalMax, originalWidth;
            readonly bool originalAuto;
            readonly TextWrappingModes originalWrapping;
            readonly bool nameOnly, legend;
            readonly bool layoutTarget;
            bool layoutChanged, widthChanged;
            public DisplayProcessor(Plugin plugin, TMP_Text owner, ITextPreprocessor original)
            {
                this.plugin = plugin; this.owner = owner; Original = original;
                originalFontSize = owner.fontSize; originalMin = owner.fontSizeMin; originalMax = owner.fontSizeMax;
                originalWidth = owner.rectTransform.rect.width; originalAuto = owner.enableAutoSizing; originalWrapping = owner.textWrappingMode;
                layoutTarget = LayoutFixes.IsTarget(owner);
                var objectName = owner.gameObject.name.ToLowerInvariant();
                nameOnly = objectName.Contains("companyname") || objectName.Contains("foundername") || objectName.Contains("cpuname") || objectName.Contains("teamname") || objectName.Contains("productname");
                foreach (var component in owner.GetComponentsInParent<MonoBehaviour>(true))
                {
                    if (component == null) continue;
                    var type = component.GetType();
                    if (type.FullName == "ProcessorTycoon.GraphSystem.LegendText") legend = true;
                    if (type.FullName == "ProcessorTycoon.Save.UI.LoadOption") nameOnly = true;
                    if (type.FullName == "ProcessorTycoon.Desktop.Finance.FinanceGraphWindow" || type.FullName == "ProcessorTycoon.Desktop.Production.ProductionGraphWindow")
                    {
                        var field = type.GetField("companiesDropdown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        var dropdown = field?.GetValue(component) as Component;
                        if (dropdown != null && owner.transform.IsChildOf(dropdown.transform)) nameOnly = true;
                    }
                    if (type.FullName == "ProcessorTycoon.Hardware.Icons.HardwareIcon")
                        foreach (var field in type.GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
                            if ((field.Name == "primaryText" || field.Name == "secondaryText") && ReferenceEquals(field.GetValue(component), owner)) nameOnly = true;
                }
            }
            internal void RestoreLayout()
            {
                if (!layoutChanged) return;
                owner.enableAutoSizing = originalAuto; owner.fontSize = originalFontSize;
                owner.fontSizeMin = originalMin; owner.fontSizeMax = originalMax; owner.textWrappingMode = originalWrapping;
                if (widthChanged) owner.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, originalWidth);
                widthChanged = false;
                layoutChanged = false;
            }
            public string PreprocessText(string source)
            {
                var original = Original == null ? source : Original.PreprocessText(source);
                if (string.IsNullOrEmpty(original)) { RestoreLayout(); return original; }
                try
                {
                    if (nameOnly) { RestoreLayout(); return original; }
                    if (legend && original != "Revenues" && original != "Expenses" && original != "Potential Sales")
                    {
                        bool finance = false;
                        foreach (var parent in owner.GetComponentsInParent<MonoBehaviour>(true))
                            if (parent != null && (parent.GetType().FullName == "ProcessorTycoon.Desktop.Finance.FinanceGraphWindow" || parent.GetType().FullName == "ProcessorTycoon.Desktop.Production.ProductionGraphWindow")) finance = true;
                        if (!finance) { RestoreLayout(); return original; }
                    }
                    var translated = plugin.Translator.Translate(original);
                    bool mainMenu = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex == 0 &&
                        (original == "New Game" || original == "Load Game" || original == "Settings" || original == "Credits" || original == "Quit");
                    bool shortLabel = translated != original && original.Length < 100 && !original.Contains("\n") && !original.Contains("\r");
                    if (translated == original) RestoreLayout();
                    bool applyLayout = mainMenu || layoutTarget;
                    if (!applyLayout || (widthChanged || !shortLabel)) RestoreLayout();
                    if (mainMenu)
                    {
                        layoutChanged = true;
                        owner.textWrappingMode = TextWrappingModes.NoWrap;
                        owner.enableAutoSizing = false;
                        owner.fontSize = 24f;
                        var rect = owner.rectTransform;
                        if (rect.rect.width < 320f)
                        {
                            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 320f);
                            widthChanged = true;
                        }
                    }
                    else if (layoutTarget && shortLabel)
                    {
                        layoutChanged = true;
                        owner.textWrappingMode = originalWrapping;
                        owner.enableAutoSizing = true;
                        owner.fontSizeMax = originalFontSize;
                        owner.fontSizeMin = Math.Min(owner.fontSizeMin, 9f);
                    }
                    return translated;
                }
                catch (Exception ex) { plugin.Logger.LogError("Translation: " + ex); return original; }
            }
        }

        void Update()
        {
            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + 0.5f;
                foreach (var text in Resources.FindObjectsOfTypeAll<TMP_Text>())
                {
                    if (!text || !text.gameObject.scene.IsValid()) continue;
                    if (text.textPreprocessor is DisplayProcessor) continue;
                    var input = text.GetComponentInParent<TMP_InputField>();
                    if (input != null && input.textComponent == text) continue;
                    var processor = new DisplayProcessor(this, text, text.textPreprocessor);
                    processors[text] = processor;
                    text.textPreprocessor = processor;
                    text.havePropertiesChanged = true;
                    text.SetAllDirty();
                }
                var dead = new List<TMP_Text>();
                foreach (var item in processors) if (!item.Key) dead.Add(item.Key);
                foreach (var item in dead) processors.Remove(item);
            }
            if (Time.unscaledTime < nextReport) return;
            nextReport = Time.unscaledTime + 15f;
            try
            {
                File.WriteAllLines(Path.Combine(folder, "untranslated.log"), missing);
                Logger.LogInfo($"RU_STATS translated={Translator.Hits} unknown={missing.Count} components={processors.Count}");
                if (!captured && Environment.GetCommandLineArgs() is string[] args && Array.IndexOf(args, "-ru-test-capture") >= 0 && Time.unscaledTime > 10)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(folder, "verified-screen.png"));
                    captured = true;
                }
            }
            catch (Exception ex) { Logger.LogWarning(ex.Message); }
        }

        void OnDestroy()
        {
            foreach (var item in processors)
                if (item.Key && item.Key.textPreprocessor == item.Value)
                {
                    item.Value.RestoreLayout();
                    item.Key.textPreprocessor = item.Value.Original;
                    item.Key.havePropertiesChanged = true;
                    item.Key.SetAllDirty();
                }
        }
    }
}
