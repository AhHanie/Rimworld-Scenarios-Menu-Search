using System;
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
    internal enum ScenarioSourceKind
    {
        All,
        Mod,
        Local,
        Workshop,
        Unknown
    }

    internal sealed class ScenarioModSourceChoice
    {
        public string PackageId;
        public string Label;
    }

    internal sealed class ScenarioSearchState
    {
        public readonly QuickSearchWidget Search = new QuickSearchWidget();
        public ScenarioSourceKind SourceKind = ScenarioSourceKind.All;
        public string SourceModPackageId;
        public bool? LastShowSourcesButton;
    }

    [HarmonyPatch(typeof(Page_SelectScenario), nameof(Page_SelectScenario.DoScenarioSelectionList))]
    internal static class Page_SelectScenarioPatch
    {
        private static readonly ConditionalWeakTable<Page_SelectScenario, ScenarioSearchState> States = new ConditionalWeakTable<Page_SelectScenario, ScenarioSearchState>();

        private static bool Prefix(Page_SelectScenario __instance, Rect rect)
        {
            ScenarioSearchState state = States.GetValue(__instance, _ => new ScenarioSearchState());
            QuickSearchWidget search = state.Search;

            bool showSourcesButton = ScenariosMenuSearchSettings.showSourcesButton;

            if (state.LastShowSourcesButton.HasValue && state.LastShowSourcesButton.Value != showSourcesButton)
            {
                __instance.scenariosScrollPosition = Vector2.zero;
            }
            state.LastShowSourcesButton = showSourcesButton;

            if (!showSourcesButton)
            {
                if (state.SourceKind != ScenarioSourceKind.All || state.SourceModPackageId != null)
                {
                    __instance.scenariosScrollPosition = Vector2.zero;
                }
                state.SourceKind = ScenarioSourceKind.All;
                state.SourceModPackageId = null;
            }

            Dictionary<Scenario, ScenarioDef> defLookup = null;
            List<ScenarioModSourceChoice> modChoices = null;
            bool hasUnknownSource = false;

            if (showSourcesButton)
            {
                defLookup = BuildScenarioDefLookup();
                modChoices = BuildModChoices(defLookup);
                hasUnknownSource = HasUnknownSource(defLookup);

                if (state.SourceKind == ScenarioSourceKind.Mod && !modChoices.Any(choice => choice.PackageId == state.SourceModPackageId))
                {
                    state.SourceKind = ScenarioSourceKind.All;
                    state.SourceModPackageId = null;
                }
                else if (state.SourceKind == ScenarioSourceKind.Unknown && !hasUnknownSource)
                {
                    state.SourceKind = ScenarioSourceKind.All;
                }
            }

            Rect searchRect = rect;
            searchRect.height = QuickSearchWidget.WidgetHeight;

            if (showSourcesButton)
            {
                Rect sourceRect = rect;
                sourceRect.height = QuickSearchWidget.WidgetHeight;
                searchRect.y = sourceRect.yMax + 4f;

                DrawSourceDropdown(sourceRect, state, modChoices, hasUnknownSource, delegate
                {
                    __instance.scenariosScrollPosition = Vector2.zero;
                });
            }

            Rect listRect = rect;
            listRect.yMin = searchRect.yMax + 4f;

            bool includeFromDef = !showSourcesButton || state.SourceKind == ScenarioSourceKind.All || state.SourceKind == ScenarioSourceKind.Mod || state.SourceKind == ScenarioSourceKind.Unknown;
            bool includeLocal = !showSourcesButton || state.SourceKind == ScenarioSourceKind.All || state.SourceKind == ScenarioSourceKind.Local;
            bool includeWorkshop = !showSourcesButton || state.SourceKind == ScenarioSourceKind.All || state.SourceKind == ScenarioSourceKind.Workshop;

            IEnumerable<Scenario> fromDefCandidates = ScenarioLister.ScenariosInCategory(ScenarioCategory.FromDef);
            if (showSourcesButton)
            {
                fromDefCandidates = fromDefCandidates.Where(scenario => MatchesModSource(scenario, state, defLookup));
            }

            List<Scenario> fromDef = includeFromDef
                ? VisibleScenarios(search, fromDefCandidates)
                : new List<Scenario>();
            List<Scenario> customLocal = includeLocal
                ? VisibleScenarios(search, ScenarioLister.ScenariosInCategory(ScenarioCategory.CustomLocal))
                : new List<Scenario>();
            List<Scenario> steamWorkshop = includeWorkshop
                ? VisibleScenarios(search, ScenarioLister.ScenariosInCategory(ScenarioCategory.SteamWorkshop))
                : new List<Scenario>();

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

            bool anySectionDrawn = false;
            if (includeFromDef)
            {
                ListScenariosOnListing(__instance, listing, fromDef);
                anySectionDrawn = true;
            }
            if (includeLocal)
            {
                if (anySectionDrawn)
                {
                    listing.Gap();
                }
                Text.Font = GameFont.Small;
                listing.Label("ScenariosCustom".Translate());
                ListScenariosOnListing(__instance, listing, customLocal);
                anySectionDrawn = true;
            }
            if (includeWorkshop)
            {
                if (anySectionDrawn)
                {
                    listing.Gap();
                }
                Text.Font = GameFont.Small;
                listing.Label("ScenariosSteamWorkshop".Translate());
                if (listing.ButtonText("OpenSteamWorkshop".Translate()))
                {
                    SteamUtility.OpenSteamWorkshopPage();
                }
                ListScenariosOnListing(__instance, listing, steamWorkshop);
            }
            listing.End();
            __instance.totalScenarioListHeight = listing.CurHeight;
            Widgets.EndScrollView();

            return false;
        }

        private static Dictionary<Scenario, ScenarioDef> BuildScenarioDefLookup()
        {
            Dictionary<Scenario, ScenarioDef> lookup = new Dictionary<Scenario, ScenarioDef>();
            foreach (ScenarioDef def in DefDatabase<ScenarioDef>.AllDefs)
            {
                if (def?.scenario != null)
                {
                    lookup[def.scenario] = def;
                }
            }
            return lookup;
        }

        private static List<ScenarioModSourceChoice> BuildModChoices(Dictionary<Scenario, ScenarioDef> defLookup)
        {
            HashSet<string> seen = new HashSet<string>();
            List<ScenarioModSourceChoice> choices = new List<ScenarioModSourceChoice>();
            foreach (ScenarioDef def in defLookup.Values)
            {
                if (!def.scenario.showInUI)
                {
                    continue;
                }
                ModContentPack pack = def.modContentPack;
                if (pack == null || !seen.Add(pack.PackageId))
                {
                    continue;
                }
                choices.Add(new ScenarioModSourceChoice { PackageId = pack.PackageId, Label = pack.Name });
            }
            choices.Sort((a, b) =>
            {
                int cmp = string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase);
                return cmp != 0 ? cmp : string.Compare(a.PackageId, b.PackageId, StringComparison.OrdinalIgnoreCase);
            });
            return choices;
        }

        private static bool HasUnknownSource(Dictionary<Scenario, ScenarioDef> defLookup)
        {
            foreach (ScenarioDef def in defLookup.Values)
            {
                if (def.scenario.showInUI && def.modContentPack == null)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool MatchesModSource(Scenario scenario, ScenarioSearchState state, Dictionary<Scenario, ScenarioDef> defLookup)
        {
            if (state.SourceKind == ScenarioSourceKind.All)
            {
                return true;
            }
            defLookup.TryGetValue(scenario, out ScenarioDef def);
            ModContentPack pack = def?.modContentPack;
            if (state.SourceKind == ScenarioSourceKind.Mod)
            {
                return pack != null && pack.PackageId == state.SourceModPackageId;
            }
            if (state.SourceKind == ScenarioSourceKind.Unknown)
            {
                return pack == null;
            }
            return true;
        }

        private static string GetSourceLabel(ScenarioSearchState state, List<ScenarioModSourceChoice> modChoices)
        {
            switch (state.SourceKind)
            {
                case ScenarioSourceKind.Local:
                    return "ScenariosMenuSearch.LocalSource".Translate();
                case ScenarioSourceKind.Workshop:
                    return "ScenariosMenuSearch.WorkshopSource".Translate();
                case ScenarioSourceKind.Unknown:
                    return "ScenariosMenuSearch.UnknownSource".Translate();
                case ScenarioSourceKind.Mod:
                    foreach (ScenarioModSourceChoice choice in modChoices)
                    {
                        if (choice.PackageId == state.SourceModPackageId)
                        {
                            return choice.Label;
                        }
                    }
                    return "ScenariosMenuSearch.AllSources".Translate();
                default:
                    return "ScenariosMenuSearch.AllSources".Translate();
            }
        }

        private static void DrawSourceDropdown(Rect rect, ScenarioSearchState state, List<ScenarioModSourceChoice> modChoices, bool hasUnknownSource, Action onChanged)
        {
            Rect labelRect = rect;
            labelRect.width = Mathf.Min(60f, rect.width * 0.3f);
            Rect buttonRect = rect;
            buttonRect.xMin = labelRect.xMax + 4f;

            Widgets.Label(labelRect, "ScenariosMenuSearch.Source".Translate());

            string currentLabel = GetSourceLabel(state, modChoices);
            string truncatedLabel = currentLabel.Truncate(buttonRect.width - 16f);
            if (Widgets.ButtonText(buttonRect, truncatedLabel))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>
                {
                    new FloatMenuOption("ScenariosMenuSearch.AllSources".Translate(), delegate
                    {
                        state.SourceKind = ScenarioSourceKind.All;
                        state.SourceModPackageId = null;
                        onChanged();
                    })
                };
                foreach (ScenarioModSourceChoice choice in modChoices)
                {
                    string packageId = choice.PackageId;
                    options.Add(new FloatMenuOption(choice.Label, delegate
                    {
                        state.SourceKind = ScenarioSourceKind.Mod;
                        state.SourceModPackageId = packageId;
                        onChanged();
                    }));
                }
                options.Add(new FloatMenuOption("ScenariosMenuSearch.LocalSource".Translate(), delegate
                {
                    state.SourceKind = ScenarioSourceKind.Local;
                    state.SourceModPackageId = null;
                    onChanged();
                }));
                options.Add(new FloatMenuOption("ScenariosMenuSearch.WorkshopSource".Translate(), delegate
                {
                    state.SourceKind = ScenarioSourceKind.Workshop;
                    state.SourceModPackageId = null;
                    onChanged();
                }));
                if (hasUnknownSource)
                {
                    options.Add(new FloatMenuOption("ScenariosMenuSearch.UnknownSource".Translate(), delegate
                    {
                        state.SourceKind = ScenarioSourceKind.Unknown;
                        state.SourceModPackageId = null;
                        onChanged();
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            if (currentLabel != truncatedLabel)
            {
                TooltipHandler.TipRegion(buttonRect, currentLabel);
            }
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
