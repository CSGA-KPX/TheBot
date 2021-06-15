namespace KPX.FsCqHttp.Event.Notice

open Newtonsoft.Json


/// 群文件信息
type GroupFile =
    { [<JsonProperty("id")>]
      Id : string
      [<JsonProperty("name")>]
      Name : string
      [<JsonProperty("size")>]
      Size : int64
      ///用途不明
      [<JsonProperty("busid")>]
      BusId : int64 }