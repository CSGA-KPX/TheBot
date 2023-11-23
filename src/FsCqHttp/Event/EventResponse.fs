namespace KPX.FsCqHttp.Event

open System

open Newtonsoft.Json

open KPX.FsCqHttp
open KPX.FsCqHttp.Message


/// 快速操作类型
type EventResponse =
    | EmptyResponse
    | PrivateMessageResponse of uid : UserId * reply: ReadOnlyMessage
    | GroupMessageResponse of gid : GroupId * reply: ReadOnlyMessage 
    | FriendAddResponse of approve: bool * remark: string
    | GroupAddResponse of approve: bool * reason: string