using UnityEngine;
using Verse;

namespace Scenarios_Menu_Search
{
    public static class ModSettingsWindow
    {
        public static void Draw(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled(
                "ScenariosMenuSearch.Settings.ShowSourcesButton".Translate(),
                ref ScenariosMenuSearchSettings.showSourcesButton,
                "ScenariosMenuSearch.Settings.ShowSourcesButtonDesc".Translate());
            listing.End();
        }
    }
}
