using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fluxo.Core;

namespace Fluxo.Core.UI
{
    public interface ISettingsPage
    {
        void PopulateUI();
        void UpdateConfig();
    }
}
