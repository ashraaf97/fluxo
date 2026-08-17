using System;
using System.Collections.Generic;
using System.IO;

namespace Translations
{
    public static class TextResource
    {
        private static Dictionary<string, string> texts = new();

        static TextResource()
        {
            // Fluxo ships English only, so there is no language index to consult -
            // the single resource file is loaded directly.
            var file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Lang", "English.txt");
            if (File.Exists(file))
            {
                LoadTexts(file);
            }
        }

        public static string GetText(string key)
        {
            if (texts.TryGetValue(key, out string? label) && label != null)
            {
                return label;
            }
            return string.Empty;
        }

        private static void LoadTexts(string path)
        {
            var lines = File.ReadAllLines(path);
            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }
                var index = line.IndexOf('=');
                var key = line.Substring(0, index);
                var val = line.Substring(index + 1);
                texts[key] = val;
            }
        }

        public static IEnumerable<string> GetKeys()
        {
            return texts.Keys;
        }
    }
}
