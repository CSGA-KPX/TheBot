module MessageTest

open KPX.FsCqHttp.Message

open Expecto


let longCqCode = """[CQ:xml,data=<?xml version='1.0' encoding='UTF-8' standalone='yes'?><msg templateID="123" url="https://b23.tv/MhBKjc" serviceID="1" action="web" actionData="" a_actionData="" i_actionData="" brief="&#91;QQ小程序&#93;哔哩哔哩" flag="0"><item layout="2"><picture cover="https://external-30160.picsz.qpic.cn/a3fbc1ccf183f2d21dfb5e0d7f6a9a5c/jpg1"/><title>哔哩哔哩</title><summary>【FF14】忍者单刷极魔神+极邪龙</summary></item><source url="https://b23.tv/MhBKjc" icon="http://i.gtimg.cn/open/app_icon/00/95/17/76//100951776_100_m.png?t=1618385508" name="哔哩哔哩" appid="0" action="web" actionData="" a_actionData="tencent0://" i_actionData=""/></msg>,resid=1]"""

let sampleCQCode = """SomeTextTextText[CQ:image,file=33f6948b2c93ef23c7ddb7fc278e9bc5.image]"""

[<Tests>]
let msgTest =
    testList
        "MessageConvert"
        [ testCase "string_to_message"
          <| fun _ ->
              Message.FromCqString(sampleCQCode) |> printfn "%A"

              Message.FromCqString(longCqCode) |> printfn "%A"
          testCase "array_to_json" <| fun _ -> () ]