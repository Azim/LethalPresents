using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Runtime.CompilerServices;
using Unity.Netcode;
using UnityEngine;

namespace LethalPresents
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class LethalPresentsPlugin : BaseUnityPlugin
    {

        private readonly Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID);

        private static bool isHost;
        private static SelectableLevel currentLevel; 
        public static ManualLogSource mls;

        private void Awake()
        {
            // Plugin startup logic
            mls = BepInEx.Logging.Logger.CreateLogSource(PluginInfo.PLUGIN_GUID);
            mls.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
            harmony.PatchAll(typeof(LethalPresentsPlugin));
        }

        [HarmonyPatch(typeof(RoundManager), "Start")]
        [HarmonyPrefix]
        static void setIsHost()
        {
            isHost = RoundManager.Instance.NetworkManager.IsHost;
        }


        [HarmonyPatch(typeof(RoundManager), "AdvanceHourAndSpawnNewBatchOfEnemies")]
        [HarmonyPrefix]
        static void updateCurrentLevelInfo(ref EnemyVent[] ___allEnemyVents, ref SelectableLevel ___currentLevel)
        {
            currentLevel = ___currentLevel;
            //currentLevelVents = ___allEnemyVents;
        }


        [HarmonyPatch(typeof(GiftBoxItem), "OpenGiftBoxServerRpc")]
        [HarmonyPrefix]
        static void spawnRandomEntity(GiftBoxItem __instance)
        {
            NetworkManager networkManager = __instance.NetworkManager;

            if ((object)networkManager == null || !networkManager.IsListening)
            {
                return;
            }
            int exec_stage = Traverse.Create(__instance).Field("__rpc_exec_stage").GetValue<int>();
            mls.LogInfo("IsServer:" + networkManager.IsServer + " IsHost:" + networkManager.IsHost + " __rpc_exec_stage:" + exec_stage);

            if(exec_stage != 1 || !isHost)
            {
                return;
            }
            int fortune = UnityEngine.Random.Range(0, 99);
            mls.LogInfo("Player's fortune:"+fortune);

            if (fortune >= 4) return;
            if (__instance.isInFactory) //inside
            {
                if(currentLevel.Enemies.Count < 1)
                {
                    mls.LogInfo("Cant spawn enemy - no other enemies present to copy from");
                    return;
                }

                int enemyIndex = UnityEngine.Random.Range(0, currentLevel.Enemies.Count - 1);
                mls.LogInfo("Enemy index" + enemyIndex + " (" + currentLevel.Enemies[enemyIndex].enemyType.enemyName + ")");

                SpawnEnemy(currentLevel.Enemies[enemyIndex], __instance.transform.position + Vector3.up * 0.25f, 0);
                mls.LogInfo("spawned at " + (__instance.transform.position + Vector3.up * 0.25f));
            }
            else if(false /* || __instance.isInElevator || __instance.isInShipRoom */) //both mean the same thing https://discord.com/channels/1169792572382773318/1169851653416034397/1183813953067946064
            {
                //do nothing for now
            }
            else  //outside + ship
            {
                if (currentLevel.OutsideEnemies.Count < 1)
                {
                    mls.LogInfo("Cant spawn enemy - no other enemies present to copy from");
                    return;
                }

                int enemyIndex = UnityEngine.Random.Range(0, currentLevel.OutsideEnemies.Count - 1);
                mls.LogInfo("Enemy index" + enemyIndex + " (" + currentLevel.OutsideEnemies[enemyIndex].enemyType.enemyName + ")");

                SpawnEnemy(currentLevel.OutsideEnemies[enemyIndex], __instance.transform.position + Vector3.up * 0.25f, 0);
                mls.LogInfo("spawned at " + (__instance.transform.position + Vector3.up * 0.25f));
            }

        }

        private static void SpawnEnemy(SpawnableEnemyWithRarity enemy, Vector3 pos, float rot)
        {
            RoundManager.Instance.SpawnEnemyGameObject(pos, rot, -1, enemy.enemyType);

        }

    }
}