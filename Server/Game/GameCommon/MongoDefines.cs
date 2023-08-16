using MongoDB.Bson.Serialization.Attributes;

namespace ZQ.Mongo
{
    public static partial class MongoDefines
    {
        // DB name
        public static string DBName = "zq";


        // account collection name
        public static string ColAccount = "accounts";
        public static string ColAccountKeySDKChannelID = nameof(DBAccount.SDKChannelId);
        public static string ColAccountKeySDKUserID = nameof(DBAccount.SDKUserId);
        public static string ColAccountKeyProfileId = nameof(DBAccount.ProfileId);


        // player collection name
        public static string ColPlayers = "players";
        public static string ColPlayerKeyName = nameof(DBPlayerBaseInfo.Nickname);
        public static string ColPlayerKeyNickName = nameof(DBPlayerBaseInfo.Nickname);
    }

    [BsonIgnoreExtraElements]
    public class DBAccount
    {
        public int SDKChannelId;
        public string SDKUserId;
        public string ProfileId;
    }

    [BsonIgnoreExtraElements]
    public class DBPlayerData
    {
        public string ProfileId;
        public DBPlayerBaseInfo BaseInfo;
    }

    [BsonIgnoreExtraElements]
    public class DBPlayerBaseInfo
    {
        //[BsonDefaultValue("")]
        public string Nickname;
    }
}
