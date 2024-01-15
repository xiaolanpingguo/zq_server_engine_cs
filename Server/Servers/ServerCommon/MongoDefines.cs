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
        public int SDKChannelId { get; set; }
        public string SDKUserId { get; set; } = null!;
        public string ProfileId { get; set; } = null!;
    }

    [BsonIgnoreExtraElements]
    public class DBPlayerData
    {
        public string ProfileId { get; set; } = null!;
        public DBPlayerBaseInfo BaseInfo { get; set; } = null!;
    }

    [BsonIgnoreExtraElements]
    public class DBPlayerBaseInfo
    {
        //[BsonDefaultValue("")]
        public string Nickname { get; set; } = null!;
    }
}
