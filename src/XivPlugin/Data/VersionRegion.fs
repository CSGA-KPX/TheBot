namespace KPX.XivPlugin.Data

open LiteDB


[<RequireQualifiedAccess>]
///</summary>
type VersionRegion =
    | China
    | Offical

    override x.ToString() =
        match x with
        | China -> "china"
        | Offical -> "offical"

    member x.BsonValue = BsonValue(x.ToString())

type VersionRegionInstructions() =
    inherit KPX.TheBot.Host.PluginPrerunInstruction()

    override x.RunInstructions() =
        let fromBsonValue (value: BsonValue) =
            match value.AsString with
            | "china" -> VersionRegion.China
            | "offical" -> VersionRegion.Offical
            | str -> invalidArg (nameof value) $"Version值不合法：当前为{str}"

        BsonMapper.Global.RegisterType<VersionRegion>((fun x -> x.BsonValue), fromBsonValue)