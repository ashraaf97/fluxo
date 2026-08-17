using System.Windows;
using Translations;

namespace Fluxo.Wpf.UI
{
    internal class TranslationResourceDictionary : ResourceDictionary
    {
        public TranslationResourceDictionary()
        {
            foreach (var key in TextResource.GetKeys())
            {
                Add(key, TextResource.GetText(key));
            }
        }
    }
}
