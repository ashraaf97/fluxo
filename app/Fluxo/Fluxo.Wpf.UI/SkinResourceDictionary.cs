using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

namespace Fluxo.Wpf.UI
{
    public class SkinResourceDictionary : ResourceDictionary
    {
        private Uri _darkSource;
        private Uri _lightSource;

        public Uri DarkSource
        {
            get { return _darkSource; }
            set
            {
                _darkSource = value;
                UpdateSource();
            }
        }
        public Uri LightSource
        {
            get { return _lightSource; }
            set
            {
                _lightSource = value;
                UpdateSource();
            }
        }

        /// <summary>
        /// Re-reads <see cref="App.Skin"/> and swaps in the matching token file.
        /// Assigning Source republishes the dictionary's contents, which is what
        /// makes every DynamicResource reference re-resolve against the new palette.
        /// Called by <see cref="ThemeManager"/> when the user changes theme.
        /// </summary>
        public void Refresh()
        {
            UpdateSource();
        }

        private void UpdateSource()
        {
            var val = App.Skin == Skin.Dark ? DarkSource : LightSource;
            if (val != null && base.Source != val)
                base.Source = val;
        }
    }
}
