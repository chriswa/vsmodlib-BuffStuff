using ProtoBuf;
using Vintagestory.API.Common.Entities;

namespace BuffStuff {
  public abstract class Buff {
    public abstract string ID { get; }
    public int TickCounter;
    public double ExpireTimestampInDays;
    internal Entity entity;
    public Entity Entity { get { return entity; } }
    /// <summary>Called when a buff is first applied. If a buff is applied while the buff is still active, OnStart is not called: OnStack is called instead.</summary>
    public virtual void OnStart() { }
    /// <summary>Called when a buff is applied while a buff with an identical ID is still active. OnStack is called on the second buff, which will replace the first buff.</summary>
    public virtual void OnStack(Buff oldBuff) { }
    /// <summary>Called when a buff expires due to time passing or SetExpiryImmediately() having been called.</summary>
    public virtual void OnExpire() { }
    /// <summary>Called when the Entity dies.</summary>
    public virtual void OnDeath() { }
    /// <summary>Called every 250 ms (4 times per second) while the buff is active.</summary>
    public virtual void OnTick() { }
    /// <summary>Called when the player with this active buff leaves the server.</summary>
    public virtual void OnLeave() { }
    /// <summary>Called when the player with this active buff re-joins the server.</summary>
    public virtual void OnJoin() { }
    protected void SetExpiryInGameDays(double deltaDays) {
      ExpireTimestampInDays = BuffManager.Now + deltaDays;
    }
    protected void SetExpiryInGameHours(double deltaHours) {
      ExpireTimestampInDays = BuffManager.Now + deltaHours / 24.0;
    }
    protected void SetExpiryInGameMinutes(double deltaMinutes) {
      ExpireTimestampInDays = BuffManager.Now + deltaMinutes / 24.0 / 60.0;
    }
    protected void SetExpiryNever() {
      ExpireTimestampInDays = double.PositiveInfinity;
    }
    protected void SetExpiryImmediately() {
      ExpireTimestampInDays = 0;
    }
    public void Apply(Entity entity) {
      BuffManager.ApplyBuff(entity, this);
    }
    public void Remove() {
      BuffManager.RemoveBuff(entity, this);
    }
  }
}
