using HarmonyLib;
using UnityEngine;
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
            GetSettings<ScenariosMenuSearchSettings>();
            new Harmony("sk.scenariosearch").PatchAll();
        }

        public override string SettingsCategory()
        {
            return "ScenariosMenuSearch.SettingsTitle".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            ModSettingsWindow.Draw(inRect);
            base.DoSettingsWindowContents(inRect);
        }
    }
}
