﻿using BepInEx;
using BepInEx.Logging;
using Discord;
using GameNetcodeStuff;
using HarmonyLib;
using MonoMod.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
using NetworkManager = Unity.Netcode.NetworkManager;

namespace LethalPresents
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class LethalPresentsPlugin : BaseUnityPlugin
    {

        public static ManualLogSource mls;

        private static int spawnChance = 5;
        private static string[] disabledEnemies = new string[0];
        private static bool IsAllowlist = false;
        private static bool ShouldSpawnMines = false;
        private static bool ShouldSpawnTurrets = false;
        private static bool ShouldSpawnBees = false;

        private static bool isHost => RoundManager.Instance.NetworkManager.IsHost;
        private static SelectableLevel currentLevel => RoundManager.Instance.currentLevel;

        internal static T GetPrivateField<T>(object instance, string fieldName)
        {
            const BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo field = instance.GetType().GetField(fieldName, bindFlags);
            return (T)field.GetValue(instance);
        }

        private void Awake()
        {
            // Plugin startup logic
            mls = BepInEx.Logging.Logger.CreateLogSource(PluginInfo.PLUGIN_GUID);
            mls.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

            loadConfig();

            On.RoundManager.AdvanceHourAndSpawnNewBatchOfEnemies += updateCurrentLevelInfo;
            On.GiftBoxItem.OpenGiftBoxServerRpc += spawnRandomEntity;
        }

        private void updateCurrentLevelInfo(On.RoundManager.orig_AdvanceHourAndSpawnNewBatchOfEnemies orig, RoundManager self)
        {
            orig(self);

            mls.LogInfo("List of spawnable enemies (inside):");
            currentLevel.Enemies.ForEach(e => mls.LogInfo(e.enemyType.name));
            mls.LogInfo("List of spawnable enemies (outside):");
            currentLevel.OutsideEnemies.ForEach(e => mls.LogInfo(e.enemyType.name));
        }

        private void loadConfig()
        {
            spawnChance = Config.Bind<int>("General", "SpawnChance", 5, "Chance of spawning an enemy when opening a present [0-100]").Value;
            disabledEnemies = Config.Bind<string>("General", "EnemyBlocklist", "", "Enemy blocklist separated by , and without whitespaces").Value.Split(",");
            IsAllowlist = Config.Bind<bool>("General", "IsAllowlist", false, "Turns blocklist into allowlist, blocklist must contain at least one inside and one outside enemy, use at your own risk").Value;
            ShouldSpawnMines = Config.Bind<bool>("General", "ShouldSpawnMines", true, "Add mines to the spawn pool").Value;
            ShouldSpawnTurrets = Config.Bind<bool>("General", "ShouldSpawnTurrets", true, "Add turrets to the spawn pool").Value;
            //ShouldSpawnBees = Config.Bind<bool>("General", "ShouldSpawnBees", true, "Add bees to the spawn pool").Value;
            if (IsAllowlist)
            {
                mls.LogInfo("Only following enemies can spawn from the gift:");
            }
            else
            {
                mls.LogInfo("Following enemies wont be spawned from the gift:");
            }
            foreach(string entry in disabledEnemies)
            {
                mls.LogInfo(entry);
            }
        }


        private void spawnRandomEntity(On.GiftBoxItem.orig_OpenGiftBoxServerRpc orig, GiftBoxItem self)
        {
            NetworkManager networkManager = self.NetworkManager;
            
            if ((object)networkManager == null || !networkManager.IsListening)
            {
                orig(self);
                return;
            }
            int exec_stage = GetPrivateField<int>(self, "__rpc_exec_stage");
            mls.LogInfo("IsServer:" + networkManager.IsServer + " IsHost:" + networkManager.IsHost + " __rpc_exec_stage:" + exec_stage);

            if (exec_stage != 1 || !isHost)
            {
                orig(self);
                return;
            }
            int fortune = UnityEngine.Random.Range(1, 100);
            mls.LogInfo("Player's fortune:" + fortune);

            if (fortune >= spawnChance)
            {
                orig(self);
                return;
            }
            chooseAndSpawnEnemy(self.isInFactory, self.transform.position, self.previousPlayerHeldBy.transform.position);


            orig(self);
        }

        static void chooseAndSpawnEnemy(bool inside, Vector3 pos, Vector3 player_pos)
        {
            mls.LogInfo("Player pos " + player_pos);

            List<SpawnableEnemyWithRarity> Enemies = currentLevel.Enemies.Where(e =>
            {
                if (disabledEnemies.Contains(e.enemyType.name)) //if enemy is in the list
                {
                    return IsAllowlist;     //if its in allowlist, we can spawn that enemy, otherwise, we cant
                }
                else                        //if enemy isnt in the list
                {
                    return !IsAllowlist;    //if its not in blacklist, we can spawn it, otherwise, we cant
                }
            }).ToList();

            List<SpawnableEnemyWithRarity> OutsideEnemies = currentLevel.OutsideEnemies.Where(e =>
            {
                if (disabledEnemies.Contains(e.enemyType.name))
                {
                    return IsAllowlist;
                }
                else
                {
                    return !IsAllowlist;
                }
            }).ToList();

            int fortune = UnityEngine.Random.Range(1, 2 + (OutsideEnemies.Count + Enemies.Count) / 2); //keep the mine/turrent % equal to the regular monster pool
            if (fortune == 2 && !ShouldSpawnMines) fortune = 1; //shouldnt spawn mines - try turrets instead
            if (fortune == 1 && !ShouldSpawnTurrets) fortune = 2; // shouldnt spawn turrets - try mines instead, if already tried it will just skip next time
            if (fortune == 2 && !ShouldSpawnMines) fortune = 3;

            switch (fortune)
            {
                case 1: // turret
                    foreach (SpawnableMapObject obj in currentLevel.spawnableMapObjects)
                    {
                        if (obj.prefabToSpawn.GetComponentInChildren<Turret>() == null) continue;
                        pos -= Vector3.up * 1.8f;
                        var turret = Instantiate<GameObject>(obj.prefabToSpawn, pos, Quaternion.identity);
                        turret.transform.position = pos;
                        turret.transform.forward = (player_pos - pos).normalized;// new Vector3(1, 0, 0);
                        turret.GetComponent<NetworkObject>().Spawn(true);
                        mls.LogInfo("Tried spawning a turret at " + pos);
                        //objectsTo
                        break;
                    }
                    break;
                case 2: //mine
                    foreach (SpawnableMapObject obj in currentLevel.spawnableMapObjects)
                    {
                        if (obj.prefabToSpawn.GetComponentInChildren<Landmine>() == null) continue;
                        pos -= Vector3.up * 1.8f;
                        var mine = Instantiate<GameObject>(obj.prefabToSpawn, pos, Quaternion.identity);
                        mine.transform.position = pos;
                        mine.transform.forward = new Vector3(1, 0, 0);
                        mine.GetComponent<NetworkObject>().Spawn(true);
                        mls.LogInfo("Tried spawning a mine at " + pos);
                        break;
                    }
                    break;
                default: //enemy

                    SpawnableEnemyWithRarity enemy;
                    if (inside) //inside
                    {

                        if (Enemies.Count < 1)
                        {
                            mls.LogInfo("Cant spawn enemy - no other enemies present to copy from");
                            return;
                        }

                        enemy = Enemies[UnityEngine.Random.Range(0, Enemies.Count - 1)];
                    }
                    else  //outside + ship
                    {

                        if (OutsideEnemies.Count < 1)
                        {
                            mls.LogInfo("Cant spawn enemy - no other enemies present to copy from");
                            return;
                        }

                        enemy = OutsideEnemies[UnityEngine.Random.Range(0, OutsideEnemies.Count - 1)];
                    }

                    pos += Vector3.up * 0.25f;
                    mls.LogInfo("Spawning " + enemy.enemyType.enemyName + " at " + pos);
                    SpawnEnemy(enemy, pos, 0);
                    break;
            }
        }
        private static void SpawnEnemy(SpawnableEnemyWithRarity enemy, Vector3 pos, float rot)
        {
            RoundManager.Instance.SpawnEnemyGameObject(pos, rot, -1, enemy.enemyType);
        }

    }
}