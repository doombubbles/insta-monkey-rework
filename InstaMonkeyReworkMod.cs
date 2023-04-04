using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppAssets.Scripts.Models.Powers;
using Il2CppAssets.Scripts.Models.Profile;
using Il2CppAssets.Scripts.Models.Towers;
using Il2CppAssets.Scripts.Simulation.Towers;
using Il2CppAssets.Scripts.Unity;
using Il2CppAssets.Scripts.Unity.Player;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using Il2CppAssets.Scripts.Unity.UI_New.InGame.RightMenu;
using Il2CppAssets.Scripts.Unity.UI_New.InGame.RightMenu.Powers;
using Il2CppAssets.Scripts.Unity.UI_New.InGame.StoreMenu;
using Il2CppAssets.Scripts.Unity.UI_New.Popups;
using BTD_Mod_Helper;
using BTD_Mod_Helper.Extensions;
using HarmonyLib;
using Il2CppTMPro;
using InstaMonkeyRework;
using MelonLoader;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

[assembly: MelonInfo(typeof(InstaMonkeyReworkMod), ModHelperData.Name, ModHelperData.Version, ModHelperData.RepoOwner)]
[assembly: MelonGame("Ninja Kiwi", "BloonsTD6")]

namespace InstaMonkeyRework;

public class InstaMonkeyReworkMod : BloonsTD6Mod
{
    public static bool ActuallyConsumeInsta;
    public static bool InInstaMode;
    public static TowerModel? InstaModel;
    public static bool Warned;

    public static Dictionary<int, string> SavedPlacedInstas = null!;

    public override void OnMainMenu()
    {
        SavedPlacedInstas = new Dictionary<int, string>();
    }

    public override void OnRestart()
    {
        SavedPlacedInstas = new Dictionary<int, string>();
    }

    public static int GetCostForThing(TowerModel towerModel)
    {
        var cost = Game.instance.model.GetTowerFromId(towerModel.name).cost +
                   towerModel.GetAppliedUpgrades().Sum(model => model.cost);

        switch (InGame.instance.SelectedDifficulty)
        {
            case "Easy":
                cost *= .85f;
                break;
            case "Hard":
                cost *= 1.08f;
                break;
            case "Impoppable":
                cost *= 1.2f;
                break;
        }

        cost *= 1 - .05f * towerModel.tier;
        return (int) (5 * Math.Round(cost / 5));
    }

    public static int GetCostForThing(Tower tower)
    {
        var cost = Game.instance.model.GetTowerFromId(tower.towerModel.name).cost;

        var towerManager = InGame.instance.GetTowerManager();
        var zoneDiscount = towerManager.GetZoneDiscount(tower.Position.ToVector3(), 0, 0);
        var discountMultiplier = towerManager.GetDiscountMultiplier(zoneDiscount);
        cost *= 1 - discountMultiplier;

        foreach (var appliedUpgrade in tower.towerModel.GetAppliedUpgrades())
        {
            float upgradeCost = appliedUpgrade.cost;
            zoneDiscount = towerManager.GetZoneDiscount(tower.Position.ToVector3(), appliedUpgrade.path,
                appliedUpgrade.tier);
            discountMultiplier = towerManager.GetDiscountMultiplier(zoneDiscount);
            upgradeCost *= 1 - discountMultiplier;
            cost += upgradeCost;
        }

        switch (InGame.instance.SelectedDifficulty)
        {
            case "Easy":
                cost *= .85f;
                break;
            case "Hard":
                cost *= 1.08f;
                break;
            case "Impoppable":
                cost *= 1.2f;
                break;
        }

        cost *= 1 - .05f * tower.towerModel.tier;
        return (int) (5 * Math.Round(cost / 5));
    }

    public static int GetTotalPlaced(string name)
    {
        return SavedPlacedInstas.Values.Count(value => value == name);
    }

