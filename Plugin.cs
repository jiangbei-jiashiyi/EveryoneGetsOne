using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using RoR2;

namespace EveryoneGetsOne
{
    [BepInPlugin(Guid, "EveryoneGetsOne", Version)]
    [BepInProcess("Risk of Rain 2.exe")]
    public sealed class EveryoneGetsOnePlugin : BaseUnityPlugin
    {
        public const string Guid = "com.liaojinghua.ror2.everyonegetsone";
        public const string Version = "1.1.1";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enable;
        internal static ConfigEntry<bool> ShareItems;
        internal static ConfigEntry<bool> ShareCommandPicks;
        internal static ConfigEntry<bool> ShareLunarItems;
        internal static ConfigEntry<bool> ShareVoidItems;
        internal static ConfigEntry<bool> ShareBossItems;
        internal static ConfigEntry<bool> ShareToDead;
        internal static ConfigEntry<bool> AnnounceProgress;
        internal static ConfigEntry<bool> HideClaimedPickups;
        internal static ConfigEntry<bool> VerboseLog;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            Enable = Config.Bind(
                "General", "Enable", true,
                "总开关。关闭后完全恢复原版逻辑。");
            ShareItems = Config.Bind(
                "General", "ShareItems", true,
                "每个物品掉落：每名玩家都可以领取一次。关闭后普通掉落恢复为先到先得。");
            ShareCommandPicks = Config.Bind(
                "General", "ShareCommandPicks", true,
                "统率神器：每名玩家都能从同一个统率精魄里选择一次自己的物品。");
            ShareLunarItems = Config.Bind(
                "Tiers", "ShareLunarItems", true,
                "是否共享月球（蓝色）物品。");
            ShareVoidItems = Config.Bind(
                "Tiers", "ShareVoidItems", true,
                "是否共享虚空物品。");
            ShareBossItems = Config.Bind(
                "Tiers", "ShareBossItems", true,
                "是否共享Boss（黄色）物品。");
            ShareToDead = Config.Bind(
                "General", "ShareToDead", true,
                "永久阵亡的玩家自动获得一份掉落，避免他们卡住掉落物。");
            AnnounceProgress = Config.Bind(
                "General", "AnnounceProgress", false,
                "在聊天栏播报领取进度（会与游戏自带的拾取提示叠加）。");
            HideClaimedPickups = Config.Bind(
                "Visibility", "HideClaimedPickups", true,
                "已领取的物品/统率精魄只对尚未领取的玩家可见：领取者本地不再显示该掉落物，雷达扫描和标记图标也不会再把它显示出来。");
            VerboseLog = Config.Bind(
                "General", "VerboseLog", false,
                "输出调试日志。");

            _harmony = new Harmony(Guid);
            try
            {
                _harmony.PatchAll(typeof(EveryoneGetsOnePlugin).Assembly);

                int patchCount = 0;
                foreach (MethodBase method in _harmony.GetPatchedMethods())
                {
                    patchCount++;
                }
                Log.LogInfo($"Harmony patches applied to {patchCount} method(s).");
            }
            catch (Exception ex)
            {
                Log.LogError("Harmony patch failed: " + ex);
            }

            Stage.onStageStartGlobal += OnStageStart;
            NetworkCompat.Initialize();
            Log.LogInfo($"EveryoneGetsOne v{Version} loaded.");
        }

        private static void OnStageStart(Stage stage)
        {
            LootShare.ResetForStage();
            LootShare.LogDiagnostics();
            if (VerboseLog != null && VerboseLog.Value)
            {
                Log.LogInfo("Stage start: shared-loot state cleared.");
            }
        }

        private void Update()
        {
            NetworkCompat.Update();
            LootShare.TickCleanup();
        }

        private void OnDestroy()
        {
            Stage.onStageStartGlobal -= OnStageStart;
            NetworkCompat.Shutdown();
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }
        }
    }
}
