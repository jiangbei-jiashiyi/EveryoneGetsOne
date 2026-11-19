using System;
using HarmonyLib;
using RoR2;
using UnityEngine.Networking;

namespace EveryoneGetsOne.Patches
{
    /// <summary>
    /// With Artifact of Command, the first player to choose normally consumes
    /// the cube and spawns an item pickup.  Instead, grant the chosen item
    /// directly to the player who is currently using the cube, keep the cube
    /// alive, and let every other player choose once.  Destroy the cube after
    /// the last player has chosen.
    /// </summary>
    [HarmonyPatch(typeof(PickupPickerController))]
    [HarmonyPatch("HandlePickupSelected", new Type[] { typeof(int) })]
    internal static class CommandCubeHandleSelectedPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(PickupPickerController __instance, int choiceIndex)
        {
            if (!NetworkServer.active)
            {
                return true;
            }
            if (!EveryoneGetsOnePlugin.Enable.Value ||
                !EveryoneGetsOnePlugin.ShareItems.Value ||
                !EveryoneGetsOnePlugin.ShareCommandPicks.Value)
            {
                return true;
            }
            if (!LootShare.IsCommandCube(__instance))
            {
                return true;
            }

            NetworkUIPromptController prompt = __instance.GetComponent<NetworkUIPromptController>();
            if (!prompt)
            {
                return true;
            }

            CharacterMaster master = prompt.currentParticipantMaster;
            if (!master)
            {
                return true;
            }

            LootShare.CubeClaim claim = LootShare.GetCubeClaim(__instance);
            if (claim.Claimed.Contains(master))
            {
                // Re-opened by a player who already picked (usually a client
                // without the mod).  Close the window without granting again.
                prompt.SetParticipantMaster(null);
                return false;
            }

            if (__instance.options == null || choiceIndex < 0 || choiceIndex >= __instance.options.Length)
            {
                return true;
            }

            PickupPickerController.Option option = __instance.options[choiceIndex];
            if (!option.available)
            {
                return true;
            }

            PickupDef pickupDef = PickupCatalog.GetPickupDef(option.pickup.pickupIndex);
            if (pickupDef == null || pickupDef.itemIndex == ItemIndex.None)
            {
                // Equipment has one slot per player; leave it vanilla.
                return true;
            }

            if (!LootShare.IsShareableItem(ItemCatalog.GetItemDef(pickupDef.itemIndex)))
            {
                return true;
            }

            if (!LootShare.TryGrantItem(
                    master,
                    pickupDef,
                    option.pickup,
                    __instance.transform.position,
                    __instance.transform.rotation))
            {
                // Fall back to vanilla instead of losing the item.
                return true;
            }

            claim.Claimed.Add(master);
            prompt.SetParticipantMaster(null);

            LootShare.CollectCollectors(LootShare.Collectors);
            LootShare.Announce(master, pickupDef, claim.Claimed, LootShare.Collectors, "选择了");
            EveryoneGetsOnePlugin.Log.LogInfo(
                "Command pick " + (pickupDef.internalName ?? "?") +
                " by " + (Util.GetBestMasterName(master) ?? "???") +
                "; collectors=" + LootShare.Collectors.Count +
                ", claimed=" + claim.Claimed.Count);

            if (LootShare.AllCollectorsClaimed(claim.Claimed))
            {
                LootShare.DestroyCube(__instance);
            }
            else
            {
                LootShare.NotifyClaimed(__instance.gameObject, master);
            }

