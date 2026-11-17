using System;
using System.Collections;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace ProcessorTycoonRussian
{
    internal static class LayoutFixes
    {
        static readonly string[] TargetTypes =
        {
            "ProcessorTycoon.Production.UI.ProductionUI",
            "ProcessorTycoon.MarketSystem.UI.MarketShareWindow",
            "ProcessorTycoon.MarketSystem.UI.MarketSalesSpreadsheetWindow",
            "ProcessorTycoon.MarketSystem.UI.MarketSalesGraphWindow",
            "ProcessorTycoon.Desktop.Finance.FinanceGraphWindow",
            "ProcessorTycoon.Desktop.Production.ProductionGraphWindow",
            "ProcessorTycoon.Desktop.Statistics.StatisticsWindow",
            "ProcessorTycoon.CompanySystem.Business.CompanyUI",
            "ProcessorTycoon.CompanySystem.Business.BusinessContractUI",
            "ProcessorTycoon.PopupSystem.Popup"
        };

        internal static bool IsTabCaption(TMP_Text text)
        {
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            foreach (var component in text.GetComponentsInParent<MonoBehaviour>(true))
            {
                if (component == null) continue;
                var type = component.GetType();
                if (type.FullName != "ProcessorTycoon.UI.ButtonTabGroup") continue;
                var variant = type.GetField("backgroundVariant", fields)?.GetValue(component)?.ToString();
                if (variant != "Analysis" && variant != "FactoryManagement") continue;
                var tabs = type.GetField("tabs", fields)?.GetValue(component) as IEnumerable;
                if (tabs == null) continue;
                foreach (var tab in tabs)
                {
                    var button = tab?.GetType().GetProperty("Button")?.GetValue(tab, null) as Component;
                    if (button != null && text.transform.IsChildOf(button.transform)) return true;
                }
            }
            return false;
        }

        internal static bool IsTarget(TMP_Text text)
        {
            if (text == null) return false;
            if (text.GetComponentInParent<TMP_Dropdown>(true) != null) return true;

            foreach (var component in text.GetComponentsInParent<MonoBehaviour>(true))
            {
                if (component == null) continue;
                var fullName = component.GetType().FullName;
                if (Array.IndexOf(TargetTypes, fullName) >= 0) return true;
            }
            return false;
        }
    }
}
