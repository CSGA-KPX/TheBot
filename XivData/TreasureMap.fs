module XivData.TreasureMap

open System
open System.Reflection
open System.IO
open System.Drawing
open System.Drawing.Imaging

let private nameMapping = 
    [|
        //复杂的放前面
        "G10", "13-"
        "G11", "17-"
        "G12", "18-"
        "隐藏", "7-"
        "绿图", "7-"
        "深层", "14-"
        "G1", "1-"
        "G2", "2-"
        "G3", "3-"
        "G4", "4-"
        "G5", "5-"
        "G6", "9-"
        "G7", "10-"
        "G8", "11-"
        "G9", "12-"
    |]

let private entries = 
    let archive = 
            let ResName = "XivData.treasuremap.zip"
            let assembly = Assembly.GetExecutingAssembly()
            let stream = assembly.GetManifestResourceStream(ResName)
            new IO.Compression.ZipArchive(stream, Compression.ZipArchiveMode.Read, false, Text.Encoding.GetEncoding("GBK"))
    [|
        for e in archive.Entries do 
            yield e.FullName, e
    |]

let private colorToMomochromeLoc(c : Color) : bool = 
    c.R >= 180uy
     && c.G >= 166uy
     && c.B >= 111uy

let private colorToMomochromeMap(c : Color) : bool = 
    c.R >= 180uy
     && c.G >= 138uy
     && c.B >= 70uy

let private DoCompare(loc : Bitmap, map : Bitmap) = 
    let centerOfMap = 
        let startX = (map.Width - loc.Width) / 2
        let startY = (map.Height - loc.Height) / 2
        let nb = new Bitmap(loc.Width, loc.Height)
        use g  = Graphics.FromImage(nb)
        let dstRect = Rectangle(0, 0, loc.Width, loc.Height)
        let srcRect = Rectangle((map.Width - loc.Width) / 2, (map.Height - loc.Height) / 2, loc.Width, loc.Height)
        g.DrawImage(map,  dstRect, srcRect, GraphicsUnit.Pixel)
        nb

    let score = 
        Seq.cast<Color*Color>
            (Array2D.init loc.Width loc.Height 
                         (fun n g -> (loc.GetPixel(n, g), centerOfMap.GetPixel(n, g))))
        |> Seq.fold 
                (fun i (l,m) -> 
                 if colorToMomochromeLoc(l) = colorToMomochromeMap(m) then
                    i + 1
                 else
                    i + 0) 0

    (float score)* 100.0 / (loc.Width * loc.Height |> float)

let Compare(map : Bitmap, msg : string) = 
    let needle = msg.ToUpperInvariant()
    let ret = nameMapping |> Array.tryFind (fun (x,_) -> needle.Contains(x))
    if ret.IsNone then failwithf "请提供宝图级别（G1-G12、隐藏、绿图、深层）"
    
    let start = snd ret.Value
    let maps = 
        entries
        |> Array.filter (fun (f, _) -> f.StartsWith(start))
    [|
        for (name, e) in maps do 
            use loc = new Bitmap(e.Open()) 
            yield name, DoCompare(loc, map)
    |]
    |> Array.sortByDescending snd
    |> Array.take 5