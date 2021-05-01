# vsmodlib-BuffStuff

A library for creating custom temporary buffs.

Buffs are are automatically "paused" for players who leave the server, and also when the server is shutdown.

## Installing

```bash
cd src
git submodule add https://github.com/chriswa/vsmodlib-BuffStuff.git
```

Or just download it as a zip and unzip it into your src/ directory.

## Usage

In your mod's `StartServerSide`, you must call `BuffStuff.BuffManager.Initialize`. You must also call `BuffStuff.BuffManager.RegisterBuffType` for each of your Buff types to register it with a unique ID:

```cs
  public class MyMod : ModSystem {
    public override void StartServerSide(ICoreServerAPI api) {
      BuffStuff.BuffManager.Initialize(api, this);
      BuffStuff.BuffManager.RegisterBuffType("MySampleBuff", typeof(MySampleBuff));
```

To apply a buff, instantiate your buff class, and call its `Apply` method, passing it an `Entity`:

```cs
      var myBuff = new MySampleBuff();
      myBuff.Apply(entity);
```

For example, you could register a command to apply a buff:

```cs
      api.RegisterCommand("buffme", "Test out my buff!", "/buffme", (IServerPlayer player, int groupId, CmdArgs args) => {
        new MySampleBuff().Apply(player.Entity);
      }, Privilege.chat);
```

### Creating a Buff

Create a buff by extending the `BuffStuff.Buff` class.

Your class _must_ override the `ID` getter to return the same string that you used to register your buff.

Your class _must_ also describe how to serialize itself via ProtoBuf. The simplest solution is to use the "AllPublic" approach below, explicitly ignoring the ID field.

If you add any of your own public fields, they will be automatically serialized and deserialized for you. You will not need to add any `[annotations]` to your new fields.

```cs
  [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
  public class MySampleBuff : BuffStuff.Buff {
    [ProtoIgnore]
    public override string ID => "MySampleBuff";
  }
```

By default, your buff will expire on the next tick (250 ms) after it was applied. Override `OnStart` to set the buff's expiry to some point in the future. Note that 1 game minute seems to be equivalent to roughly 1.85 seconds, with default settings.

```cs
    public override void OnStart() {
      SetExpiryInGameMinutes(5);
    }
```

To handle the case when a second buff is applied before the first buff expires, override `OnStack`. Unless you're doing something advanced with "stackable" buffs, you'll probably want to ignore the `oldBuff` and reset the buff's expiry. The old buff is discarded after this method is called on the new buff.

```cs
    public override void OnStack(Buff oldBuff) {
      SetExpiryInGameMinutes(5);
    }
```

Active buffs get an `OnTick` call every 250 milliseconds (4 times per IRL second). Note that you have access to the `Entity` and `TickCounter` from any method.

```cs
    public override void OnTick() {
      // heal the player 0.1 hp every 2 seconds
      if (TickCounter % 8 == 0) {
        Entity.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Internal, Type = EnumDamageType.Heal }, 0.1f);
      }
    }
```

If your buff makes a persistent change in `OnStart` (for example, setting something in `entity.Stats`,) you can `OnExpire` to revert it:

```cs
    public override void OnStart() {
      entity.Stats.Set("walkspeed", "mymod", 0.5f, true);
    }
    public override void OnExpire() {
      entity.Stats.Remove("walkspeed", "mymod");
    }
```

By default, buffs persist beyond death, so you'll likely want to override `OnDeath` to expire yours:

```cs
    public override void OnDeath() {
      SetExpiryImmediate();
    }
```

There are also `OnLeave` and `OnJoin` callbacks, but I have no idea what you might want to use them for. They are, of course, only called for buffs on entities which happen to be players.

```cs
    public override void OnLeave() {
    }
    public override void OnJoin() {
    }
```

## Using Ticks for Expiry Instead of Game Time

Game time may not be precise enough for some buffs. If you want to use ticks to expire your buff, instead of game minutes, you can call `SetExpiryNever()` in `OnStart` and then, in your `OnTick` call `SetExpiryImmediate()` when TickCounter reaches a certain value.

```cs
    public override void OnStart() {
      SetExpiryNever();
    }
    public override void OnTick() {
      if (TickCounter > 40) { // 4 ticks per IRL second
        SetExpiryImmediate();
      }
    }
```

Please note that `SetExpiryImmediate` doesn't expire a buff until just before its next tick.
