namespace TheBot.Module.EveModule.Utils.LpUtils

open TheBot.Module.EveModule.Utils.Config

type LpConfigParser() as x =
    inherit EveConfigParser()

    do
        x.RegisterOption("vol", "10")
        x.RegisterOption("val", "2000")
        x.RegisterOption("count", "50")

    member x.MinimalVolume = x.GetValue<float>("vol")
    member x.MinimalValue = x.GetValue<float>("val")

    member x.RecordCount =
        let ret = x.GetValue<uint32>("count") |> int
        if ret = 0 then System.Int32.MaxValue else ret
