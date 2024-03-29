// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: db_player.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021, 8981
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace PBDB {

  /// <summary>Holder for reflection information generated from db_player.proto</summary>
  public static partial class DbPlayerReflection {

    #region Descriptor
    /// <summary>File descriptor for db_player.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static DbPlayerReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "Cg9kYl9wbGF5ZXIucHJvdG8SBFBCREIiQAoMUEJQbGF5ZXJEYXRhEjAKEHBs",
            "YXllcl9iYXNlX2luZm8YASABKAsyFi5QQkRCLlBCUGxheWVyQmFzZUluZm8i",
            "gAEKEFBCUGxheWVyQmFzZUluZm8SEgoKcHJvZmlsZV9pZBgBIAEoCRITCgtz",
            "ZGtfdXNlcl9pZBgCIAEoCRIRCglzZGtfdG9rZW4YAyABKAkSEgoKY2hhbm5l",
            "bF9pZBgEIAEoBRIQCghuaWNrbmFtZRgFIAEoCRIKCgJpcBgGIAEoCWIGcHJv",
            "dG8z"));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { },
          new pbr::GeneratedClrTypeInfo(null, null, new pbr::GeneratedClrTypeInfo[] {
            new pbr::GeneratedClrTypeInfo(typeof(global::PBDB.PBPlayerData), global::PBDB.PBPlayerData.Parser, new[]{ "PlayerBaseInfo" }, null, null, null, null),
            new pbr::GeneratedClrTypeInfo(typeof(global::PBDB.PBPlayerBaseInfo), global::PBDB.PBPlayerBaseInfo.Parser, new[]{ "ProfileId", "SdkUserId", "SdkToken", "ChannelId", "Nickname", "Ip" }, null, null, null, null)
          }));
    }
    #endregion

  }
  #region Messages
  public sealed partial class PBPlayerData : pb::IMessage<PBPlayerData>
  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      , pb::IBufferMessage
  #endif
  {
    private static readonly pb::MessageParser<PBPlayerData> _parser = new pb::MessageParser<PBPlayerData>(() => new PBPlayerData());
    private pb::UnknownFieldSet _unknownFields;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pb::MessageParser<PBPlayerData> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::PBDB.DbPlayerReflection.Descriptor.MessageTypes[0]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public PBPlayerData() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public PBPlayerData(PBPlayerData other) : this() {
      playerBaseInfo_ = other.playerBaseInfo_ != null ? other.playerBaseInfo_.Clone() : null;
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public PBPlayerData Clone() {
      return new PBPlayerData(this);
    }

    /// <summary>Field number for the "player_base_info" field.</summary>
    public const int PlayerBaseInfoFieldNumber = 1;
    private global::PBDB.PBPlayerBaseInfo playerBaseInfo_;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public global::PBDB.PBPlayerBaseInfo PlayerBaseInfo {
      get { return playerBaseInfo_; }
      set {
        playerBaseInfo_ = value;
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override bool Equals(object other) {
      return Equals(other as PBPlayerData);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool Equals(PBPlayerData other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (!object.Equals(PlayerBaseInfo, other.PlayerBaseInfo)) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override int GetHashCode() {
      int hash = 1;
      if (playerBaseInfo_ != null) hash ^= PlayerBaseInfo.GetHashCode();
      if (_unknownFields != null) {
        hash ^= _unknownFields.GetHashCode();
      }
      return hash;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override string ToString() {
      return pb::JsonFormatter.ToDiagnosticString(this);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void WriteTo(pb::CodedOutputStream output) {
    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      output.WriteRawMessage(this);
    #else
      if (playerBaseInfo_ != null) {
        output.WriteRawTag(10);
        output.WriteMessage(PlayerBaseInfo);
      }
      if (_unknownFields != null) {
        _unknownFields.WriteTo(output);
      }
    #endif
    }

    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    void pb::IBufferMessage.InternalWriteTo(ref pb::WriteContext output) {
      if (playerBaseInfo_ != null) {
        output.WriteRawTag(10);
        output.WriteMessage(PlayerBaseInfo);
      }
      if (_unknownFields != null) {
        _unknownFields.WriteTo(ref output);
      }
    }
    #endif

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public int CalculateSize() {
      int size = 0;
      if (playerBaseInfo_ != null) {
        size += 1 + pb::CodedOutputStream.ComputeMessageSize(PlayerBaseInfo);
      }
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(PBPlayerData other) {
      if (other == null) {
        return;
      }
      if (other.playerBaseInfo_ != null) {
        if (playerBaseInfo_ == null) {
          PlayerBaseInfo = new global::PBDB.PBPlayerBaseInfo();
        }
        PlayerBaseInfo.MergeFrom(other.PlayerBaseInfo);
      }
      _unknownFields = pb::UnknownFieldSet.MergeFrom(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(pb::CodedInputStream input) {
    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      input.ReadRawMessage(this);
    #else
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
        switch(tag) {
          default:
            _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, input);
            break;
          case 10: {
            if (playerBaseInfo_ == null) {
              PlayerBaseInfo = new global::PBDB.PBPlayerBaseInfo();
            }
            input.ReadMessage(PlayerBaseInfo);
            break;
          }
        }
      }
    #endif
    }

    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    void pb::IBufferMessage.InternalMergeFrom(ref pb::ParseContext input) {
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
        switch(tag) {
          default:
            _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, ref input);
            break;
          case 10: {
            if (playerBaseInfo_ == null) {
              PlayerBaseInfo = new global::PBDB.PBPlayerBaseInfo();
            }
            input.ReadMessage(PlayerBaseInfo);
            break;
          }
        }
      }
    }
    #endif

  }

  public sealed partial class PBPlayerBaseInfo : pb::IMessage<PBPlayerBaseInfo>
  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      , pb::IBufferMessage
  #endif
  {
    private static readonly pb::MessageParser<PBPlayerBaseInfo> _parser = new pb::MessageParser<PBPlayerBaseInfo>(() => new PBPlayerBaseInfo());
    private pb::UnknownFieldSet _unknownFields;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pb::MessageParser<PBPlayerBaseInfo> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::PBDB.DbPlayerReflection.Descriptor.MessageTypes[1]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public PBPlayerBaseInfo() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public PBPlayerBaseInfo(PBPlayerBaseInfo other) : this() {
      profileId_ = other.profileId_;
      sdkUserId_ = other.sdkUserId_;
      sdkToken_ = other.sdkToken_;
      channelId_ = other.channelId_;
      nickname_ = other.nickname_;
      ip_ = other.ip_;
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public PBPlayerBaseInfo Clone() {
      return new PBPlayerBaseInfo(this);
    }

    /// <summary>Field number for the "profile_id" field.</summary>
    public const int ProfileIdFieldNumber = 1;
    private string profileId_ = "";
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public string ProfileId {
      get { return profileId_; }
      set {
        profileId_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }

    /// <summary>Field number for the "sdk_user_id" field.</summary>
    public const int SdkUserIdFieldNumber = 2;
    private string sdkUserId_ = "";
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public string SdkUserId {
      get { return sdkUserId_; }
      set {
        sdkUserId_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }

    /// <summary>Field number for the "sdk_token" field.</summary>
    public const int SdkTokenFieldNumber = 3;
    private string sdkToken_ = "";
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public string SdkToken {
      get { return sdkToken_; }
      set {
        sdkToken_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }

    /// <summary>Field number for the "channel_id" field.</summary>
    public const int ChannelIdFieldNumber = 4;
    private int channelId_;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public int ChannelId {
      get { return channelId_; }
      set {
        channelId_ = value;
      }
    }

    /// <summary>Field number for the "nickname" field.</summary>
    public const int NicknameFieldNumber = 5;
    private string nickname_ = "";
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public string Nickname {
      get { return nickname_; }
      set {
        nickname_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }

    /// <summary>Field number for the "ip" field.</summary>
    public const int IpFieldNumber = 6;
    private string ip_ = "";
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public string Ip {
      get { return ip_; }
      set {
        ip_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override bool Equals(object other) {
      return Equals(other as PBPlayerBaseInfo);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool Equals(PBPlayerBaseInfo other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (ProfileId != other.ProfileId) return false;
      if (SdkUserId != other.SdkUserId) return false;
      if (SdkToken != other.SdkToken) return false;
      if (ChannelId != other.ChannelId) return false;
      if (Nickname != other.Nickname) return false;
      if (Ip != other.Ip) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override int GetHashCode() {
      int hash = 1;
      if (ProfileId.Length != 0) hash ^= ProfileId.GetHashCode();
      if (SdkUserId.Length != 0) hash ^= SdkUserId.GetHashCode();
      if (SdkToken.Length != 0) hash ^= SdkToken.GetHashCode();
      if (ChannelId != 0) hash ^= ChannelId.GetHashCode();
      if (Nickname.Length != 0) hash ^= Nickname.GetHashCode();
      if (Ip.Length != 0) hash ^= Ip.GetHashCode();
      if (_unknownFields != null) {
        hash ^= _unknownFields.GetHashCode();
      }
      return hash;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override string ToString() {
      return pb::JsonFormatter.ToDiagnosticString(this);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void WriteTo(pb::CodedOutputStream output) {
    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      output.WriteRawMessage(this);
    #else
      if (ProfileId.Length != 0) {
        output.WriteRawTag(10);
        output.WriteString(ProfileId);
      }
      if (SdkUserId.Length != 0) {
        output.WriteRawTag(18);
        output.WriteString(SdkUserId);
      }
      if (SdkToken.Length != 0) {
        output.WriteRawTag(26);
        output.WriteString(SdkToken);
      }
      if (ChannelId != 0) {
        output.WriteRawTag(32);
        output.WriteInt32(ChannelId);
      }
      if (Nickname.Length != 0) {
        output.WriteRawTag(42);
        output.WriteString(Nickname);
      }
      if (Ip.Length != 0) {
        output.WriteRawTag(50);
        output.WriteString(Ip);
      }
      if (_unknownFields != null) {
        _unknownFields.WriteTo(output);
      }
    #endif
    }

    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    void pb::IBufferMessage.InternalWriteTo(ref pb::WriteContext output) {
      if (ProfileId.Length != 0) {
        output.WriteRawTag(10);
        output.WriteString(ProfileId);
      }
      if (SdkUserId.Length != 0) {
        output.WriteRawTag(18);
        output.WriteString(SdkUserId);
      }
      if (SdkToken.Length != 0) {
        output.WriteRawTag(26);
        output.WriteString(SdkToken);
      }
      if (ChannelId != 0) {
        output.WriteRawTag(32);
        output.WriteInt32(ChannelId);
      }
      if (Nickname.Length != 0) {
        output.WriteRawTag(42);
        output.WriteString(Nickname);
      }
      if (Ip.Length != 0) {
        output.WriteRawTag(50);
        output.WriteString(Ip);
      }
      if (_unknownFields != null) {
        _unknownFields.WriteTo(ref output);
      }
    }
    #endif

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public int CalculateSize() {
      int size = 0;
      if (ProfileId.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(ProfileId);
      }
      if (SdkUserId.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(SdkUserId);
      }
      if (SdkToken.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(SdkToken);
      }
      if (ChannelId != 0) {
        size += 1 + pb::CodedOutputStream.ComputeInt32Size(ChannelId);
      }
      if (Nickname.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(Nickname);
      }
      if (Ip.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(Ip);
      }
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(PBPlayerBaseInfo other) {
      if (other == null) {
        return;
      }
      if (other.ProfileId.Length != 0) {
        ProfileId = other.ProfileId;
      }
      if (other.SdkUserId.Length != 0) {
        SdkUserId = other.SdkUserId;
      }
      if (other.SdkToken.Length != 0) {
        SdkToken = other.SdkToken;
      }
      if (other.ChannelId != 0) {
        ChannelId = other.ChannelId;
      }
      if (other.Nickname.Length != 0) {
        Nickname = other.Nickname;
      }
      if (other.Ip.Length != 0) {
        Ip = other.Ip;
      }
      _unknownFields = pb::UnknownFieldSet.MergeFrom(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(pb::CodedInputStream input) {
    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      input.ReadRawMessage(this);
    #else
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
        switch(tag) {
          default:
            _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, input);
            break;
          case 10: {
            ProfileId = input.ReadString();
            break;
          }
          case 18: {
            SdkUserId = input.ReadString();
            break;
          }
          case 26: {
            SdkToken = input.ReadString();
            break;
          }
          case 32: {
            ChannelId = input.ReadInt32();
            break;
          }
          case 42: {
            Nickname = input.ReadString();
            break;
          }
          case 50: {
            Ip = input.ReadString();
            break;
          }
        }
      }
    #endif
    }

    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    void pb::IBufferMessage.InternalMergeFrom(ref pb::ParseContext input) {
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
        switch(tag) {
          default:
            _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, ref input);
            break;
          case 10: {
            ProfileId = input.ReadString();
            break;
          }
          case 18: {
            SdkUserId = input.ReadString();
            break;
          }
          case 26: {
            SdkToken = input.ReadString();
            break;
          }
          case 32: {
            ChannelId = input.ReadInt32();
            break;
          }
          case 42: {
            Nickname = input.ReadString();
            break;
          }
          case 50: {
            Ip = input.ReadString();
            break;
          }
        }
      }
    }
    #endif

  }

  #endregion

}

#endregion Designer generated code
