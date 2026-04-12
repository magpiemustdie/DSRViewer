using System;
using System.Collections.Generic;
using System.Text;

namespace DSRViewer.FileProcess
{
    /// <summary>
    /// Патчит байты FLVER напрямую — без десериализации/сериализации.
    /// Используется как fallback когда FLVER2.Write() падает с ошибкой.
    ///
    /// Особенности FLVER:
    /// - Строки хранятся как null-terminated UTF-16LE в строковой секции
    /// - Путь текстуры вида "N:\path\to\name" — игра читает только имя после последнего '\'
    ///   Начало пути игрой игнорируется, поэтому его можно обрезать
    /// - Строки идут по порядку — патчим N-е вхождение, не все подряд
    /// - Если новая строка длиннее старой — обрезаем начало новой строки
    /// </summary>
    public static class FlverBytePatcher
    {
        /// <summary>
        /// Описывает одну замену строки: старое значение, новое, порядковый номер вхождения (0-based).
        /// </summary>
        public record StringPatch(string OldValue, string NewValue, int Occurrence = 0);

        // ── Публичный API ────────────────────────────────────────────────

        /// <summary>
        /// Применяет список патчей к байтам FLVER.
        /// Каждый патч заменяет конкретное (Occurrence-е) вхождение строки.
        /// Если новая строка длиннее — обрезается с начала.
        /// Возвращает изменённые байты и список предупреждений.
        /// </summary>
        public static (byte[] result, List<string> warnings) Apply(
            byte[] flverBytes, IEnumerable<StringPatch> patches)
        {
            if (flverBytes == null || flverBytes.Length == 0)
                return (flverBytes, ["Empty input"]);

            byte[] result = (byte[])flverBytes.Clone();
            var warnings = new List<string>();

            foreach (var patch in patches)
            {
                if (string.IsNullOrEmpty(patch.OldValue)) continue;
                if (patch.OldValue == patch.NewValue) continue;

                var (ok, truncated) = PatchOccurrence(result, patch.OldValue, patch.NewValue ?? "", patch.Occurrence);

                if (!ok)
                    warnings.Add($"[{patch.Occurrence}] '{patch.OldValue}' → '{patch.NewValue}': не найдено");
                else if (truncated != null)
                {
                    warnings.Add($"[{patch.Occurrence}] Truncated: '{patch.NewValue}' → '{truncated}' (new was longer than old)");
                    Console.WriteLine($"[FlverBytePatcher] [{patch.Occurrence}] '{patch.OldValue}' → '{truncated}' (truncated from '{patch.NewValue}')");
                }
                else
                    Console.WriteLine($"[FlverBytePatcher] [{patch.Occurrence}] '{patch.OldValue}' → '{patch.NewValue}'");
            }

            return (result, warnings);
        }

        // ── Внутренняя логика ────────────────────────────────────────────

        /// <summary>
        /// Заменяет occurrence-е вхождение строки oldValue на newValue.
        /// Если newValue короче — дополняет нулями.
        /// Если newValue длиннее — обрезает начало (TruncateToFit).
        /// Возвращает (успех, усечённая строка или null если усечения не было).
        /// </summary>
        private static (bool ok, string truncated) PatchOccurrence(
            byte[] data, string oldValue, string newValue, int occurrence)
        {
            // Пробуем UTF-16LE (основной формат DS1/DSR)
            var r = TryPatchOccurrenceEncoding(data, oldValue, newValue, occurrence, Encoding.Unicode);
            if (r.ok) return r;

            // Fallback: UTF-8
            return TryPatchOccurrenceEncoding(data, oldValue, newValue, occurrence, Encoding.UTF8);
        }

        private static (bool ok, string truncated) TryPatchOccurrenceEncoding(
            byte[] data, string oldValue, string newValue, int occurrence, Encoding enc)
        {
            byte[] oldBytes = enc.GetBytes(oldValue + "\0");
            byte[] newBytes = enc.GetBytes(newValue + "\0");

            string truncated = null;

            // Если новая строка длиннее — обрезаем начало
            if (newBytes.Length > oldBytes.Length)
            {
                newValue  = TruncateToFit(newValue, oldBytes.Length, enc);
                newBytes  = enc.GetBytes(newValue + "\0");
                truncated = newValue;

                // После усечения всё ещё не влезает — отказываем
                if (newBytes.Length > oldBytes.Length)
                    return (false, null);
            }

            int found = 0;
            int searchFrom = 0;

            while (true)
            {
                int idx = IndexOf(data, oldBytes, searchFrom);
                if (idx < 0) return (false, null);

                if (found == occurrence)
                {
                    Array.Copy(newBytes, 0, data, idx, newBytes.Length);
                    // Заполняем остаток нулями
                    for (int i = idx + newBytes.Length; i < idx + oldBytes.Length; i++)
                        data[i] = 0;
                    return (true, truncated);
                }

                found++;
                searchFrom = idx + oldBytes.Length;
            }
        }

        /// <summary>
        /// Обрезает строку с начала так чтобы enc.GetBytes(result + "\0").Length <= maxBytes.
        /// Для пути текстуры "N:\path\to\name" — обрезает лишние сегменты пути слева,
        /// сохраняя как можно больше значимой части (имя файла в конце).
        /// </summary>
        private static string TruncateToFit(string value, int maxBytes, Encoding enc)
        {
            // Для пути: обрезаем сегменты слева по одному
            // "N:\very\long\path\name" → "very\long\path\name" → "long\path\name" → "path\name" → "name"
            if (value.Contains('\\'))
            {
                string current = value;
                while (current.Contains('\\'))
                {
                    int slash = current.IndexOf('\\');
                    current = current[(slash + 1)..];
                    if (enc.GetBytes(current + "\0").Length <= maxBytes)
                        return current;
                }
                // current теперь — имя без слешей, но всё ещё не влезает → падаем в посимвольную обрезку
            }

            // Обрезаем посимвольно с начала
            for (int i = 1; i < value.Length; i++)
            {
                string candidate = value[i..];
                if (enc.GetBytes(candidate + "\0").Length <= maxBytes)
                    return candidate;
            }

            return "";
        }

        /// <summary>Поиск подмассива байт начиная с позиции start.</summary>
        private static int IndexOf(byte[] data, byte[] pattern, int start = 0)
        {
            int limit = data.Length - pattern.Length;
            for (int i = start; i <= limit; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                    if (data[i + j] != pattern[j]) { match = false; break; }
                if (match) return i;
            }
            return -1;
        }
    }
}
