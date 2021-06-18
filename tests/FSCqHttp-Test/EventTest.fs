module EventTest

open KPX.FsCqHttp.Event

open Expecto
open Newtonsoft.Json
open Newtonsoft.Json.Linq

let testGroupMessageJson = """{
  "anonymous": null,
  "font": 0,
  "group_id": 123456789,
  "message": [
    {
      "data": {
        "text": "#eat"
      },
      "type": "text"
    }
  ],
  "message_id": 266698,
  "message_seq": 266698,
  "message_type": "group",
  "post_type": "message",
  "raw_message": "#eat",
  "self_id": 987654321,
  "sender": {
    "age": 0,
    "area": "",
    "card": "群名片",
    "level": "",
    "nickname": "昵称",
    "role": "member",
    "sex": "unknown",
    "title": "群头衔",
    "user_id": 13800138000
  },
  "sub_type": "normal",
  "time": 1619668470,
  "user_id": 13800138000
}
"""

let testPrivateMessage = """{
  "font": 0,
  "message": [
    {
      "data": {
        "text": "SAMPLE"
      },
      "type": "text"
    }
  ],
  "message_id": 12450,
  "message_type": "private",
  "post_type": "message",
  "raw_message": "SAMPLE",
  "self_id": 987654321,
  "sender": {
    "age": 0,
    "nickname": "昵称",
    "sex": "unknown",
    "user_id": 123456789
  },
  "sub_type": "friend",
  "time": 1620214324,
  "user_id": 123456789
}
"""

[<Tests>]
let messageEventTest =
    testList
        "MessageEventTest"
        [ testCase "GroupMessageTest"
          <| fun _ ->
              let str =
                  JObject
                      .Parse(testGroupMessageJson)
                      .ToObject<MessageEvent>()
                  |> JsonConvert.SerializeObject

              let str2 =
                  JObject.Parse(str).ToObject<MessageEvent>()
                  |> JsonConvert.SerializeObject

              Expect.equal str str2 ""
          testCase "PrivateMessageTest"
          <| fun _ ->
              let str =
                  JObject
                      .Parse(testPrivateMessage)
                      .ToObject<MessageEvent>()
                  |> JsonConvert.SerializeObject

              let str2 =
                  JObject.Parse(str).ToObject<MessageEvent>()
                  |> JsonConvert.SerializeObject

              Expect.equal str str2 "" ]
