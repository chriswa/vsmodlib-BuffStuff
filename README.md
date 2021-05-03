# vsmodlib-BuffStuff

A library for creating custom, temporary buffs.

## Overview

This source code library can be included in your mod's source code to support your own buffs.

Buffs can specify their own expiry time, either in game time or real time.

Buff expiry is automatically "paused" for players who leave the server, and also when the server is shutdown.

Buffs get an `OnTick` method call every 250 ms while active, and there are several other event-based methods which get called, such as `OnStart`, `OnExpire`, `OnStack`, and more.

## Installing

```bash
cd src
git submodule add https://github.com/chriswa/vsmodlib-BuffStuff.git
```

Or just download it as a zip and unzip it into your `src/` directory.

If you need to create buffs in multiple mods, simply include a copy of the library in each mod. (It's very small!)

However, note that you will not be able to access buffs from other mods: each mod has its own `BuffManager`.

## Usage

In your mod's `StartServerSide`, you must call `BuffManager.Initialize`. You must also call `BuffManager.RegisterBuffType` for each of your Buff types to register it with a unique ID:

```cs
  public class MyMod : ModSystem {
    public override void StartServerSide(ICoreServerAPI api) {
      BuffManager.Initialize(api, this);
      BuffManager.RegisterBuffType("MySampleBuff", typeof(MySampleBuff));
```

To apply a buff, instantiate your buff class (see Creating a Buff Class below,) and call its `Apply` method, passing it an `Entity`. You will need to create a `new MySampleBuff()` each time you want to apply a buff.

```cs
      var myBuff = new MySampleBuff();
      myBuff.Apply(entity);
```

For example, you could register a command to apply a buff:

```cs
      api.RegisterCommand("buffme", "Test out my buff!", "/buffme", (IServerPlayer player, int groupId, CmdArgs args) => {
        var myBuff = new MySampleBuff();
        myBuff.Apply(player.Entity);
      }, Privilege.chat);
```

### Creating a Buff Class

Create a buff type by extending the `Buff` class. Your class must follow some rules, which are required to support pausing active buffs when players go offline and when the server shuts down:

1. You _must_ describe how to serialize your class to ProtoBuf. The simplest solution is to use the `AllPublic` approach below. This will automatically enable serialization of any new fields you add, without the need to add any additional annotations.

1. You _must not_ add a constructor. (This is because a parameterless constructor is required by ProtoBuf.)

Below is the simplest possible Buff, which you should use as a template for your Buffs. It doesn't do anything, and it never expires.

```cs
  [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
  public class MySampleBuff : Buff {
  }
```

### Event Handling

You can add features to your buff by overriding one or more of the following methods:

  - `OnStart()` - Called when a buff is first applied to an entity. If a buff is applied while a buff (with the same type) is still active on the entity, `OnStart` is not called: `OnStack` is called instead.
  - `OnStack(Buff oldBuff)` - Called when a buff is applied while a buff with an identical type is still active on the entity. `OnStack` is called on the second buff, which will replace the first buff. The first buff is quietly removed without getting any method calls.
  - `OnExpire()` - Called when a buff expires due to game time or ticks passing. (see Auto-expiry below)
  - `OnDeath()` - Called when the entity dies.
  - `OnTick()` - Called every 250 ms (4 times per second) while the buff is active.
  - `OnLeave()` - Called when the player with this active buff leaves the server.
  - `OnJoin()` - Called when the player with this active buff re-joins the server.

Buffs which change the player's stats (e.g. a speed potion) will likely need to define `OnStart`, `OnExpire`, and `OnDeath` overrides.

Buffs which cause damage-over-time (e.g. poison) will likely need to define `OnStart` and `OnTick` overrides.

Most buffs will also need to define `OnStack`, if only because `OnStart` is not always called.

### Auto-expiry

Buffs can be configured to automatically expire. There are two basic ways to specify when they expire:

1. Game time: this is specified in game days, game hours, or game minutes. Game time is sped up drastically by sleeping. The real-world speed of game time can be configured on each server.

1. Real time: okay, this is not _exactly_ real time: it's based on 250 ms ticks. If the server is overloaded, those ticks can be delayed and "real time" can slow down.

Your `OnStart` method should set an expiry time, otherwise your buff will expire before its first tick. The following `Buff` methods can be used to set the expiry time. Everything is relative, so `this.SetExpiryInGameDays(1);` means 1 day from now.

  - `SetExpiryInGameDays(double deltaDays)`
  - `SetExpiryInGameHours(double deltaHours)`
  - `SetExpiryInGameMinutes(double deltaMinutes)`
  - `SetExpiryInTicks(int deltaTicks)`
  - `SetExpiryInRealSeconds(int deltaSeconds)`
  - `SetExpiryInRealMinutes(int deltaMinutes)`
  - `SetExpiryNever()`
  - `SetExpiryImmediately()`

For example:

```cs
    public override void OnStart() {
      SetExpiryInGameHours(2);
    }
```

### Initializing Custom Data

If your want to pass custom data into your Buff, you cannot use a constructor. One solution is to supply an `init` method and call it before you call `apply`. For example, you might want to do this so that you can have one Buff class serve multiple different poison strengths.

```cs
    public double hpRemaining;
    public void init(double hpDamageTotal) {
      hpRemaining = hp;
    }
```

```cs
    var buff = new MySampleBuff();
    buff.init(2.5);
    buff.apply(entity);
```

### Stacking

When a second instance of your Buff is applied to an entity before the first is removed, `OnStack` is called on the new buff, passing in the old buff for reference. The old buff is unceremoniously removed afterward.

Be aware that `OnStart` is not called on the new buff if there was an old buff, so make sure you at least set your expiry in your `OnStack` call!

```cs
    public override void OnStack(Buff oldBuff) {
      SetExpiryInGameHours(2);
    }
```

Note that `Buff oldBuff` will _always_ be the same type as your class, so you can safely cast it to your buff class:

```cs
    public override void OnStack(Buff oldBuff) {
      MySampleBuff myOldBuff = (MySampleBuff)oldBuff;
      hpRemaining += myOldBuff.hpRemaining;
      SetExpiryNever();
    }
```

### Ticks

Active buffs get an `OnTick` call every 250 milliseconds (4 times per IRL second). Note that you have access to the `Entity` and `TickCounter` from any method.

```cs
    public override void OnTick() {
      // heal the player 0.1 hp every 2 seconds
      if (TickCounter % 8 == 0) {
        Entity.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Internal, Type = EnumDamageType.Heal }, 0.1f);
      }
    }
```

### OnExpire

If your buff makes a persistent change in `OnStart` (for example, setting something in `entity.Stats`,) you can use `OnExpire` to revert it:

```cs
    public override void OnStart() {
      entity.Stats.Set("walkspeed", "mymod", 0.5f, true);
    }
    public override void OnExpire() {
      entity.Stats.Remove("walkspeed", "mymod");
    }
```

Just be careful not to call `Remove` without cleaning up, since `Remove` does not call `OnExpire`

### OnDeath

By default, buffs persist beyond death. In most cases, you'll want to override `OnDeath` to expire yours:

```cs
    public override void OnDeath() {
      SetExpiryImmediate();
    }
```

## Example 1: Game Time Based Stat Buff

This buff increases your walk speed by 50% for 2 hours. If you apply the buff a second time while it is still active, the expiry time is reset to 2 hours.

```cs
  [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
  public class SpeedPotionBuff : Buff {
    public override void OnStart() {
      Entity.Stats.Set("walkspeed", "mymodspeedpotion", 0.5f, true);
      SetExpiryInGameHours(2);
    }
    public override void OnStack(Buff oldBuff) {
      Entity.Stats.Set("walkspeed", "mymodspeedpotion", 0.5f, true);
      SetExpiryInGameHours(2);
    }
    public override void OnExpire() {
      Entity.Stats.Remove("walkspeed", "mymodspeedpotion");
    }
  }
```

## Example 2: Tick Based Damage Over Time (De)buff

This buff saps the player's health slowly, over the course of 5 seconds. If you apply the buff a second time while it is still active, the expiry time is reset to 5 seconds.

```cs
  [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
  public class SpeedPotionBuff : Buff {
    private static float HP_PER_TICK = 1f / 12f;
    private static int DURATION_IN_REAL_SECONDS = 5;
    public override void OnStart() {
      SetExpiryInRealSeconds(DURATION_IN_REAL_SECONDS);
    }
    public override void OnStack(Buff oldBuff) {
      SetExpiryInRealSeconds(DURATION_IN_REAL_SECONDS);
    }
    public override void OnTick() {
      Entity.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Internal, Type = EnumDamageType.Poison }, HP_PER_TICK);
    }
  }
```

## Other Functionality

You can use `BuffManager.GetActiveBuff(Entity entity, string buffId)` or `BuffManager.IsBuffActive(Entity entity, string buffId)` to look up an active buff on an entity by its registered `ID`.


