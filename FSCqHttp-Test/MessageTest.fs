module MessageTest

open NUnit.Framework

open KPX.FsCqHttp.Message


[<SetUp>]
let Setup () =
    ()

let longCqCode = """[CQ:xml,data=<?xml version='1.0' encoding='UTF-8' standalone='yes'?><msg templateID="123" url="https://b23.tv/MhBKjc" serviceID="1" action="web" actionData="" a_actionData="" i_actionData="" brief="&#91;QQĞ¡³ÌĞò&#93;ßÙÁ¨ßÙÁ¨" flag="0"><item layout="2"><picture cover="https://external-30160.picsz.qpic.cn/a3fbc1ccf183f2d21dfb5e0d7f6a9a5c/jpg1"/><title>ßÙÁ¨ßÙÁ¨</title><summary>¡¾FF14¡¿ÈÌÕßµ¥Ë¢¼«Ä§Éñ+¼«Ğ°Áú</summary></item><source url="https://b23.tv/MhBKjc" icon="http://i.gtimg.cn/open/app_icon/00/95/17/76//100951776_100_m.png?t=1618385508" name="ßÙÁ¨ßÙÁ¨" appid="0" action="web" actionData="" a_actionData="tencent0://" i_actionData=""/></msg>,resid=1]"""

let sampleCQCode = """asdasdasdsa[CQ:image,file=33f6948b2c93ef23c7ddb7fc278e9bc5.image]"""

[<Test>]
let ``string_to_message`` () =
    Assert.DoesNotThrow(fun () -> Message.FromCqString(sampleCQCode) |> printfn "%A")
    Assert.DoesNotThrow(fun () -> Message.FromCqString(longCqCode) |> printfn "%A")

[<Test>]
let ``array_to_json`` () =
    Assert.Pass()