using System;
using HarmonyLib;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace EveryoneGetsOne.Patches
{
    /// <summary>
    /// Replaces GenericPickupController.AttemptGrant for item pickups.
    ///
    /// The vanilla method grants the item and destroys the pickup.  For a
    /// shared pickup we grant the item to the touching player but keep the
    /// pickup alive until every current player has claimed it.  The last
    /// player goes through the vanilla method so the pickup despawns normally.
    /// </summary>
    [HarmonyPatch(typeof(GenericPickupController))]
    [HarmonyPatch("AttemptGrant", new Type[] { typeof(CharacterBody) })]
    internal static class GenericPickupAttemptGrantPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(GenericPickupController __instance, CharacterBody body)
        {
            if (!NetworkServer.active)
            {
                return true;
            }
            if (!EveryoneGetsOnePlugin.Enable.Value || !EveryoneGetsOnePlugin.ShareItems.Value)
            {
                return true;
            }
            if (body == null || body.master == null || body.master.inventory == null)
            {
                return true;
            }
            if (body.teamComponent == null || body.teamComponent.teamIndex != TeamIndex.Player)
            {
                return true;
            }
            if (body.master.playerCharacterMasterController == null)
            {
                return true;
            }

            PickupDef pickupDef = PickupCatalog.GetPickupDef(__instance.pickup.pickupIndex);
            if (pickupDef == null || pickupDef.itemIndex == ItemIndex.None)
            {
                return true;
            }

            if (!LootShare.IsShareableItem(ItemCatalog.GetItemDef(pickupDef.itemIndex)))
            {
                return true;
            }

            LootShare.PickupClaim claim = LootShare.GetPickupClaim(__instance);
            CharacterMaster master = body.master;
            if (claim.Claimed.Contains(master))
            {
                // Already collected by this player.  Keep standing on the orb
                // from doing anything.
                return false;
            }

            if (EveryoneGetsOnePlugin.ShareToDead.Value)
            {
                LootShare.GrantToDeadPlayers(pickupDef, __instance.pickup, claim);
            }

            claim.Claimed.Add(master);
            LootShare.CollectCollectors(LootShare.Collectors);
            LootShare.Announce(master, pickupDef, claim.Claimed, LootShare.Collectors, "领取了");

            if (LootShare.AllCollectorsClaimed(claim.Claimed))
            {
                // Last collector: let the vanilla method grant and destroy.
                return true;
            }

            Vector3 position = __instance.pickupDisplay
                ? __instance.pickupDisplay.transform.position
                : __instance.transform.position;
            Quaternion rotation = __instance.pickupDisplay
                ? __instance.pickupDisplay.transform.rotation
                : Quaternion.identity;

            if (!LootShare.TryGrantItem(master, pickupDef, __instance.pickup, position, rotation))
            {
                // Fall back to vanilla rather than swallowing the pickup.
                return true;
            }

            LootShare.NotifyClaimed(__instance.gameObject, master);
            return false;
        }
    }
}
