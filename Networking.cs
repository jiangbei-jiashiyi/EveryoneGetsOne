using System;
using System.Collections.Generic;
using RoR2;
using RoR2.Networking;
using UnityEngine;
using UnityEngine.Networking;

namespace EveryoneGetsOne
{
    /// <summary>
    /// Sent once per client connection.  The server only sends per-player hide
    /// messages to connections that announced this mod, so vanilla clients in
    /// the lobby never receive unknown message types.
    /// </summary>
    public sealed class PresenceMessage : MessageBase
    {
        public const short MsgType = 17320;
        public const int ProtocolVersion = 1;

        public int version;

        public PresenceMessage()
        {
        }

        public PresenceMessage(int version)
        {
            this.version = version;
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(version);
        }

        public override void Deserialize(NetworkReader reader)
        {
            version = reader.ReadInt32();
        }
    }

    /// <summary>
    /// Tells one client to hide one pickup locally.  The server object is not
    /// touched, so every other player keeps seeing it.
    /// </summary>
    public sealed class HidePickupMessage : MessageBase
    {
        public const short MsgType = 17321;

        public uint netId;

        public HidePickupMessage()
        {
        }

        public HidePickupMessage(uint netId)
        {
            this.netId = netId;
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(netId);
        }

        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadUInt32();
        }

        public static void Handle(NetworkMessage netMessage)
        {
            if (!EveryoneGetsOnePlugin.HideClaimedPickups.Value)
            {
                return;
            }

            try
            {
                HidePickupMessage message = netMessage.ReadMessage<HidePickupMessage>();
                if (message == null)
                {
                    return;
                }

                GameObject obj = ClientScene.FindLocalObject(new NetworkInstanceId(message.netId));
                if (obj)
                {
                    LocalHide.Hide(obj);
                }
            }
            catch (Exception ex)
            {
                if (EveryoneGetsOnePlugin.VerboseLog.Value)
                {
                    EveryoneGetsOnePlugin.Log.LogWarning("Failed to handle hide message: " + ex);
                }
            }
        }
    }

    internal static class NetworkCompat
    {
        internal static readonly HashSet<NetworkConnection> ModdedConnections =
            new HashSet<NetworkConnection>();

        private static bool serverHandlerRegistered;
        private static bool serverHandlerErrorLogged;
        private static bool helloPending;

        internal static void Initialize()
        {
            NetworkManagerSystem.onClientConnectGlobal += OnClientConnect;
        }

        internal static void Shutdown()
        {
            NetworkManagerSystem.onClientConnectGlobal -= OnClientConnect;
        }

        internal static void Update()
        {
            if (NetworkServer.active)
            {
                RegisterServerHandler();
            }
            else
            {
                serverHandlerRegistered = false;
                serverHandlerErrorLogged = false;
                if (ModdedConnections.Count > 0)
                {
                    ModdedConnections.Clear();
                }
            }

            if (helloPending)
            {
                TrySendHello();
            }
        }

        internal static void RegisterServerHandler()
        {
            if (!NetworkServer.active || serverHandlerRegistered)
            {
                return;
            }

            try
            {
                NetworkServer.RegisterHandler(PresenceMessage.MsgType, HandlePresence);
                serverHandlerRegistered = true;
                serverHandlerErrorLogged = false;
            }
            catch (Exception ex)
            {
                if (!serverHandlerErrorLogged)
                {
                    EveryoneGetsOnePlugin.Log.LogWarning(
                        "Failed to register presence handler: " + ex);
                    serverHandlerErrorLogged = true;
                }
            }
        }

        private static void OnClientConnect(NetworkConnection connection)
        {
            NetworkManagerSystem manager = NetworkManagerSystem.singleton;
            if (manager == null || manager.client == null)
            {
                return;
            }

            try
            {
                manager.client.RegisterHandler(HidePickupMessage.MsgType, HidePickupMessage.Handle);
            }
            catch (Exception ex)
            {
                if (EveryoneGetsOnePlugin.VerboseLog.Value)
                {
                    EveryoneGetsOnePlugin.Log.LogWarning("Failed to register hide handler: " + ex);
                }
            }

            helloPending = true;
            TrySendHello();
        }

        private static void TrySendHello()
        {
            NetworkManagerSystem manager = NetworkManagerSystem.singleton;
            if (manager == null || manager.client == null)
            {
                return;
            }

            NetworkConnection connection = manager.client.connection;
            if (connection == null || !connection.isConnected)
            {
                return;
            }

            if (connection.Send(
                    PresenceMessage.MsgType,
                    new PresenceMessage(PresenceMessage.ProtocolVersion)))
            {
                helloPending = false;
            }
        }

        private static void HandlePresence(NetworkMessage netMessage)
        {
            try
            {
                PresenceMessage message = netMessage.ReadMessage<PresenceMessage>();
                if (message != null && message.version >= 1 && netMessage.conn != null)
                {
                    ModdedConnections.Add(netMessage.conn);
                }
            }
            catch (Exception ex)
            {
                if (EveryoneGetsOnePlugin.VerboseLog.Value)
                {
                    EveryoneGetsOnePlugin.Log.LogWarning("Failed to read presence message: " + ex);
                }
            }
        }
    }
}
