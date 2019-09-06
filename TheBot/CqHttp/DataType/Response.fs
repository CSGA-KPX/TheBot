namespace KPX.TheBot.WebSocket.DataType.Response
open KPX.TheBot.WebSocket.DataType
open System
open System.IO
open System.Text
open Newtonsoft.Json

[<JsonConverter(typeof<ResponseConverter>)>]
type Response =
    | EmptyResponse
    | PrivateMessageResponse of reply : Message.Message
    | GroupMessageResponse of reply : Message.Message * at_sender : bool * delete :bool * kick : bool * ban : bool * ban_duration : int
    | DiscusMessageResponse of reply : Message.Message * at_sender : bool
    | FriendAddResponse of approve : bool * remark : string
    | GroupAddResponse of approve : bool * reason : string
and ResponseConverter() =
    inherit JsonConverter<Response>()

    override x.WriteJson(w:JsonWriter , r : Response, js:JsonSerializer) =
        let sb = new StringBuilder()
        let sw = new StringWriter(sb)
        //w.WriteStartObject()
        for prop in r.GetType().GetProperties() do
            if Char.IsLower(prop.Name.[0]) then
                w.WritePropertyName(prop.Name)
                js.Serialize(w, prop.GetValue(r))
        //w.WriteEndObject()

    override x.ReadJson(r : JsonReader, objType : Type, existingValue : Response, hasExistingValue : bool, s : JsonSerializer) =
        raise<Response> <| NotImplementedException()