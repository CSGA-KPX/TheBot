module XivItemTest

open BotData
open NUnit.Framework

[<OneTimeSetUp>]
let Setup () = InitCollection.Setup()

[<Test>]
let ``FFXIV: Item.GetById`` () =
    let item = XivData.Item.ItemCollection.Instance.GetByItemId(4)
    Assert.AreEqual(item.Name, "风之碎晶")

[<Test>]
let ``FFXIV: Item.GetByName`` () =
    let ret = XivData.Item.ItemCollection.Instance.TryGetByName("风之碎晶")
    Assert.IsTrue(ret.IsSome)
    Assert.AreEqual(ret.Value.Name, "风之碎晶")
    Assert.AreEqual(ret.Value.Id, 4)