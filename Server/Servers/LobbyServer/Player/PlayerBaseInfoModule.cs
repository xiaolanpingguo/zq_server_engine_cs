using C2S;
using Google.Protobuf;
using ZQ.Mongo;

namespace ZQ
{
    public class PlayerBaseInfoModule : IPlayerModule
    {
        public DBPlayerBaseInfo BaseInfo { get; private set; } = null!;

        public PlayerBaseInfoModule(Player player) : base(player)
        {
            m_player.RegisterMessage(C2S_MSG_ID.IdC2ZChangeNicknameReq, typeof(C2ZChangeNicknameReq), OnChangeNickNameReq);
        }

        public override bool Init(DBPlayerData dbData)
        {
            // if dbdata of this module is null
            // it means this is a new user or the user's db data is just behind current game version(eg: add a new module)
            if (dbData.BaseInfo == null)
            {
                BaseInfo = new DBPlayerBaseInfo();
                BaseInfo.Nickname = "new_user";
                dbData.BaseInfo = BaseInfo;
                DataDirty = true;
            }
            else
            {
                BaseInfo = dbData.BaseInfo;
            }

            return true;
        }

        public override void OnLoginSuccess(C2ZLoginZoneRes res)
        {
            res.BaseInfo = new CSPlayerBaseInfo();
            res.BaseInfo.ProfileId = m_player.ProfileId;
            res.BaseInfo.Nickname = BaseInfo.Nickname;
        }

        public override void Update(long timeNow)
        {
            base.Update(timeNow);
        }

        #region C2S Message
        private void OnChangeNickNameReq(ushort messageId, int rpcId, IMessage? message)
        {

        }

        #endregion
    }
}
