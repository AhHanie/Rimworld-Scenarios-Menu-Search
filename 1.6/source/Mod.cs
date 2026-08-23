using HarmonyLib;
using Verse;

namespace Scenarios_Menu_Search
{
    public class Mod : Verse.Mod
    {
        public Mod(ModContentPack content) : base(content)
        {
            LongEventHandler.QueueLongEvent(Init, "ScenariosMenuSearch.LoadingLabel", doAsynchronously: true, null);
        }

        private void Init()
        {
            new Harmony("sk.scenariosearch").PatchAll();
        }
    }
}
