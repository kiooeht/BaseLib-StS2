using BaseLib.Patches.Features;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace BaseLib.Abstracts;

public interface ITwoAmountPower
{
    public int Amount2 { get; set; }

    public int DisplayAmount2 => Amount2;

    public string DisplayAmount2String => DisplayAmount2.ToString();

    public Color Amount2LabelColor {
        get {
            if (this is PowerModel power) {
                return power.AmountLabelColor;
            }

            return (Color) (AccessTools.DeclaredField(typeof(PowerModel), "_normalAmountLabelColor").GetValue(null) ?? StsColors.cream);
        }
    }

    public sealed Action? DisplayAmount2Changed {
        get => TwoAmountPowerPatch.DisplayAmount2Changed[this];
        set => TwoAmountPowerPatch.DisplayAmount2Changed[this] = value;
    }

    public void InvokeDisplayAmount2Changed()
    {
        DisplayAmount2Changed?.Invoke();
    }
}
