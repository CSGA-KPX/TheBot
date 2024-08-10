namespace LibFFXIV.GameData.Provider

#nowarn "25"

open System.IO
open System.IO.Compression
open System.Collections.Generic
open System.Text
open System.Reflection

open ProviderImplementation.ProvidedTypes

open LibFFXIV.GameData
open LibFFXIV.GameData.Raw


[<Sealed>]
type ProviderContext(hdrCache: XivHeaderCache, id : string) =
    let subName =
        if System.String.IsNullOrWhiteSpace(id) then
            ""
        else
            $"{id}_"

    let mainNS = "LibFFXIV.GameData.Provided" // Collection type namespace
    let internalNS = "LibFFXIV.GameData.Provided" // Sheet and field namespace

    let asm = Assembly.GetExecutingAssembly()

    let mainCache = Dictionary<string, ProvidedTypeDefinition>()

    let internalCache = Dictionary<string, ProvidedTypeDefinition>()

    let mutable internalList = List.empty<_>

    member x.ProvideFor(tp: TypeProviderForNamespaces, typeName) =
        let tpType =
            if not <| mainCache.ContainsKey("MAIN") then
                mainCache.["MAIN"] <- x.CreateCollectionType(typeName)

            mainCache.["MAIN"]

        tp.AddNamespace(mainNS, [ tpType ])

        if internalList.Length <> internalCache.Count then
            internalList <- internalCache.Values |> Seq.toList

        tp.AddNamespace(internalNS, internalList)

        tpType

    member x.GetSheetType(shtName: string) =
        let key = $"{subName}Sheet_%s{shtName}"

        if not <| internalCache.ContainsKey(key) then
            internalCache.[key] <- x.CreateSheetType(shtName)

        internalCache.[key]

    member x.GetRowType(shtName: string) =
        let key = $"{subName}Row_%s{shtName}"

        if not <| internalCache.ContainsKey(key) then
            internalCache.[key] <- x.CreateRowType(shtName)

        internalCache.[key]

    member x.GetCellType(shtName: string, hdr: TypedHeaderItem) =
        let key = hdr.GetCacheKey(shtName)

        if not <| internalCache.ContainsKey(key) then
            internalCache.[key] <- x.CreateCellType(shtName, hdr)

        internalCache.[key]

    member private x.CreateCollectionType(typeName: string) =
        let tpType =
            ProvidedTypeDefinition(
                asm,
                mainNS,
                typeName,
                Some typeof<XivCollection>,
                hideObjectMethods = true,
                nonNullable = true
            )

        ProvidedConstructor(
            [ ProvidedParameter("col", typeof<XivCollection>) ],
            invokeCode = fun [ col ] -> <@@ (%%col: XivCollection) @@>
        )
        |> tpType.AddMember

        ProvidedConstructor(
            [ ProvidedParameter("lang", typeof<XivLanguage>)
              ProvidedParameter("zipPath", typeof<string>)
              ProvidedParameter("prefix", typeof<string>) ],
            invokeCode =
                fun [ lang; zipPath; prefix ] ->
                    <@@ let lang = (%%lang: XivLanguage)
                        let zipPath = (%%zipPath: string)
                        let file = File.Open(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read)
                        let archive = new ZipArchive(file, ZipArchiveMode.Read)
                        let prefix = (%%prefix: string)
                        new ZippedXivCollection(lang, archive, prefix) @@>
        )
        |> tpType.AddMember

        ProvidedConstructor(
            [ ProvidedParameter("lang", typeof<XivLanguage>)
              ProvidedParameter("stream", typeof<Stream>)
              ProvidedParameter("prefix", typeof<string>) ],
            invokeCode =
                fun [ lang; stream; prefix ] ->
                    <@@ let lang = (%%lang: XivLanguage)
                        let archive = new ZipArchive((%%stream: Stream), ZipArchiveMode.Read)
                        let prefix = (%%prefix: string)
                        new ZippedXivCollection(lang, archive, prefix) @@>
        )
        |> tpType.AddMember

        let deps = ResizeArray(hdrCache.Headers.Count)

        for shtName in hdrCache.Headers.Keys do
            let tySheetType = x.GetSheetType(shtName)
            deps.Add(tySheetType)

            let p =
                ProvidedProperty(
                    propertyName = shtName,
                    propertyType = tySheetType,
                    getterCode = (fun [ this ] -> <@@ (%%this: XivCollection).GetSheet(shtName) @@>)
                )

            tpType.AddMember p

        tpType

    member private x.CreateSheetType(shtName: string) =
        let tySheetType =
            ProvidedTypeDefinition(
                asm,
                internalNS,
                $"{subName}Sheet_%s{shtName}",
                Some typeof<XivSheetBase>,
                hideObjectMethods = true,
                nonNullable = true
            )

        let rowType = x.GetRowType(shtName)

        // Indexed properties
        ProvidedMethod(
            methodName = "Item",
            parameters = [ ProvidedParameter("key", typeof<XivKey>) ],
            returnType = rowType,
            invokeCode =
                (fun [ sheet; key ] ->
                    <@@ let key: XivKey = %%key
                        ((%%sheet: XivSheetBase) :?> XivSheet).[key] @@>)
        )
        |> tySheetType.AddMember

        ProvidedMethod(
            methodName = "Item",
            parameters = [ ProvidedParameter("key", typeof<int>) ],
            returnType = rowType,
            invokeCode =
                (fun [ sheet; key ] ->
                    <@@ let key: int = %%key
                        ((%%sheet: XivSheetBase) :?> XivSheet).[key] @@>)
        )
        |> tySheetType.AddMember

        // IEnumerable
        let untypedInt = typeof<System.Collections.IEnumerable>
        let untypedMethodInfo = untypedInt.GetMethod("GetEnumerator")

        let untypedMethodProvided =
            ProvidedMethod(
                methodName = "GetEnumerator",
                parameters = List.empty,
                returnType = typeof<System.Collections.IEnumerator>,
                invokeCode =
                    (fun [ sheet ] ->
                        <@@ let sheet = (%%sheet: XivSheet)
                            let ie = sheet :> System.Collections.IEnumerable
                            ie.GetEnumerator() @@>)
            )

        tySheetType.AddInterfaceImplementation(untypedInt)
        tySheetType.DefineMethodOverride(untypedMethodProvided, untypedMethodInfo)

        // IEnumerable<T>
        let typedInterface = ProvidedTypeBuilder.MakeGenericType(typedefof<IEnumerable<_>>, [ rowType ])
        let typedMethodInfo = typedInterface.GetMethod("GetEnumerator")

        let typedMethodProvided =
            ProvidedMethod(
                methodName = "GetEnumerator",
                parameters = List.empty,
                returnType = typedefof<IEnumerator<_>>.MakeGenericType rowType,
                invokeCode =
                    (fun [ sheet ] ->
                        <@@ let sheet = (%%sheet: XivSheet)
                            let ie = sheet :> IEnumerable<_>
                            ie.GetEnumerator() @@>)
            )

        tySheetType.AddInterfaceImplementation(typedInterface)
        tySheetType.DefineMethodOverride(typedMethodProvided, typedMethodInfo)

        tySheetType

    member private x.CreateRowType(shtName: string) =
        let tyRowType =
            ProvidedTypeDefinition(
                asm,
                internalNS,
                $"{subName}Row_%s{shtName}",
                Some typeof<XivRow>,
                hideObjectMethods = true,
                nonNullable = true
            )

        let mutable ret = List.empty<_>

        for hdr in hdrCache.Headers.[shtName] do
            let prop =
                match hdr with
                | TypedHeaderItem.NoName (colIdx, typeName) ->
                    let idx = colIdx.ToHdrIndex

                    let prop =
                        ProvidedProperty(
                            propertyName = $"RAW_%i{colIdx.ToRawIndex}",
                            propertyType = x.GetCellType(shtName, hdr),
                            getterCode = (fun [ row ] -> <@@ TypedCell((%%row: XivRow), idx) @@>)
                        )

                    prop.AddXmlDoc $"字段 %s{shtName}.[%i{colIdx.ToHdrIndex}] : %s{typeName}"

                    prop
                | TypedHeaderItem.Normal (colName, typeName) ->
                    let prop =
                        ProvidedProperty(
                            propertyName = colName,
                            propertyType = x.GetCellType(shtName, hdr),
                            getterCode = (fun [ row ] -> <@@ TypedCell((%%row: XivRow), colName) @@>)
                        )

                    prop.AddXmlDoc $"字段 %s{shtName}->%s{colName} : %s{typeName}\r\n\r\n{hdrCache.GetHint(shtName, colName)}"

                    prop
                | TypedHeaderItem.Array1D (name, tmpl, typeName, r0) ->
                    let f0, t0 = r0.From, r0.To

                    let prop =
                        ProvidedProperty(
                            propertyName = name,
                            propertyType = x.GetCellType(shtName, hdr),
                            getterCode = (fun [ row ] -> <@@ TypedArrayCell1D((%%row: XivRow), tmpl, f0, t0) @@>)
                        )

                    let doc =
                        StringBuilder() // 诡异：XmlDoc中\r\n无效，\r\n\r\n才能用
                            .AppendFormat("字段模板 {0}->{1} : {2}\r\n\r\n", shtName, tmpl, typeName)
                            .AppendFormat("范围 {0} -> {1}\r\n\r\n", r0.From, r0.To)
                            .Append($"{hdrCache.GetHint(shtName, tmpl)}")

                    prop.AddXmlDoc(doc.ToString())
                    prop
                | TypedHeaderItem.Array2D (name, tmpl, typeName, (r0, r1)) ->
                    let f0, t0 = r0.From, r0.To
                    let f1, t1 = r1.From, r1.To

                    let prop =
                        ProvidedProperty(
                            propertyName = name,
                            propertyType = x.GetCellType(shtName, hdr),
                            getterCode =
                                (fun [ row ] -> <@@ TypedArrayCell2D((%%row: XivRow), tmpl, f0, t0, f1, t1) @@>)
                        )

                    let doc =
                        StringBuilder() // 诡异：XmlDoc中\r\n无效，\r\n\r\n才能用
                            .AppendFormat("字段模板 {0}->{1} : {2}\r\n\r\n", shtName, tmpl, typeName)
                            .AppendFormat("范围0 : {0} -> {1}\r\n\r\n", r0.From, r0.To)
                            .AppendFormat("范围1 : {0} -> {1}\r\n\r\n", r1.From, r1.To)
                            .Append($"{hdrCache.GetHint(shtName, tmpl)}")

                    prop.AddXmlDoc(doc.ToString())
                    prop

            ret <- prop :: ret

        tyRowType.AddMembers ret

        tyRowType

    member private x.CreateCellType(shtName: string, hdr: TypedHeaderItem) =
        match hdr with
        | TypedHeaderItem.NoName (_, typeName) ->
            let cellType =
                ProvidedTypeDefinition(
                    asm,
                    internalNS,
                    hdr.GetCacheKey(shtName),
                    Some typeof<TypedCell>,
                    hideObjectMethods = true,
                    nonNullable = true
                )

            if hdrCache.Headers.ContainsKey(typeName) then
                cellType.AddMemberDelayed (fun () -> // this是最后一个，因为定义是empty所以只有一个this
                    ProvidedMethod(
                        methodName = "AsRow",
                        parameters = List.empty,
                        returnType = x.GetRowType(typeName),
                        invokeCode =
                            (fun [ cell ] -> // this是最后一个，因为定义是empty所以只有一个this
                                <@@ let cell = (%%cell: TypedCell)
                                    let key = cell.AsInt()

                                    let sht = cell.Row.Sheet.Collection.GetSheet(typeName)

                                    sht.[key] @@>)
                    ))

            cellType
        | TypedHeaderItem.Normal (_, typeName) ->
            let cellType =
                ProvidedTypeDefinition(
                    asm,
                    internalNS,
                    hdr.GetCacheKey(shtName),
                    Some typeof<TypedCell>,
                    hideObjectMethods = true,
                    nonNullable = true
                )

            if hdrCache.Headers.ContainsKey(typeName) then
                cellType.AddMemberDelayed (fun () -> // this是最后一个，因为定义是empty所以只有一个this
                    ProvidedMethod(
                        methodName = "AsRow",
                        parameters = List.empty,
                        returnType = x.GetRowType(typeName),
                        invokeCode =
                            (fun [ cell ] -> // this是最后一个，因为定义是empty所以只有一个this
                                <@@ let cell = (%%cell: TypedCell)
                                    let key = cell.AsInt()

                                    let sht = cell.Row.Sheet.Collection.GetSheet(typeName)

                                    sht.[key] @@>)
                    ))

            cellType
        | TypedHeaderItem.Array1D (_, _, typeName, _) ->
            let cellType =
                ProvidedTypeDefinition(
                    asm,
                    internalNS,
                    hdr.GetCacheKey(shtName),
                    Some typeof<TypedArrayCell1D>,
                    hideObjectMethods = true,
                    nonNullable = true
                )

            if hdrCache.Headers.ContainsKey(typeName) then
                cellType.AddMemberDelayed (fun () -> // this是最后一个，因为定义是empty所以只有一个this
                    ProvidedMethod(
                        methodName = "AsRows",
                        parameters = List.empty,
                        returnType = x.GetRowType(typeName).MakeArrayType(),
                        invokeCode =
                            (fun [ cell ] -> // this是最后一个，因为定义是empty所以只有一个this
                                <@@ let cell = (%%cell: TypedArrayCell1D)

                                    let sht = cell.Row.Sheet.Collection.GetSheet(typeName)

                                    cell.AsInts() |> Array.map (fun key -> sht.[key]) @@>)
                    ))

            cellType
        | TypedHeaderItem.Array2D (_, _, typeName, _) ->
            let cellType =
                ProvidedTypeDefinition(
                    asm,
                    internalNS,
                    hdr.GetCacheKey(shtName),
                    Some typeof<TypedArrayCell2D>,
                    hideObjectMethods = true,
                    nonNullable = true
                )

            if hdrCache.Headers.ContainsKey(typeName) then
                cellType.AddMemberDelayed (fun () -> // this是最后一个，因为定义是empty所以只有一个this
                    ProvidedMethod(
                        methodName = "AsRows",
                        parameters = List.empty,
                        returnType = x.GetRowType(typeName).MakeArrayType(2),
                        invokeCode =
                            (fun [ cell ] -> // this是最后一个，因为定义是empty所以只有一个this
                                <@@ let cell = (%%cell: TypedArrayCell2D)

                                    let sht = cell.Row.Sheet.Collection.GetSheet(typeName)

                                    cell.AsInts() |> Array2D.map (fun key -> sht.[key]) @@>)
                    ))

            cellType
