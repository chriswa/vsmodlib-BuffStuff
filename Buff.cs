using Vintagestory.API.Common.Entities;

namespace BuffStuff {
  public abstract class Buff {
    public abstract string ID { get; }
    public double ExpireTimestampInDays;
    internal Entity entity;
    public Entity Entity { get { return entity; } }
    public virtual void OnStack(Buff otherBuff) { }
    public virtual void OnStart() { }
    public virtual void OnExpire() { }
    public virtual void OnDeath() { }
    public virtual void OnTick() { }
    public virtual void OnLeave() { }
    public virtual void OnJoin() { }
    public virtual byte[] Serialize() { return null; }
    public virtual void Deserialize(byte[] data) { }
    protected void SetRelativeExpiryInDays(double deltaDays) {
      ExpireTimestampInDays = BuffManager.Now + deltaDays;
    }
    public void Apply(Entity entity) {
      BuffManager.ApplyBuff(entity, this);
    }
    public void Remove() {
      BuffManager.RemoveBuff(entity, this);
    }
  }
}
