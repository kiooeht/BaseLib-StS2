using System.Reflection.Emit;
using BaseLib.Abstracts;
using BaseLib.Utils;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace BaseLib.Patches.Features;

[HarmonyPatch]
public static class TwoAmountPowerPatch
{
    public static readonly SpireField<ITwoAmountPower, Action?> DisplayAmount2Changed = new(() => null);
        
    [HarmonyPatch(typeof(PowerModel))]
    static class Model
    {
        [HarmonyPatch(nameof(PowerModel.HoverTips), MethodType.Getter)]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> AddAmount2Var(IEnumerable<CodeInstruction> instructions)
        {
            var codeMatcher = new CodeMatcher(instructions);

            codeMatcher
                .MatchStartForward(
                    CodeMatch.LoadsConstant("Amount")
                )
                .InsertAndAdvance(
                    new CodeInstruction(OpCodes.Dup), // locString
                    CodeInstruction.LoadArgument(0), // this
                    CodeInstruction.Call(typeof(Model), nameof(AddAmount2ToLocString))
                );

            return codeMatcher.Instructions();
        }

        private static void AddAmount2ToLocString(LocString locString, PowerModel power)
        {
            if (power is ITwoAmountPower twoAmountPower) {
                locString.Add("Amount2", twoAmountPower.Amount2);
            }
        }
    }
    
    [HarmonyPatch(typeof(NPower))]
    static class UI
    {
        static readonly SpireField<NPower, Action?> OnDisplay2AmountChangedStore = new(() => null);
        
        [HarmonyPatch(nameof(NPower._Ready))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> AddSecondAmountLabel(IEnumerable<CodeInstruction> instructions)
        {
            var codeMatcher = new CodeMatcher(instructions);

            codeMatcher
                .MatchStartForward(
                    CodeMatch.Calls(typeof(NPower).Method("Reload"))
                )
                .ThrowIfInvalid("Failed to find this.Reload()")
                .InsertAndAdvance(
                    CodeInstruction.LoadArgument(0),
                    CodeInstruction.Call<NPower>(nPower => AddSecondAmountLabel(nPower))
                );

            return codeMatcher.Instructions();
        }
        
        private static void AddSecondAmountLabel(NPower __instance)
        {
            var amount1Label = __instance.GetNode<MegaLabel>("%AmountLabel");
            if (__instance.HasNode("Amount2Label")) return;
            var amount2Label = (MegaLabel)amount1Label.Duplicate();
            amount2Label.Name = "Amount2Label";
            amount2Label.UniqueNameInOwner = false;
            amount2Label.Visible = false;
            __instance.AddChild(amount2Label);
            __instance.MoveChild(amount2Label, amount1Label.GetIndex());
        }

        [HarmonyPatch("RefreshAmount")]
        [HarmonyPostfix]
        static void AlterAmount1DisplayString(NPower __instance, MegaLabel ____amountLabel)
        {
            if (__instance.Model is not IDisplayAmountStringPower amountStringPower) return;

            var text = amountStringPower.DisplayAmountString;
            if (text != null) {
                ____amountLabel.SetTextAutoSize(text);
            }
        }

        [HarmonyPatch("Reload")]
        [HarmonyPostfix]
        static void ReloadRefreshAmount2(NPower __instance)
        {
            if (!__instance.IsNodeReady()) return;
            if (__instance.Model is not ITwoAmountPower) return;
            
            OnDisplayAmount2Changed(__instance);
        }
        
        private static void OnDisplayAmount2Changed(NPower nPower)
        {
            var amount2Label = nPower.GetNode<MegaLabel>("Amount2Label");
            
            if (nPower.Model is not ITwoAmountPower twoAmountPower) {
                amount2Label.Visible = false;
                return;
            }

            amount2Label.Visible = true;
            amount2Label.AddThemeColorOverride(ThemeConstants.Label.FontColor, twoAmountPower.Amount2LabelColor);
            amount2Label.SetTextAutoSize(twoAmountPower.DisplayAmount2String);
            var fontSize = amount2Label.GetThemeFontSize(ThemeConstants.Label.FontSize);
            var amount1Label = (MegaLabel)typeof(NPower).DeclaredField("_amountLabel").GetValue(nPower)!;
            amount2Label.Position = amount1Label.Position + new Vector2(0, -(fontSize + 2));
        }

        [HarmonyPatch("SubscribeToModelEvents")]
        [HarmonyPostfix]
        static void SubscribeToAmount2Events(NPower __instance)
        {
            if (__instance.Model is ITwoAmountPower twoAmountPower) {
                var tmp = () => OnDisplayAmount2Changed(__instance);
                OnDisplay2AmountChangedStore[__instance] = tmp;
                twoAmountPower.DisplayAmount2Changed += tmp;
            }
        }
        
        [HarmonyPatch("UnsubscribeFromModelEvents")]
        [HarmonyPostfix]
        static void UnsubscribeToAmount2Events(NPower __instance)
        {
            if (__instance.Model is ITwoAmountPower twoAmountPower) {
                var tmp = OnDisplay2AmountChangedStore[__instance];
                OnDisplay2AmountChangedStore[__instance] = null;
                twoAmountPower.DisplayAmount2Changed -= tmp;
            }
        }
    }
}
