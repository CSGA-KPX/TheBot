namespace KPX.FsCqHttp.OneBot.V12

open System
open System.Collections.Generic
open Newtonsoft.Json

open FSharp.Reflection


[<Struct>]
[<JsonConverter(typeof<SingleCaseInlineConverter<UserId>>)>]
type UserId =
    | UserId of string

    static member Zero = UserId "UserID_Null"

    member x.Value =
        let (UserId value) = x
        value

[<Struct>]
[<JsonConverter(typeof<SingleCaseInlineConverter<GroupId>>)>]
type GroupId =
    | GroupId of string

    static member Zero = GroupId "GroupId_0"

    member x.Value =
        let (GroupId value) = x
        value

[<Struct>]
[<JsonConverter(typeof<SingleCaseInlineConverter<EventId>>)>]
type EventId =
    | EventId of string

    static member Zero = EventId "EventId_0"

    member x.Value =
        let (EventId value) = x
        value

[<Struct>]
[<JsonConverter(typeof<SingleCaseInlineConverter<Echo>>)>]
type Echo =
    | Echo of string

    static member Create() = Echo(Guid.NewGuid().ToString())

    member x.Value =
        let (Echo value) = x
        value

type BotSelf =
    { [<JsonProperty("platform")>]
      Platform: string
      [<JsonProperty("user_id")>]
      UserId: UserId }

/// 基本事件
[<Struct>]
[<JsonConverter(typeof<StringEnumConverter<BaseEventType>>)>]
type BaseEventType =
    | Meta
    | Message
    | Notice
    | Request

type EventTypeAttribute(main, detail, sub) =
    inherit Attribute()

    member x.MainType: string = main
    member x.DetailType: string = detail
    member x.SubType: string = sub

    override x.ToString() = $"{x.MainType}.{x.DetailType}.{x.SubType}"

type Raw = { Data: Linq.JObject }

type RawEvent = Event<Raw>

and Event<'T> =
    { Id: EventId
      Time: DateTimeOffset
      Type: BaseEventType
      [<JsonProperty("detail_type")>]
      DetailType: string
      [<JsonProperty("sub_type")>]
      SubType: string }

type internal Request =
    { Action: string
      Params: obj
      Echo: Echo
      Self: BotSelf }

[<AbstractClass>]
type Request<'RetType>(action: string) =

    member x.Action = action

    abstract GetRequestObj: unit -> obj

    abstract ProcessResponse : Linq.JObject -> 'RetType

    default x.ProcessResponse (obj) = obj.ToObject<'RetType>()

    // 感觉应该是连接层面做的事情
    member x.DoRequest(self : BotSelf) =
        let obj = 
            {
                Action = action
                Params = x.GetRequestObj()
                Echo = Echo.Create()
                Self = self
            }

        // 根据输入blabalbla

        // 处理Response

        // 解码为'RetType
        x.ProcessResponse(raise <| NotImplementedException())



[<Struct>]
[<JsonConverter(typeof<StringEnumConverter<StatusType>>)>]
type StatusType =
    | Ok
    | Failed

[<Struct>]
[<JsonConverter(typeof<SingleCaseInlineConverter<RetCode>>)>]
type RetCode =
    | RetCode of int64

    member x.Value =
        let (RetCode value) = x
        value

type Response<'T> =
    { Status: StatusType
      RetCode: RetCode
      Data: 'T
      Message: string
      Echo: Echo option }

[<Struct>]
[<JsonConverter(typeof<SingleCaseInlineConverter<FileId>>)>]
type FileId =
    | FileId of string

    static member Create() = FileId(Guid.NewGuid().ToString())

    member x.Value =
        let (FileId value) = x
        value


/// 对OneBot中MessageId的包装
[<Struct>]
[<JsonConverter(typeof<SingleCaseInlineConverter<MessageId>>)>]
type MessageId =
    | MessageId of string

    member x.Value =
        let (MessageId value) = x
        value