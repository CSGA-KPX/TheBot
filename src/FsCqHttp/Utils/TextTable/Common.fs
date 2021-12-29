namespace KPX.FsCqHttp.Utils.TextResponse


type ResponseType =
    | ForceImage
    | PreferImage
    | ForceText

[<Struct>]
[<RequireQualifiedAccess>]
type TextAlignment =
    | Left
    | Right