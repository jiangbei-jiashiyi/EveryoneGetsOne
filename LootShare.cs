using System;
using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace EveryoneGetsOne
{
    internal static class LootShare
    {
        internal sealed class PickupClaim
        {
            internal readonly GenericPickupController Pickup;
            internal readonly HashSet<CharacterMaster> Claimed = new HashSet<CharacterMaster>();

            internal PickupClaim(GenericPickupController pickup)
            {
                Pickup = pickup;
            }
        }

        internal sealed class CubeClaim
        {
            internal readonly PickupPickerController Cube;
            internal readonly HashSet<CharacterMaster> Claimed = new HashSet<CharacterMaster>();

            internal CubeClaim(PickupPickerController cube)
            {
                Cube = cube;
            }
        }

        internal static readonly List<CharacterMaster> Collectors = new List<CharacterMaster>();

        private static readonly Dictionary<int, PickupClaim> PickupClaims =
            new Dictionary<int, PickupClaim>();

        private static readonly Dictionary<int, CubeClaim> CubeClaims =
            new Dictionary<int, CubeClaim>();

        private static readonly HashSet<uint> LocalCubePicks = new HashSet<uint>();
        private static readonly List<CharacterMaster> AllPlayers = new List<CharacterMaster>();
        private static readonly List<int> RemoveKeys = new List<int>();
        private static float nextCleanupTime;

        internal static void ResetForStage()
        {
            PickupClaims.Clear();
            CubeClaims.Clear();
            LocalCubePicks.Clear();
            Collectors.Clear();
            AllPlayers.Clear();
            nextCleanupTime = 0f;
        }

        /// <summary>
        /// Command cubes are the only PickupPickerControllers created by
        /// PickupDropletController.CreateCommandCube.  On the server the
        /// pickupThatGeneratedOptions field is set; on clients only the prefab
        /// name / context string are available.
        /// </summary>
        internal static bool IsCommandCube(PickupPickerController picker)
        {
            if (!picker)
            {
                return false;
            }

            if (picker.pickupThatGeneratedOptions.HasValue)
            {
                return true;
            }

            string objectName = picker.gameObject.name;
            if (!string.IsNullOrEmpty(objectName) &&
                objectName.StartsWith("CommandCube", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return picker.contextString == "ARTIFACT_COMMAND_CUBE_INTERACTION_PROMPT";
        }

        internal static bool IsShareableItem(ItemDef itemDef)
        {
            if (!EveryoneGetsOnePlugin.Enable.Value || !EveryoneGetsOnePlugin.ShareItems.Value)
            {
                return false;
            }

            if (itemDef == null)
            {
                return false;
            }

            switch (itemDef.tier)
            {
                case ItemTier.Lunar:
                    return EveryoneGetsOnePlugin.ShareLunarItems.Value;
                case ItemTier.Boss:
                    return EveryoneGetsOnePlugin.ShareBossItems.Value;
                case ItemTier.VoidTier1:
                case ItemTier.VoidTier2:
                case ItemTier.VoidTier3:
                case ItemTier.VoidBoss:
                    return EveryoneGetsOnePlugin.ShareVoidItems.Value;
                default:
                    return true;
            }
        }

        internal static PickupClaim GetPickupClaim(GenericPickupController pickup)
        {
            int key = pickup.GetInstanceID();
            PickupClaim claim;
            if (!PickupClaims.TryGetValue(key, out claim) || claim.Pickup != pickup)
            {
                claim = new PickupClaim(pickup);
                PickupClaims[key] = claim;
            }
            return claim;
        }

        internal static CubeClaim GetCubeClaim(PickupPickerController cube)
        {
            int key = cube.GetInstanceID();
            CubeClaim claim;
            if (!CubeClaims.TryGetValue(key, out claim) || claim.Cube != cube)
            {
                claim = new CubeClaim(cube);
                CubeClaims[key] = claim;
            }
            return claim;
        }

        internal static void CollectCollectors(List<CharacterMaster> into)
        {
            into.Clear();
            foreach (PlayerCharacterMasterController player in PlayerCharacterMasterController.instances)
            {
                if (!player)
                {
                    continue;
                }

                CharacterMaster master = player.master;
                if (!master || !master.inventory)
                {
                    continue;
                }

                // Permanently dead players cannot walk over a drop and cannot
                // use a Command cube.  They must not block it forever.
                if (master.IsDeadAndOutOfLivesServer())
                {
                    continue;
                }

                into.Add(master);
            }
        }

        private static void CollectAllPlayers(List<CharacterMaster> into)
        {
            into.Clear();
            foreach (PlayerCharacterMasterController player in PlayerCharacterMasterController.instances)
            {
                if (!player)
                {
                    continue;
                }

                CharacterMaster master = player.master;
                if (master && master.inventory)
                {
                    into.Add(master);
                }
            }
        }

        internal static bool AllCollectorsClaimed(HashSet<CharacterMaster> claimed)
        {
            CollectCollectors(Collectors);
            if (Collectors.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < Collectors.Count; i++)
            {
                if (!claimed.Contains(Collectors[i]))
                {
                    return false;
                }
            }
            return true;
        }

        internal static void GrantToDeadPlayers(PickupDef pickupDef, UniquePickup pickup, PickupClaim claim)
        {
            CollectAllPlayers(AllPlayers);
            for (int i = 0; i < AllPlayers.Count; i++)
            {
                CharacterMaster master = AllPlayers[i];
                if (!master || claim.Claimed.Contains(master))
                {
                    continue;
                }

                if (!master.IsDeadAndOutOfLivesServer())
                {
                    continue;
                }

                if (TryGrantItem(master, pickupDef, pickup, Vector3.zero, Quaternion.identity))
                {
                    claim.Claimed.Add(master);
                    NotifyClaimed(claim.Pickup.gameObject, master);
                }
            }
        }

        /// <summary>
        /// Grants one copy of an item to a player without touching the pickup
        /// object it came from.  Uses the game's own PickupDef.attemptGrant so
        /// item-specific behaviour (duplicator, mutual exclusivity, lunar
        /// corruption, temp items, etc.) still runs.
        /// </summary>
        internal static bool TryGrantItem(
            CharacterMaster master,
            PickupDef pickupDef,
            UniquePickup pickup,
            Vector3 position,
            Quaternion rotation)
        {
            if (!NetworkServer.active || !master || pickupDef == null)
            {
                return false;
            }

            CharacterBody body = master.GetBody();
            if (body && master.IsDeadAndOutOfLivesServer())
            {
                // Do not play pickup effects on a corpse; go through the
                // inventory-only path below.
                body = null;
            }

            if (body && body.inventory)
            {
                PickupDef.GrantContext context = default(PickupDef.GrantContext);
                context.body = body;
                context.controller = null;
                context.uniquePickup = pickup;
                context.position = position;
                context.rotation = rotation;

                bool granted;
                if (pickupDef.attemptGrant != null)
                {
                    pickupDef.attemptGrant(ref context);
                    granted = true;
                }
                else
                {
                    granted = GiveFallback(body.inventory, pickupDef, pickup);
                    context.shouldNotify = granted;
                }

                if (!granted)
                {
                    return false;
                }

                if (body)
                {
                    body.OnPickup(context.pickup.isTempItem
                        ? CharacterBody.PickupClass.TempItem
                        : CharacterBody.PickupClass.Item);
                }

                if (context.shouldNotify)
                {
                    GenericPickupController.SendPickupMessage(master, context.pickup);
                }

                if (EveryoneGetsOnePlugin.VerboseLog.Value)
                {
                    EveryoneGetsOnePlugin.Log.LogInfo(
                        "Granted " + (pickupDef.internalName ?? "?") + " to " + Util.GetBestMasterName(master));
                }
                return true;
            }

            if (!master.inventory)
            {
                return false;
            }

            if (!GiveFallback(master.inventory, pickupDef, pickup))
            {
                return false;
            }

            GenericPickupController.SendPickupMessage(master, pickup);
            return true;
        }

        private static bool GiveFallback(Inventory inventory, PickupDef pickupDef, UniquePickup pickup)
        {
            if (!inventory || pickupDef == null || pickupDef.itemIndex == ItemIndex.None)
            {
                return false;
            }

            if (pickup.isRental)
            {
                inventory.GiveItemRented(pickupDef.itemIndex, 1);
            }
            else if (pickup.isTempItem)
            {
                inventory.GiveItemTemp(pickupDef.itemIndex, pickup.decayValue);
            }
            else
            {
                inventory.GiveItemPermanent(pickupDef.itemIndex, 1);
            }
            return true;
        }

        internal static void Announce(
            CharacterMaster master,
            PickupDef pickupDef,
            HashSet<CharacterMaster> claimed,
            List<CharacterMaster> collectors,
            string verb)
        {
            if (!EveryoneGetsOnePlugin.AnnounceProgress.Value || !NetworkServer.active)
            {
                return;
            }
            if (pickupDef == null || collectors == null || collectors.Count == 0)
            {
                return;
            }

            int done = 0;
            for (int i = 0; i < collectors.Count; i++)
            {
                if (claimed.Contains(collectors[i]))
                {
                    done++;
                }
            }

            string playerName = Util.GetBestMasterName(master);
            if (string.IsNullOrEmpty(playerName))
            {
                playerName = "???";
            }

            string itemName = string.IsNullOrEmpty(pickupDef.nameToken)
                ? "???"
                : Language.GetString(pickupDef.nameToken);
            if (string.IsNullOrEmpty(itemName))
            {
                itemName = "???";
            }

            Chat.SendBroadcastChat(new Chat.SimpleChatMessage
            {
                baseToken = "<style=cIsUtility>[全员拾取] {0} " + verb + " {1}（{2}/{3}）</style>",
                paramTokens = new string[]
                {
                    playerName,
                    itemName,
                    done.ToString(),
                    collectors.Count.ToString()
                }
            });
        }

        internal static void DestroyCube(PickupPickerController cube)
        {
            if (!cube)
            {
                return;
            }

            CubeClaims.Remove(cube.GetInstanceID());
            if (NetworkServer.active)
            {
                NetworkServer.Destroy(cube.gameObject);
            }
        }

        internal static bool IsLocalCubePicked(PickupPickerController cube)
        {
            return cube && LocalCubePicks.Contains(cube.netId.Value);
        }

        internal static void MarkLocalCubePicked(PickupPickerController cube)
        {
            if (cube)
            {
                LocalCubePicks.Add(cube.netId.Value);
            }
        }

        internal static bool IsCubeClaimed(PickupPickerController cube, CharacterMaster master)
        {
            if (!cube || !master)
            {
                return false;
            }

            CubeClaim claim;
            return CubeClaims.TryGetValue(cube.GetInstanceID(), out claim)
                && claim.Cube == cube
                && claim.Claimed.Contains(master);
        }

        internal static bool IsPickupClaimed(GenericPickupController pickup, CharacterMaster master)
        {
            if (!pickup || !master)
            {
                return false;
            }

            PickupClaim claim;
            return PickupClaims.TryGetValue(pickup.GetInstanceID(), out claim)
                && claim.Pickup == pickup
                && claim.Claimed.Contains(master);
        }

        /// <summary>
        /// Tells the claiming player's client to hide this pickup locally.
        /// The host hides it immediately; remote clients get a message only if
        /// they announced this mod during the connection handshake.
        /// </summary>
        internal static void NotifyClaimed(GameObject pickupObject, CharacterMaster master)
        {
            if (!EveryoneGetsOnePlugin.Enable.Value ||
                !EveryoneGetsOnePlugin.HideClaimedPickups.Value)
            {
                return;
            }
            if (!NetworkServer.active || !pickupObject || !master)
            {
                return;
            }

            NetworkIdentity identity = pickupObject.GetComponent<NetworkIdentity>();
            if (identity == null || identity.netId.IsEmpty())
            {
                return;
            }

            LocalUser localUser = LocalUserManager.GetFirstLocalUser();
            if (localUser != null && localUser.cachedMaster == master)
            {
                LocalHide.Hide(pickupObject);
                return;
            }

            PlayerCharacterMasterController controller = master.playerCharacterMasterController;
            NetworkUser user = controller ? controller.networkUser : null;
            NetworkConnection connection = user ? user.connectionToClient : null;
            if (connection == null || !connection.isConnected)
            {
                return;
            }
            if (!NetworkCompat.ModdedConnections.Contains(connection))
            {
                return;
            }

            connection.Send(
                HidePickupMessage.MsgType,
                new HidePickupMessage(identity.netId.Value));
        }

        /// <summary>
        /// Removes state for destroyed objects and finishes Command cubes whose
        /// remaining players have left the game.  Runs only on the host.
        /// </summary>
        internal static void TickCleanup()
        {
            if (!NetworkServer.active)
            {
                return;
            }

            if (Time.unscaledTime < nextCleanupTime)
            {
                return;
            }
            nextCleanupTime = Time.unscaledTime + 2f;

            RemoveKeys.Clear();
            foreach (KeyValuePair<int, PickupClaim> pair in PickupClaims)
            {
                if (!pair.Value.Pickup)
                {
                    RemoveKeys.Add(pair.Key);
                }
            }
            for (int i = 0; i < RemoveKeys.Count; i++)
            {
                PickupClaims.Remove(RemoveKeys[i]);
            }

            RemoveKeys.Clear();
            foreach (KeyValuePair<int, CubeClaim> pair in CubeClaims)
            {
                CubeClaim claim = pair.Value;
                if (!claim.Cube)
                {
                    RemoveKeys.Add(pair.Key);
                    continue;
                }

                NetworkUIPromptController prompt = claim.Cube.GetComponent<NetworkUIPromptController>();
                if (prompt && prompt.inUse)
                {
                    continue;
                }

                if (AllCollectorsClaimed(claim.Claimed))
                {
                    RemoveKeys.Add(pair.Key);
                }
            }

            for (int i = 0; i < RemoveKeys.Count; i++)
            {
                int key = RemoveKeys[i];
                CubeClaim claim;
                if (!CubeClaims.TryGetValue(key, out claim))
                {
                    continue;
                }

                CubeClaims.Remove(key);
                if (claim.Cube)
                {
                    NetworkServer.Destroy(claim.Cube.gameObject);
                }
            }
        }
    }
}
