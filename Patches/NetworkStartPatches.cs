using HarmonyLib;
using RoR2.Networking;

namespace EveryoneGetsOne.Patches
{
    /// <summary>
    /// Register the presence handler as soon as the server exists, rather than
    /// waiting for the next Update.  NetworkServer.RegisterHandler requires an
    /// active NetworkServer instance.
    /// </summary>
    [HarmonyPatch(typeof(NetworkManagerSystem))]
    [HarmonyPatch(nameof(NetworkManagerSystem.OnStartServer))]
    internal static class NetworkServerStartPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            NetworkCompat.RegisterServerHandler();
        }
    }
}
