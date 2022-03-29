namespace KPX.XivPlugin.Data

open System
open System.Collections.Generic

open KPX.XivPlugin.Data


type Race =
    { Name: string
      CanMale: bool
      CanFemale: bool }

module Race =
    let Races =
        [ let data = Dictionary<int, (string * bool * bool)>()

          for row in OfficalDistroData.GetCollection().Race do
              let name = row.Masculine.AsString()
              let canMale = row.``RSE{M}{Body}``.AsInt() <> 0
              let canFemale = row.``RSE{F}{Body}``.AsInt() <> 0
              data.Add(row.Key.Main, (name, canMale, canFemale))

          for row in ChinaDistroData.GetCollection().Race do
              let (_, m, f) = data.[row.Key.Main]
              data.[row.Key.Main] <- (row.Masculine.AsString().Trim('族'), m, f)

          for (n, m, f) in data.Values do
              if n <> String.Empty then
                  { Name = n; CanMale = m; CanFemale = f } ]

    let RaceCombinations =
        [ for r in Races do
              if r.CanMale then $"{r.Name}男"
              if r.CanFemale then $"{r.Name}女" ]
