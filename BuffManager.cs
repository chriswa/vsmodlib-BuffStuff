using System;
using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace BuffStuff {
  public class BuffManager {
    private static readonly int TICK_MILLISECONDS = 250;
    private static bool isInitialized = false;
    private static Dictionary<string, Type> buffTypes = new Dictionary<string, Type>();
    private static Dictionary<Entity, Dictionary<string, Buff>> activeBuffsByEntityAndBuffId = new Dictionary<Entity, Dictionary<string, Buff>>();
    private static Dictionary<string, List<SerializedBuff>> inactiveBuffsByPlayerUid = new Dictionary<string, List<SerializedBuff>>();
    public static void RegisterBuffType(string buffTypeId, Type buffType) {
      if (!isInitialized) { throw new System.Exception("BuffManager.RegisterBuff: must call BuffManager.Initialize() first!"); }
      if (buffTypes.ContainsKey(buffTypeId)) { throw new System.Exception($"BuffManager.RegisterBuff: buffId already registered: {buffTypeId}"); }
      buffTypes[buffTypeId] = buffType;
    }
    private static SerializedBuff serializeBuff(Buff buff) {
      return new SerializedBuff() {
        id = buff.ID,
        timeRemainingInDays = buff.ExpireTimestampInDays - api.World.Calendar.TotalDays,
        data = buff.Serialize()
      };
    }
    private static Buff deserializeBuff(SerializedBuff serializedBuff, Entity entity) {
      var buff = (Buff)Activator.CreateInstance(buffTypes[serializedBuff.id]);
      buff.entity = entity;
      buff.ExpireTimestampInDays = api.World.Calendar.TotalDays + serializedBuff.timeRemainingInDays;
      buff.Deserialize(serializedBuff.data);
      return buff;
    }
    private static ICoreAPI api;
    internal static double Now { get { return api.World.Calendar.TotalDays; } }
    public static void Initialize(ICoreAPI api_, ModSystem mod) {
      api = api_;
      if (api.Side == EnumAppSide.Server) {
        buffTypes.Clear();
        activeBuffsByEntityAndBuffId.Clear();
        inactiveBuffsByPlayerUid.Clear();

        var sapi = api as ICoreServerAPI;
        sapi.Event.SaveGameLoaded += () => {
          var data = sapi.WorldManager.SaveGame.GetData($"{mod.Mod.Info.ModID}:BuffStuff");
          if (data != null) {
            inactiveBuffsByPlayerUid = SerializerUtil.Deserialize<Dictionary<string, List<SerializedBuff>>>(data);
          }
        };
        sapi.Event.GameWorldSave += () => {
          var activeAndInactiveBuffsByPlayerUid = new Dictionary<string, List<SerializedBuff>>(inactiveBuffsByPlayerUid); // shallow clone dictionary
          foreach (var entityActiveBuffsByBuffIdPair in activeBuffsByEntityAndBuffId) { // add active buffs too!
            var playerEntity = entityActiveBuffsByBuffIdPair.Key as EntityPlayer;
            if (playerEntity != null) {
              activeAndInactiveBuffsByPlayerUid[playerEntity.PlayerUID] = entityActiveBuffsByBuffIdPair.Value.Select(pair => serializeBuff(pair.Value)).ToList();
            }
          }
          sapi.WorldManager.SaveGame.StoreData($"{mod.Mod.Info.ModID}:BuffStuff", SerializerUtil.Serialize<Dictionary<string, List<SerializedBuff>>>(activeAndInactiveBuffsByPlayerUid));
        };
        sapi.Event.PlayerNowPlaying += (serverPlayer) => {
          var playerUid = (serverPlayer.Entity as EntityPlayer).PlayerUID;
          if (inactiveBuffsByPlayerUid.TryGetValue(playerUid, out var inactiveBuffs)) {
            var now = Now;
            activeBuffsByEntityAndBuffId[serverPlayer.Entity] = inactiveBuffs.Select(serializedBuff => deserializeBuff(serializedBuff, serverPlayer.Entity)).ToDictionary(buff => buff.ID, buff => buff);
            inactiveBuffsByPlayerUid.Remove(playerUid);
            foreach (var buffIdAndBuffPair in activeBuffsByEntityAndBuffId[serverPlayer.Entity]) {
              buffIdAndBuffPair.Value.OnJoin();
            }
          }
        };
        sapi.Event.PlayerLeave += (serverPlayer) => {
          var playerUid = (serverPlayer.Entity as EntityPlayer).PlayerUID;
          if (activeBuffsByEntityAndBuffId.TryGetValue(serverPlayer.Entity, out var activeBuffsByBuffId)) {
            foreach (var activeBuffsByBuffIdPair in activeBuffsByBuffId) {
              activeBuffsByBuffIdPair.Value.OnLeave();
            }
            inactiveBuffsByPlayerUid[playerUid] = activeBuffsByBuffId.Select(pair => serializeBuff(pair.Value)).ToList();
            // activeBuffs.ToDictionary(pair => pair.Key, pair => pair.Value - now); // convert remaining time to future timestamp
            activeBuffsByEntityAndBuffId.Remove(serverPlayer.Entity);
          }
        };
        api.Event.OnEntityDespawn += (Entity entity, EntityDespawnReason reason) => {
          activeBuffsByEntityAndBuffId.Remove(entity);
        };
        sapi.Event.PlayerDeath += (serverPlayer, damageSource) => {
          if (activeBuffsByEntityAndBuffId.TryGetValue(serverPlayer.Entity, out var activeBuffsByBuffId)) {
            foreach (var activeBuffsByBuffIdPair in activeBuffsByBuffId) {
              activeBuffsByBuffIdPair.Value.OnDeath();
            }
          }
          activeBuffsByEntityAndBuffId.Remove(serverPlayer.Entity);
        };
        api.World.RegisterGameTickListener((float dt) => {
          var now = Now;
          foreach (var entity in activeBuffsByEntityAndBuffId.Keys.ToArray()) {
            var activeBuffsByBuffId = activeBuffsByEntityAndBuffId[entity];
            foreach (var buff in activeBuffsByBuffId.Values.ToArray()) {
              if (buff.ExpireTimestampInDays < now) {
                buff.OnExpire();
                RemoveBuff(entity, buff);
              }
              else {
                buff.OnTick();
              }
            }
          }
        }, TICK_MILLISECONDS);
      }
      isInitialized = true;
    }
    internal static void ApplyBuff(Entity entity, Buff buff) {
      if (!isInitialized) { throw new System.Exception("BuffManager.RegisterBuff: must call BuffManager.Initialize() first"); }
      if (!buffTypes.ContainsKey(buff.ID)) { throw new System.Exception($"BuffManager.RegisterBuff: must call BuffManager.RegisterBuffType() first for buff ID {buff.ID}"); }
      buff.entity = entity; // set entity! this is otherwise unsettable, because it's internal
      Dictionary<string, Buff> activeBuffs;
      if (!activeBuffsByEntityAndBuffId.TryGetValue(entity, out activeBuffs)) { // if entity doesn't have a dict pair, create one
        activeBuffs = new Dictionary<string, Buff>();
        activeBuffsByEntityAndBuffId[entity] = activeBuffs;
      }
      if (activeBuffsByEntityAndBuffId[entity].TryGetValue(buff.ID, out var oldBuff)) {
        buff.OnStack(oldBuff);
      }
      else {
        buff.OnStart();
      }
      activeBuffs[buff.ID] = buff;
    }
    internal static bool RemoveBuff(Entity entity, Buff buff) { // n.b. does not call any Buff event callbacks!
      if (activeBuffsByEntityAndBuffId.TryGetValue(entity, out var activeBuffs)) {
        if (activeBuffs.Remove(buff.ID)) {
          if (activeBuffs.Count == 0) {
            activeBuffsByEntityAndBuffId.Remove(entity); // cleanup dictionary pair with now-empty list!
          }
          return true;
        }
      }
      return false;
    }
  }
}
