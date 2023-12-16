using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
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

        private static int spawnChance = 5;
        private static string[] disabledEnemies = new string[0];
        private static bool IsAllowlist = false;

        private void Awake()
        {
            // Plugin startup logic
            mls = BepInEx.Logging.Logger.CreateLogSource(PluginInfo.PLUGIN_GUID);
            mls.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");


            harmony.PatchAll(typeof(LethalPresentsPlugin));
        }

        private void loadConfig()
        {
            spawnChance = Config.Bind<int>("General", "SpawnChance", 5, "Chance of spawning an enemy when opening a present [0-100]").Value;
            disabledEnemies = Config.Bind<string>("General", "EnemyBlocklist", "", "Enemy blocklist separated by , and without whitespaces").Value.Split(",");
            IsAllowlist = Config.Bind<bool>("General", "IsAllowlist", false, "Turns blocklist into allowlist, blocklist must contain at least one inside and one outside enemy, use at your own risk").Value;
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

        static void chooseAndSpawnEnemy(bool inside, Vector3 pos)
        {
            SpawnableEnemyWithRarity enemy;

            if (inside) //inside
            {
                List<SpawnableEnemyWithRarity> Enemies = currentLevel.Enemies.Where(e => {
                    if (disabledEnemies.Contains(e.enemyType.enemyName)) //if enemy is in the list
                    {
                        return IsAllowlist;     //if its in allowlist, we can spawn that enemy, otherwise, we cant
                    }
                    else                        //if enemy isnt in the list
                    {
                        return !IsAllowlist;    //if its not in blacklist, we can spawn it, otherwise, we cant
                    }
                }).ToList();

                if (Enemies.Count < 1)
                {
                    mls.LogInfo("Cant spawn enemy - no other enemies present to copy from");
                    return;
                }

                enemy = Enemies[UnityEngine.Random.Range(0, Enemies.Count - 1)];
            }
            else  //outside + ship
            {
                List<SpawnableEnemyWithRarity> OutsideEnemies = currentLevel.OutsideEnemies.Where(e => {
                    if (disabledEnemies.Contains(e.enemyType.enemyName))
                    {
                        return IsAllowlist;
                    }
                    else
                    {
                        return !IsAllowlist;
                    }
                }).ToList();

                if (OutsideEnemies.Count < 1)
                {
                    mls.LogInfo("Cant spawn enemy - no other enemies present to copy from");
                    return;
                }

                enemy = OutsideEnemies[UnityEngine.Random.Range(0, OutsideEnemies.Count - 1)];
            }


            mls.LogInfo("Spawning " + enemy.enemyType.enemyName + " at " + pos);
            SpawnEnemy(enemy, pos, 0);
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
            int fortune = UnityEngine.Random.Range(1, 100);
            mls.LogInfo("Player's fortune:"+fortune);

            if (fortune >= spawnChance) return;

            chooseAndSpawnEnemy(__instance.isInFactory, __instance.transform.position + Vector3.up * 0.25f);

        }

        private static void SpawnEnemy(SpawnableEnemyWithRarity enemy, Vector3 pos, float rot)
        {
            RoundManager.Instance.SpawnEnemyGameObject(pos, rot, -1, enemy.enemyType);

        }

    }
}