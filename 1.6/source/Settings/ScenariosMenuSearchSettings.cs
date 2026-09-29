using Verse;

namespace Scenarios_Menu_Search
{
    public class ScenariosMenuSearchSettings : ModSettings
    {
        public static bool showSourcesButton = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref showSourcesButton, "showSourcesButton", true);
        }
    }
}