    public override void OnUpdate()
    {
        if (InstaTowersMenu.instaTowersInstance != null && InGame.instance != null && InGame.instance.bridge != null)
        {
            foreach (var button in InstaTowersMenu.instaTowersInstance
                         .GetComponentsInChildren<StandardInstaTowerButton>())
            {
                var costText = button.GetComponentsInChildren<TextMeshProUGUI>()
                    .FirstOrDefault(text => text.name == "Cost");
                if (costText == null) continue;

                var cost = TextToInt(costText);
                costText.color = InGame.instance.GetCash() >= cost ? Color.white : Color.red;


                var useCount = button.GetUseCount();
                var placed = GetTotalPlaced(button.instaTowerModel.name);
                var discountText = button.GetComponentsInChildren<TextMeshProUGUI>()
                    .First(text => text.name == "Discount");
                if (placed >= useCount)
                {
                    costText.enabled = false;
                    discountText.enabled = false;
                    button.powerCountText.color = Color.red;
                }
                else
                {
                    costText.enabled = true;
                    discountText.enabled = true;
                    button.powerCountText.color = Color.white;
                }
            }
        }
    }

    public static int TextToInt(TextMeshProUGUI textMeshProUGUI)
    {
        return int.Parse(textMeshProUGUI.text.Substring(1).Replace(",", ""));
    }

    public override void OnTowerSaved(Tower tower, TowerSaveDataModel saveData)
    {
        if (SavedPlacedInstas.ContainsKey(tower.Id.Id))
        {
            saveData.metaData["InstaMonkeyRework"] = SavedPlacedInstas[tower.Id.Id];
        }
    }

    public override void OnTowerLoaded(Tower tower, TowerSaveDataModel saveData)
    {
        if (saveData.metaData.ContainsKey("InstaMonkeyRework"))
        {
            SavedPlacedInstas[tower.Id.Id] = saveData.metaData["InstaMonkeyRework"];
        }
    }

    public override void OnTowerDestroyed(Tower tower)
    {
        if (SavedPlacedInstas.ContainsKey(tower.Id.Id))
        {
            SavedPlacedInstas.Remove(tower.Id.Id);
        }
    }

    [HarmonyPatch(typeof(InputManager), nameof(InputManager.Update))]
    internal class InputManager_Update
    {
        [HarmonyPostfix]
        internal static void Postfix(InputManager __instance)
        {
            if (!__instance.inInstaMode || InInstaMode)
            {
                return;
            }

            var useCount = __instance.instaButton.GetUseCount();
            var placed = GetTotalPlaced(__instance.instaModel.name);
            var cost = GetCostForThing(__instance.instaModel);
            if (!Warned && (placed >= useCount || cost > InGame.instance.GetCash()))
            {
                PopupScreen.instance.ShowPopup(PopupScreen.Placement.inGameCenter, "Real Insta Warning",
                    "You are placing an actual Insta Monkey, and doing so will remove it from your inventory. Are you sure you want to continue?",
                    new Action(() => Warned = true), "Yes",
                    new Action(__instance.CancelAllPlacementActions), "No", Popup.TransitionAnim.Scale
                );
            }

            InInstaMode = true;
            InstaModel = __instance.instaModel;
        }
    }

    [HarmonyPatch(typeof(InputManager), nameof(InputManager.TryPlace))]
    internal class InputManager_TryPlace
    {
        [HarmonyPrefix]
        internal static bool Prefix()
        {
            return !(PopupScreen.instance != null && PopupScreen.instance.IsPopupActive());
        }
    }

    [HarmonyPatch(typeof(InputManager), nameof(InputManager.ExitInstaMode))]
    internal class InputManager_ExitInstaMode
    {
        [HarmonyPostfix]
        internal static void Postfix(InputManager __instance)
        {
            InInstaMode = false;
        }
    }


