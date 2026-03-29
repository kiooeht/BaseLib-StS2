using System.Runtime.CompilerServices;
using BaseLib.Abstracts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace BaseLib.Commands;

public static class Amount2Cmd
{
    public static async Task<IEnumerable<T>> Apply2<T>(
        IEnumerable<Creature> targets,
        decimal amount,
        decimal amount2,
        Creature? applier,
        CardModel? cardSource,
        bool silent = false
    ) where T : PowerModel, ITwoAmountPower
    {
        var powers = new List<T>();
        foreach (var target in targets) {
            var p = await Apply2<T>(target, amount, amount2, applier, cardSource, silent);
            if (p != null) {
                powers.Add(p);
            }
        }

        return powers;
    }
    
    public static async Task<T?> Apply2<T>(
        Creature target,
        decimal amount,
        decimal amount2,
        Creature? applier,
        CardModel? cardSource,
        bool silent = false
    ) where T : PowerModel, ITwoAmountPower
    {
        if (CombatManager.Instance.IsEnding) {
            return null;
        }
        if (!target.CanReceivePowers) {
            return null;
        }

        var powerModel = ModelDb.Power<T>();
        PowerModel? power;
        if (powerModel.IsInstanced || !target.HasPower<T>()) {
            power = powerModel.ToMutable();
            await Apply2(power, target, amount, amount2, applier, cardSource, silent);
        }
        else {
            power = target.GetPower<T>();
            if (power == null) {
                throw new InvalidOperationException("Creature missing expected power.");
            }

            if (await PowerCmd.ModifyAmount(power, amount, applier, cardSource, silent) == 0) {
                power = null;
            }
        }

        return power as T;
    }

    public static async Task Apply2(
        PowerModel power,
        Creature target,
        decimal amount,
        decimal amount2,
        Creature? applier,
        CardModel? cardSource,
        bool silent = false
    )
    {
        Amount2CmdPatch.Amount2Store.Add(power, new Amount2CmdPatch.DecimalWrapper(amount2));
        await PowerCmd.Apply(power, target, amount, applier, cardSource, silent);
    }
}

[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.ApplyInternal))]
internal static class Amount2CmdPatch
{
    internal class DecimalWrapper
    {
        internal DecimalWrapper(decimal value) => Value = value;
        internal decimal Value { get; }
    }
    
    internal static ConditionalWeakTable<PowerModel, DecimalWrapper> Amount2Store = new();

    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> InsertSetAmount2(IEnumerable<CodeInstruction> instructions)
    {
        var codeMatcher = new CodeMatcher(instructions);

        codeMatcher
            .MatchStartForward(
                CodeMatch.Calls(typeof(PowerModel).Method(nameof(PowerModel.SetAmount)))
            )
            .ThrowIfInvalid("Failed to find this.SetAmount()")
            .InsertAfterAndAdvance(
                CodeInstruction.LoadArgument(0),
                CodeInstruction.LoadArgument(3),
                // ReSharper disable once ConvertClosureToMethodGroup
                CodeInstruction.Call((PowerModel powerModel, bool silent) => SetAmount2(powerModel, silent))
            );

        return codeMatcher.Instructions();
    }

    private static void SetAmount2(PowerModel powerModel, bool silent)
    {
        if (powerModel is not ITwoAmountPower twoAmountPower) return;

        if (Amount2Store.TryGetValue(powerModel, out var amount2Wrapper)) {
            Amount2Store.Remove(powerModel);
            // twoAmountPower.SetAmount2((int) amount2.Value);
            var amount2 = (int) amount2Wrapper.Value;
            
            powerModel.AssertMutable();
            amount2 = Math.Min(amount2, Really.bigNumber);
            amount2 = Math.Max(amount2, -Really.bigNumber);
            var change = amount2 - twoAmountPower.Amount2;
            if (change == 0) {
                return;
            }

            twoAmountPower.Amount2 = amount2;
            // twoAmountPower.SetAmount2Impl(amount2);
            twoAmountPower.InvokeDisplayAmount2Changed();
            powerModel.Owner.InvokePowerModified(powerModel, change, silent);
        }
    }
}
