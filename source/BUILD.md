# Сборка исходников

Требуется .NET SDK 11 и Windows для установщика.

Переводчик отдельно от Unity проверяется командой из этой папки:

```powershell
dotnet run --project tests/Translator.Tests.csproj -- .
```

Плагин: в `plugin/RussianMod.csproj` задайте `GameDir` на свою копию Processor Tycoon; ссылки BepInEx должны указывать на `bepinex-reference/BepInEx/core` рядом с папкой plugin. Затем:

```powershell
dotnet build plugin/RussianMod.csproj -c Release
```

Установщик требует `dist/payload.zip` относительно папки source (либо свойство `PayloadPath`). Архив должен включать разрешённые файлы BepInEx, DLL плагина, Newtonsoft.Json.dll и четыре словаря; точный allowlist в InstallerEngine.cs. DLL самой игры не включайте. Все ZIP-пути используют `/`.

```powershell
dotnet publish installer/ProcessorTycoon.Installer.csproj -c Release -t:Rebuild -o publish
```

`-t:Rebuild` обязателен после смены встроенного payload, чтобы исключить устаревший ресурс. Проверяйте контрольные суммы установленных файлов относительно содержимого payload.
