module KPX.TheBot.Data.XivData.Item.Utils


let TryGetToOption (x : bool, y : 'Value) = if x then Some(y) else None
