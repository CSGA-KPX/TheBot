module KPX.EvePlugin.Utils.UserInventory

open System

open System.Collections.Concurrent

open KPX.FsCqHttp.Handler

open KPX.TheBot.Host.DataModel.Recipe

open KPX.EvePlugin.Data.EveType


// 此模块提供用户已有材料的缓存，用来配合#er和#err指令
// 暂不保存到数据库，并且暂时只有管理员能用，所以暂不引入System.Runtime.Caching缓存

type InventoryCollection private () =
    let col = ConcurrentDictionary<string, ItemAccumulator<EveType>>(StringComparer.OrdinalIgnoreCase)

    static member val Instance = InventoryCollection()

    member x.Contains(key) = col.ContainsKey(key)

    member x.Create(key) =
        let acc = ItemAccumulator<EveType>()

        if not <| col.TryAdd(key, acc) then
            raise <| ModuleException(ModuleError, "添加数据失败")

        key, acc

    member x.Create() =
        let guid = Guid.NewGuid().ToString("N")
        x.Create(guid)

    member x.TryGet(guid) =
        let succ, acc = col.TryGetValue(guid)
        if succ then Some(guid, acc) else None
