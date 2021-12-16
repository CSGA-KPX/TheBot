namespace rec KPX.XivPlugin.DataModel

open KPX.TheBot.Host.Data
open KPX.TheBot.Host.DataCache



[<CLIMutable>]
type XivItem =
    { [<LiteDB.BsonId(false)>]
      Id: int
      Name: string }

    override x.ToString() = $"%s{x.Name}(%i{x.Id})"

    static member GetUnknown() = { Id = -1; Name = "Unknown" }





