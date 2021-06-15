namespace KPX.FsCqHttp.Event

open System


type INoticeEventType =
    abstract Subtype : string 

/// 指示该类型是通知事件数据
[<AttributeUsage(AttributeTargets.Class, AllowMultiple = false)>]
type NoticeEventDataAttribute (typeName : string, ?subType : string) =
    inherit Attribute()
    
    member x.NoticeType = typeName
    
    member val Subtype = defaultArg subType ""
    
/// 指示该类型是消息事件数据
[<AttributeUsage(AttributeTargets.Class, AllowMultiple = false)>]
type MessageEventDataAttribute (typeName : string, ?subType : string) =
    inherit Attribute()
    
    member x.MessageType = typeName
    
    member val Subtype = defaultArg subType ""
    
/// 指示该类型是请求事件数据
[<AttributeUsage(AttributeTargets.Class, AllowMultiple = false)>]
type RequestEventDataAttribute (typeName : string, ?subType : string) =
    inherit Attribute()
    
    member x.RequestType = typeName
    
    member val Subtype = defaultArg subType ""
    
/// 指示该类型是消息事件数据
[<AttributeUsage(AttributeTargets.Class, AllowMultiple = false)>]
type MetaEventDataAttribute (typeName : string, ?subType : string) =
    inherit Attribute()
    
    member x.MetaEventType = typeName
    
    member val Subtype = defaultArg subType ""