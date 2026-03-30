using BaseLib.Commands;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace BaseLib.Hooks;

public interface IModifyPowerAmount2
{
    bool TryModifyPowerAmount2Given(
        PowerModel power,
        Creature giver,
        decimal amount2,
        Creature? target,
        CardModel? cardSource,
        out decimal modifiedAmount2
    )
    {
        modifiedAmount2 = amount2;
        return false;
    }

    bool TryModifyPowerAmount2Received(
        PowerModel power,
        Creature target,
        decimal amount2,
        Creature? giver,
        out decimal modifiedAmount2
    )
    {
        modifiedAmount2 = amount2;
        return false;
    }

    Task AfterModifyingPowerAmount2Given(
        PowerModel power
    ) => Task.CompletedTask;

    Task AfterModifyingPowerAmount2Received(
        PowerModel power
    ) => Task.CompletedTask;
}

[HarmonyPatch]
internal static class ModifyPowerAmount2Patch
{
    [HarmonyPatch(typeof(Hook), nameof(Hook.ModifyPowerAmountGiven))]
    [HarmonyPostfix]
    static void HookModifyPowerAmount2Given(
        CombatState combatState,
        PowerModel power,
        Creature giver,
        Creature? target,
        CardModel? cardSource
    )
    {
        if (!Amount2CmdPatch.Amount2Store.TryGetValue(power, out var amount2Wrapper)) return;

        List<AbstractModel> modelList = [];
        var num = amount2Wrapper.Value;
        foreach (var iterateHookListener in combatState.IterateHookListeners()) {
            if (iterateHookListener is not IModifyPowerAmount2 modifyPowerAmount2) continue;
            
            if (modifyPowerAmount2.TryModifyPowerAmount2Given(power, giver, num, target, cardSource, out var modifiedAmount2)) {
                num = modifiedAmount2;
                modelList.Add(iterateHookListener);
            }
        }

        amount2Wrapper.Value = num;
        amount2Wrapper.GivenModifiers = modelList;
        Amount2CmdPatch.Amount2Store.AddOrUpdate(power, amount2Wrapper);
    }
    
    [HarmonyPatch(typeof(Hook), nameof(Hook.ModifyPowerAmountReceived))]
    [HarmonyPostfix]
    static void HookModifyPowerAmount2Received(
        CombatState combatState,
        [HarmonyArgument("canonicalPower")] PowerModel power,
        Creature target,
        Creature? giver
    )
    {
        if (!Amount2CmdPatch.Amount2Store.TryGetValue(power, out var amount2Wrapper)) return;

        List<AbstractModel> modelList = [];
        var num = amount2Wrapper.Value;
        foreach (var iterateHookListener in combatState.IterateHookListeners()) {
            if (iterateHookListener is not IModifyPowerAmount2 modifyPowerAmount2) continue;
            
            if (modifyPowerAmount2.TryModifyPowerAmount2Received(power, target, num, giver, out var modifiedAmount2)) {
                num = modifiedAmount2;
                modelList.Add(iterateHookListener);
            }
        }

        amount2Wrapper.Value = num;
        amount2Wrapper.ReceivedModifiers = modelList;
        Amount2CmdPatch.Amount2Store.AddOrUpdate(power, amount2Wrapper);
    }

    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterModifyingPowerAmountGiven))]
    [HarmonyPostfix]
    static void HookAfterModifyingPowerAmount2Given(
        CombatState combatState,
        PowerModel modifiedPower
    )
    {
        HookAfterModifyingPowerAmount2GivenImpl(combatState, modifiedPower).Wait();
    }

    private static async Task HookAfterModifyingPowerAmount2GivenImpl(
        CombatState combatState,
        PowerModel modifiedPower
    )
    {
        if (!Amount2CmdPatch.Amount2Store.TryGetValue(modifiedPower, out var amount2Wrapper)) return;
        
        foreach (var iterateHookListener in combatState.IterateHookListeners()) {
            if (iterateHookListener is not IModifyPowerAmount2 modifyPowerAmount2) continue;
            
            if (amount2Wrapper.GivenModifiers?.Contains(iterateHookListener) == true) {
                await modifyPowerAmount2.AfterModifyingPowerAmount2Given(modifiedPower);
                iterateHookListener.InvokeExecutionFinished();
            }
        }
    }

    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterModifyingPowerAmountReceived))]
    [HarmonyPostfix]
    static void HookAfterModifyingPowerAmount2Received(
        CombatState combatState,
        PowerModel modifiedPower
    )
    {
        HookAfterModifyingPowerAmount2ReceivedImpl(combatState, modifiedPower).Wait();
    }

    private static async Task HookAfterModifyingPowerAmount2ReceivedImpl(
        CombatState combatState,
        PowerModel modifiedPower
    )
    {
        if (!Amount2CmdPatch.Amount2Store.TryGetValue(modifiedPower, out var amount2Wrapper)) return;
        
        foreach (var iterateHookListener in combatState.IterateHookListeners()) {
            if (iterateHookListener is not IModifyPowerAmount2 modifyPowerAmount2) continue;
            
            if (amount2Wrapper.ReceivedModifiers?.Contains(iterateHookListener) == true) {
                await modifyPowerAmount2.AfterModifyingPowerAmount2Received(modifiedPower);
                iterateHookListener.InvokeExecutionFinished();
            }
        }

        Amount2CmdPatch.Amount2Store.Remove(modifiedPower);
    }
}
