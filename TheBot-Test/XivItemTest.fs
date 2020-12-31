module XivItemTest

open KPX.TheBot.Data.XivData
open NUnit.Framework

[<OneTimeSetUp>]
let Setup () = InitCollection.Setup()

[<Test>]
let ``FFXIV: Item.GetById`` () =
    let item =
        ItemCollection.Instance.GetByItemId(4)

    Assert.AreEqual(item.Name, "风之碎晶")

[<Test>]
let ``FFXIV: Item.GetByName`` () =
    let ret =
        ItemCollection.Instance.TryGetByName("风之碎晶")

    Assert.IsTrue(ret.IsSome)
    Assert.AreEqual(ret.Value.Name, "风之碎晶")
    Assert.AreEqual(ret.Value.Id, 4)
