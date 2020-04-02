module XivData.LiteCachedSheet

open XivData.Utils
open LibFFXIV.GameData.Raw


[<CLIMutable>]
type HeaderCache = 
    {
        Id : string
        Items : XivHeaderItem []
    }

[<CLIMutable>]
type DataRowCache = 
    {
        Id : string
        Sheet : string
        Data : string []
    }

    static member private GetKey(row : XivRow) = 
        let sheet = row.Sheet.Name
        let main  = row.Key.Main
        let alt   = row.Key.Alt
        sprintf "%s:%i:%i" sheet main alt

    static member FromRow(row : XivRow) = 
        {
            Id = DataRowCache.GetKey(row)
            Sheet = row.Sheet.Name
            Data = row.RawData
        }

type CachedSheet() = 
    let x = 1