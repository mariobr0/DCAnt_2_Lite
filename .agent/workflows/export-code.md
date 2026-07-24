---
name: /export-code
description: Собирает весь исходный код проекта в один Markdown-файл AllCode_[название].md
---

# Сборка кода в единый файл

Когда пользователь вызывает команду `/export-code [название_сборки]`, выполни следующие действия:

1. Определи название сборки из аргументов. Если пользователь не передал название, используй короткий хэш текущего git-коммита (например, через `git rev-parse --short HEAD`).
2. Используй инструмент `run_command` для выполнения PowerShell-скрипта, который найдет все файлы `.cs` и `.csproj` в папке `src` и объединит их.
3. Обязательно используй кодировку UTF-8 (например, `[System.Text.Encoding]::UTF8`) для сохранения, чтобы не повредить кириллицу.
4. Ответь пользователю, приложив кликабельную markdown-ссылку на созданный файл.

**Пример скрипта PowerShell для выполнения задачи:**

```powershell
$name = "название_сборки"
$outFile = "C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\AllCode_$name.md"
$files = Get-ChildItem -Path .\src -Include *.cs, *.csproj -Recurse -File

$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine("# Code Dump: $name`n")

foreach ($f in $files) { 
    [void]$sb.AppendLine("## $($f.FullName)`n")
    [void]$sb.AppendLine('```csharp')
    [void]$sb.AppendLine([System.IO.File]::ReadAllText($f.FullName))
    [void]$sb.AppendLine('```') 
}

[System.IO.File]::WriteAllText($outFile, $sb.ToString(), [System.Text.Encoding]::UTF8)
```
