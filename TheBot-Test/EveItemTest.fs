module EveItemTest

open BotData
open NUnit.Framework

[<OneTimeSetUp>]
let Setup () = InitCollection.Setup()

[<Test>]
let ``EVE : Type.getById`` () =
    let tc =
        EveData.EveType.EveTypeCollection.Instance

    let item = tc.GetById(34)
    Assert.AreEqual(item.Name, "三钛合金")

[<Test>]
let ``EVE : Type.getByName`` () =
    let tc =
        EveData.EveType.EveTypeCollection.Instance

    let ret = tc.TryGetByName("三钛合金")
    Assert.IsTrue(ret.IsSome)
    Assert.AreEqual(ret.Value.Id, 34)
    Assert.AreEqual(ret.Value.Name, "三钛合金")
