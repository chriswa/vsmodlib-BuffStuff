using ProtoBuf;

namespace BuffStuff {
  [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
  internal class SerializedBuff {
    public string id;
    public double timeRemainingInDays;
    public int tickCounter;
    public byte[] data;
  }
}
