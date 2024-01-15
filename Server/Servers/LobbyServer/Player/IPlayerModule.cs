using C2S;
using ZQ.Mongo;

namespace ZQ
{
    public abstract class IPlayerModule
    {
        protected readonly Player m_player;

        public bool DataDirty { get; protected set; }

        public IPlayerModule(Player player)
        {
            m_player = player;
        }

        public virtual void Update(long timeNow) { }

        public abstract bool Init(DBPlayerData dbData);
        public abstract void OnLoginSuccess(C2ZLoginZoneRes res);
    }
}