            return false;
        }
    }

    /// <summary>
    /// Hide a Command cube from players who are done with it.  Remote clients
    /// only know about their own pick, so local state is enough; the host can
    /// additionally consult the authoritative claim table.
    /// </summary>
    [HarmonyPatch(typeof(PickupPickerController))]
    [HarmonyPatch(nameof(PickupPickerController.GetInteractability))]
    internal static class CommandCubeInteractabilityPatch
    {
        [HarmonyPostfix]
        private static void Postfix(
            PickupPickerController __instance,
            Interactor activator,
            ref Interactability __result)
        {
            if (!EveryoneGetsOnePlugin.Enable.Value ||
                !EveryoneGetsOnePlugin.ShareItems.Value ||
                !EveryoneGetsOnePlugin.ShareCommandPicks.Value)
            {
                return;
            }
            if (__result == Interactability.Disabled)
            {
                return;
            }
            if (!LootShare.IsCommandCube(__instance))
            {
                return;
            }

            // On a listen server GetInteractability is also evaluated for
            // remote players.  LocalCubePicks only describes the host's own
            // pick, so it must never be used there.  The claim table is
            // authoritative for every player on the server.
            if (NetworkServer.active)
            {
                if (activator == null)
                {
                    return;
                }

                CharacterBody body = activator.GetComponent<CharacterBody>();
                CharacterMaster master = body ? body.master : null;
                if (master != null && LootShare.IsCubeClaimed(__instance, master))
                {
                    __result = Interactability.Disabled;
                }
                return;
            }

            // Pure client: the server has not told us about other players'
            // picks, but we know our own.
            if (LootShare.IsLocalCubePicked(__instance))
            {
                __result = Interactability.Disabled;
            }
        }
    }

    /// <summary>
    /// Record the local player's pick as soon as they click an option.  This is
    /// what makes the cube stop prompting that player before the server has
    /// finished processing the message.
    /// </summary>
    [HarmonyPatch(typeof(PickupPickerController))]
    [HarmonyPatch(nameof(PickupPickerController.SubmitChoice))]
    internal static class CommandCubeSubmitChoicePatch
    {
        [HarmonyPostfix]
        private static void Postfix(PickupPickerController __instance, int choiceIndex)
        {
            if (!EveryoneGetsOnePlugin.Enable.Value ||
                !EveryoneGetsOnePlugin.ShareItems.Value ||
                !EveryoneGetsOnePlugin.ShareCommandPicks.Value)
            {
                return;
            }
            if (!LootShare.IsCommandCube(__instance) || !__instance.available)
            {
                return;
            }
            if (__instance.options == null || choiceIndex < 0 || choiceIndex >= __instance.options.Length)
            {
                return;
            }
            if (!__instance.options[choiceIndex].available)
            {
                return;
            }

            LootShare.MarkLocalCubePicked(__instance);
            if (EveryoneGetsOnePlugin.HideClaimedPickups.Value)
            {
                try
                {
                    LocalHide.Hide(__instance.gameObject);
                }
                catch (Exception ex)
                {
                    if (EveryoneGetsOnePlugin.VerboseLog.Value)
                    {
                        EveryoneGetsOnePlugin.Log.LogWarning("Local command hide failed: " + ex);
                    }
                }
            }
        }
    }

    /// <summary>
    /// On the host, refuse to open a Command cube the host has already used.
    /// </summary>
    [HarmonyPatch(typeof(PickupPickerController))]
    [HarmonyPatch(nameof(PickupPickerController.OnInteractionBegin))]
    internal static class CommandCubeInteractionBeginPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(PickupPickerController __instance, Interactor activator)
        {
            if (!NetworkServer.active)
            {
                return true;
            }
            if (!EveryoneGetsOnePlugin.Enable.Value ||
                !EveryoneGetsOnePlugin.ShareItems.Value ||
                !EveryoneGetsOnePlugin.ShareCommandPicks.Value)
            {
                return true;
            }
            if (!LootShare.IsCommandCube(__instance) || activator == null)
            {
                return true;
            }

            CharacterBody body = activator.GetComponent<CharacterBody>();
            CharacterMaster master = body ? body.master : null;
            if (master != null && LootShare.IsCubeClaimed(__instance, master))
            {
                return false;
            }

            return true;
        }
    }
}
