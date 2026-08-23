using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Steam;

namespace Scenarios_Menu_Search.Patches
{
    internal sealed class ScenarioSearchState
    {
        public readonly QuickSearchWidget Search = new QuickSearchWidget();
    }

    [HarmonyPatch(typeof(Page_SelectScenario), nameof(Page_SelectScenario.DoScenarioSelectionList))]
    internal static class Page_SelectScenarioPatch
    {
        private static readonly ConditionalWeakTable<Page_SelectScenario, ScenarioSearchState> States = new ConditionalWeakTable<Page_SelectScenario, ScenarioSearchState>();

        private static bool Prefix(Page_SelectScenario __instance, Rect rect)
        {
            ScenarioSearchState state = States.GetValue(__instance, _ => new ScenarioSearchState());
            QuickSearchWidget search = state.Search;

            Rect searchRect = rect;
            searchRect.height = QuickSearchWidget.WidgetHeight;

            Rect listRect = rect;
            listRect.yMin = searchRect.yMax + 4f;

            List<Scenario> fromDef = VisibleScenarios(search, ScenarioLister.ScenariosInCategory(ScenarioCategory.FromDef));
            List<Scenario> customLocal = VisibleScenarios(search, ScenarioLister.ScenariosInCategory(ScenarioCategory.CustomLocal));
            List<Scenario> steamWorkshop = VisibleScenarios(search, ScenarioLister.ScenariosInCategory(ScenarioCategory.SteamWorkshop));

            search.noResultsMatched = search.filter.Active && fromDef.Count == 0 && customLocal.Count == 0 && steamWorkshop.Count == 0;
            search.OnGUI(searchRect, delegate
            {
                __instance.scenariosScrollPosition = Vector2.zero;
            });

            listRect.xMax += 2f;
            Rect viewRect = new Rect(0f, 0f, listRect.width - 16f - 2f, __instance.totalScenarioListHeight + 250f);
            Widgets.BeginScrollView(listRect, ref __instance.scenariosScrollPosition, viewRect);
            Rect columnRect = viewRect.AtZero();
            columnRect.height = 999999f;
            Listing_Standard listing = new Listing_Standard();
            listing.ColumnWidth = viewRect.width;
            listing.Begin(columnRect);
            Text.Font = GameFont.Small;
            ListScenariosOnListing(__instance, listing, fromDef);
            listing.Gap();
            Text.Font = GameFont.Small;
            listing.Label("ScenariosCustom".Translate());
            ListScenariosOnListing(__instance, listing, customLocal);
            listing.Gap();
            Text.Font = GameFont.Small;
            listing.Label("ScenariosSteamWorkshop".Translate());
            if (listing.ButtonText("OpenSteamWorkshop".Translate()))
            {
                SteamUtility.OpenSteamWorkshopPage();
            }
            ListScenariosOnListing(__instance, listing, steamWorkshop);
            listing.End();
            __instance.totalScenarioListHeight = listing.CurHeight;
            Widgets.EndScrollView();

            return false;
        }

        private static List<Scenario> VisibleScenarios(QuickSearchWidget search, IEnumerable<Scenario> scenarios)
        {
            return scenarios.Where(scenario => scenario.showInUI && (search.filter.Matches(scenario.name) || search.filter.Matches(scenario.GetSummary()))).ToList();
        }

        private static void ListScenariosOnListing(Page_SelectScenario instance, Listing_Standard listing, List<Scenario> scenarios)
        {
            bool any = false;
            foreach (Scenario scenario in scenarios)
            {
                if (any)
                {
                    listing.Gap(6f);
                }
                Rect rect = listing.GetRect(68f).ContractedBy(4f);
                instance.DoScenarioListEntry(rect, scenario);
                any = true;
            }
            if (!any)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.5f);
                listing.Label("(" + "NoneLower".Translate() + ")");
                GUI.color = Color.white;
            }
        }
    }
}
