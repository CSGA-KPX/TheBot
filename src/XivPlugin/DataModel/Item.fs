namespace KPX.XivPlugin.DataModel

open KPX.TheBot.Host.Data
open KPX.TheBot.Host.DataCache
open KPX.TheBot.Host.DataCache.LiteDb


[<CLIMutable>]
type XivItem =
    { [<LiteDB.BsonId>]
      Id: int
      ItemId : int
      Region : VersionRegion
      Name: string }

    override x.ToString() = $"%A{x.Region}/%s{x.Name}(%i{x.Id})"