using HarmonyLib;
using RoR2;
using RoR2.UI;
using UnityEngine;
using UnityEngine.Networking;

namespace EveryoneGetsOne.Patches
{
    /// <summary>
    /// On the host, the server-side object must keep its colliders so other
    /// players can still walk over it.  This blocks the host's own interaction
    /// prompt for a pickup it has already collected.
    /// </summary>
    [HarmonyPatch(typeof(GenericPickupController))]
    [HarmonyPatch(nameof(GenericPickupController.GetInteractability))]
    internal static class GenericPickupInteractabilityPatch
    {
        [HarmonyPostfix]
        private static void Postfix(
            GenericPickupController __instance,
            Interactor activator,
            ref Interactability __result)
        {
            if (!NetworkServer.active || activator == null)
            {
                return;
            }
            if (__result == Interactability.Disabled)
            {
                return;
            }

            CharacterBody body = activator.GetComponent<CharacterBody>();
            CharacterMaster master = body ? body.master : null;
            if (master != null && LootShare.IsPickupClaimed(__instance, master))
            {
                __result = Interactability.Disabled;
            }
        }
    }

    /// <summary>
    /// The Radar Scanner works through ChestRevealer.Pulse, which asks each
    /// interactable whether it should appear on the scanner.  A locally hidden
    /// pickup answers no, so each player only sees the drops they still need.
    /// </summary>
    [HarmonyPatch(typeof(GenericPickupController))]
    [HarmonyPatch(nameof(GenericPickupController.ShouldShowOnScanner))]
    internal static class GenericPickupShouldShowOnScannerPatch
    {
        [HarmonyPostfix]
        private static void Postfix(GenericPickupController __instance, ref bool __result)
        {
            if (!__result || !EveryoneGetsOnePlugin.HideClaimedPickups.Value)
            {
                return;
            }

            if (LocalHide.IsObjectHidden(__instance.gameObject))
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(PickupPickerController))]
    [HarmonyPatch(nameof(PickupPickerController.ShouldShowOnScanner))]
    internal static class PickerShouldShowOnScannerPatch
    {
        [HarmonyPostfix]
        private static void Postfix(PickupPickerController __instance, ref bool __result)
        {
            if (!__result || !EveryoneGetsOnePlugin.HideClaimedPickups.Value)
            {
                return;
            }

            if (LocalHide.IsObjectHidden(__instance.gameObject))
            {
                __result = false;
            }
        }
    }

    /// <summary>
    /// Position indicators (radar reveals and pings) are separate client-side
    /// objects.  Kill a new one immediately if its target is a pickup this
    /// client has already collected.
    /// </summary>
    [HarmonyPatch(typeof(PositionIndicator))]
    [HarmonyPatch("Start")]
    internal static class PositionIndicatorStartPatch
    {
        [HarmonyPostfix]
        private static void Postfix(PositionIndicator __instance)
        {
            if (!EveryoneGetsOnePlugin.HideClaimedPickups.Value)
            {
                return;
            }

            Transform target = __instance.targetTransform;
            if (target == null)
            {
                return;
            }

            LocalHide hide = target.GetComponentInParent<LocalHide>();
            if (hide != null && hide.IsHidden)
            {
                PingIndicator ping = __instance.GetComponentInParent<PingIndicator>();
                if (ping != null)
                {
                    Object.Destroy(ping.gameObject);
                }
                else
                {
                    Object.Destroy(__instance.gameObject);
                }
            }
        }
    }

    /// <summary>
    /// A ping on a pickup this player has already collected would otherwise
    /// leave its world-space label behind even after the map icon is removed.
    /// </summary>
    [HarmonyPatch(typeof(PingIndicator))]
    [HarmonyPatch(nameof(PingIndicator.RebuildPing))]
    internal static class PingIndicatorRebuildPatch
    {
        [HarmonyPostfix]
        private static void Postfix(PingIndicator __instance)
        {
            if (!EveryoneGetsOnePlugin.HideClaimedPickups.Value)
            {
                return;
            }

            GameObject target = __instance.pingTarget;
            if (target == null)
            {
                return;
            }

            LocalHide hide = target.transform.GetComponentInParent<LocalHide>();
            if (hide != null && hide.IsHidden)
            {
                Object.Destroy(__instance.gameObject);
            }
        }
    }
}
