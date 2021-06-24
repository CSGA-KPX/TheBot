namespace rec KPX.FsCqHttp.Event.Message

open Newtonsoft.Json

open KPX.FsCqHttp


[<CLIMutable>]
type AnonymousUser =
    { /// 匿名用户 ID
      [<JsonProperty("id")>]
      Id : UserId
      /// 匿名用户名称
      [<JsonProperty("name")>]
      Name : string
      /// 匿名用户 flag，在调用禁言 API 时需要传入
      [<JsonProperty("flag")>]
      Flag : string }

type AnonymousUserOptionConverter() =
    inherit JsonConverter<AnonymousUser option>()

    override this.WriteJson
        (
            writer : JsonWriter,
            value : AnonymousUser option,
            serializer : JsonSerializer
        ) : unit =
        if value.IsNone then
            writer.WriteNull()
        else
            serializer.Serialize(writer, value.Value)

    override this.ReadJson(reader, _, _, _, serializer) =
        let obj =
            serializer.Deserialize<AnonymousUser>(reader)

        if isNull <| box obj then None else Some obj
