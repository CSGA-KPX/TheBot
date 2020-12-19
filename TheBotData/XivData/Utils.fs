module BotData.XivData.Item.Utils

open LibFFXIV.GameData.Raw

let TryGetToOption (x : bool, y : 'Value) = if x then Some(y) else None
