namespace KPX.FsCqHttp.OneBot.V12.Meta

open Newtonsoft.Json
open KPX.FsCqHttp.OneBot.V12


type GetLatestEvents(?limit: int64, ?timeoutSec: int64) =
    inherit Request<RawEvent[]>("")

    override x.GetRequestObj() =
        {| limit = defaultArg limit 0L
           timtout = defaultArg timeoutSec 0L |}

type GetSupportedActions() =
    inherit Request<string[]>("get_supported_actions")

    override x.GetRequestObj() = obj ()

type GetStatus() =
    inherit Request<OnebotStatus>("get_status")

    override x.GetRequestObj() = obj ()

type BotVersion =
    { [<JsonProperty("impl")>]
      Impl: string
      [<JsonProperty("version")>]
      Version: string
      [<JsonProperty("onebot_version")>]
      OneBotVersion: string }

type GetVersion() =
    inherit Request<BotVersion>("get_version")

    override x.GetRequestObj() = obj ()