    [HarmonyPatch(typeof(Btd6Player), nameof(Btd6Player.ConsumeInstaTower))]
    internal class Btd6Player_ConsumeInstaTower
    {
        [HarmonyPrefix]
        internal static bool Prefix()
        {
            if (ActuallyConsumeInsta)
            {
                ActuallyConsumeInsta = false;
                return true;
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(Tower), nameof(Tower.OnPlace))]
    internal static class Tower_OnPlace
    {
        [HarmonyPrefix]
        private static void Prefix(Tower __instance)
        {
            var tower = __instance;
            var def = __instance.towerModel;
            Warned = false;
            if (tower.worth == 0 &&
                def.name == InstaModel?.name &&
                (!InGame.instance.IsCoop || tower.owner == Game.instance.GetNkGI().PeerID))
            {
                var cost = GetCostForThing(tower);
                if (InGame.instance.GetCash() >= cost)
                {
                    cost = GetCostForThing(tower);
                    InGame.instance.AddCash(-cost);
                    tower.worth = cost;
                    SavedPlacedInstas[tower.Id.Id] = def.name;
                }
                else
                {
                    ActuallyConsumeInsta = true;
                    Game.instance.GetBtd6Player().ConsumeInstaTower(def.baseId, def.tiers);
                }

                InstaModel = null;
            }
        }
    }

    [HarmonyPatch(typeof(StandardInstaTowerButton), nameof(StandardInstaTowerButton.UpdateUseCount))]
    internal class StandardInstaTowerButton_UpdateUseCount
    {
        [HarmonyPostfix]
        internal static void Postfix(StandardInstaTowerButton __instance, int useCount)
        {
            var amountAvailable = useCount - GetTotalPlaced(__instance.instaTowerModel.name);
            __instance.powerCountText.SetText(amountAvailable + "/" + useCount);
        }
    }

    [HarmonyPatch(typeof(StandardInstaTowerButton), nameof(StandardInstaTowerButton.SetPower))]
    internal class StandardInstaTowerButton_SetPower
    {
        [HarmonyPostfix]
        internal static void Postfix(StandardInstaTowerButton __instance, PowerModel powerModel, bool isInsta)
        {
            var costText = __instance.GetComponentsInChildren<TextMeshProUGUI>()
                .FirstOrDefault(text => text.name == "Cost");

            float unit = __instance.tiers.fontSize / 3;
            if (costText == null)
            {
                costText = Object.Instantiate(__instance.tiers, __instance.tiers.transform.parent, true);
                costText.name = "Cost";
                costText.transform.Translate(0, unit, 0);
                costText.color = Color.red;
            }

            var cost = GetCostForThing(powerModel.tower);
            costText.SetText($"${cost:n0}");

            var tier = __instance.instaTowerModel.tier;
            var discountText = __instance.GetComponentsInChildren<TextMeshProUGUI>()
                .FirstOrDefault(text => text.name == "Discount");
            if (discountText == null)
            {
                discountText = Object.Instantiate(__instance.powerCountText,
                    __instance.powerCountText.transform.parent, true);
                discountText.name = "Discount";
                discountText.transform.Translate(unit * 3, 0, 0);
                discountText.color = Color.green;
            }

            if (tier > 0)
            {
                discountText.SetText("-" + tier * 5 + "%");
            }
            else
            {
                discountText.SetText("");
            }
        }
    }

    [HarmonyPatch(typeof(ItemPurchaseButton), nameof(ItemPurchaseButton.OnPointerClick))]
    internal class TowerPurchaseButton_OnPointerClick
    {
        [HarmonyPostfix]
        internal static void Postfix(ItemPurchaseButton __instance, PointerEventData eventData)
        {
            if (__instance.Is(out TowerPurchaseButton button) && 
                eventData.button == PointerEventData.InputButton.Right &&
                button.towerModel.IsBaseTower &&
                !button.IsHero &&
                InGameData.CurrentGame.ArePowersAllowed())
            {
                RightMenu.instance.ShowPowersMenu();
                PowersMenu.instance.ShowInstaMonkeys();
                InstaTowersMenu.instaTowersInstance.Show(button.towerModel);

                InGame.instance.InputManager.ExitTowerMode();
            }
        }
    }
}