module XivData.TreasureMap

open System
open System.Reflection
open System.IO
open System.Drawing
open System.Drawing.Imaging
open CoenM.ImageHash

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
                    i - 1) 0

    (float score)* 100.0 / (loc.Width * loc.Height |> float)


let private grayMatrix = 
    [|
        [| 0.299f; 0.299f; 0.299f; 0.0f; 0.0f |] // R
        [| 0.587f; 0.587f; 0.587f; 0.0f; 0.0f |] // G
        [| 0.114f; 0.114f; 0.114f; 0.0f; 0.0f |] // B
        [| 0.0f; 0.0f; 0.0f; 1.0f; 0.0f|]        // A
        [| 0.0f; 0.0f; 0.0f; 0.0f; 1.0f|]        // W
    |] |> ColorMatrix

let colorToBrightness (c : Color) = 
    (c.R |> float) * 0.299 +
        (c.G |> float) * 0.587 +
        (c.B |> float) * 0.114
    

let private DoCompare2(loc : Bitmap, map : Bitmap) = 
    let centerOfMap (loc : Bitmap) = 
        let startX = (map.Width - loc.Width) / 2
        let startY = (map.Height - loc.Height) / 2
        let nb = new Bitmap(loc.Width, loc.Height)
        use g  = Graphics.FromImage(nb)
        let dstRect = Rectangle(0, 0, loc.Width, loc.Height)
        let srcRect = Rectangle((map.Width - loc.Width) / 2, (map.Height - loc.Height) / 2, loc.Width, loc.Height)
        g.DrawImage(map,  dstRect, srcRect, GraphicsUnit.Pixel)
        nb
    let resizeAndGrayBitmap (bmp: Bitmap) = 
        let size = Size(33,32)
        let output = new Bitmap(size.Width, size.Height)
        use g = Graphics.FromImage(output)
        g.InterpolationMode <- Drawing2D.InterpolationMode.High
        g.CompositingQuality <- Drawing2D.CompositingQuality.HighQuality
        g.SmoothingMode <- Drawing2D.SmoothingMode.AntiAlias
        g.DrawImage(bmp, Rectangle(0,0,size.Width, size.Height))

        output
        //new Bitmap(bmp, size)
        //use resized = new Bitmap(bmp, size)
        //let gray = new Bitmap(size.Width, size.Height)
        //use g = Graphics.FromImage(gray)
        //use attr = new ImageAttributes()
        //attr.SetColorMatrix(grayMatrix)
        //let rect = new Rectangle(0,0,size.Width, size.Height)
        //g.DrawImage(resized, rect, 0, 0 ,size.Width, size.Height, GraphicsUnit.Pixel, attr)
        //gray

    let dHash (bmp : Bitmap) = 
        let sb = Text.StringBuilder()
        for row = 0 to bmp.Width - 1 do 
            for col = 0 to bmp.Height - 2 do 
                let cur = bmp.GetPixel(row, col) |> colorToBrightness
                let nxt = bmp.GetPixel(row, col+1) |> colorToBrightness
                if cur > nxt then
                    sb.Append("1") |> ignore
                else
                    sb.Append("0") |> ignore
        
        sb.ToString().ToCharArray()

    let gsLoc = loc |> centerOfMap |> resizeAndGrayBitmap |> dHash
    let gsMap = map |> resizeAndGrayBitmap |> dHash

    let distance =
        Array.map2 (fun a b -> if a = b then 1 else 0) gsLoc gsMap
        |> Array.sum
    
    distance

let private DoCompare3(user : Stream, map : Stream) = 
    let hasher = HashAlgorithms.PerceptualHash()
    user.Position <- 0L

    let usrHash = hasher.Hash(user)
    let mapHash = hasher.Hash(map)
    CompareHash.Similarity(usrHash, mapHash)
    

let Compare(loc : Stream, msg : string) = 
    let needle = msg.ToUpperInvariant()
    let ret = nameMapping |> Array.tryFind (fun (x,_) -> needle.Contains(x))
    if ret.IsNone then failwithf "请提供宝图级别（G1-G12、隐藏、绿图、深层）"
    
    let start = snd ret.Value
    let maps = 
        entries
        |> Array.filter (fun (f, _) -> f.StartsWith(start))
    [|
        for (name, e) in maps do 
            yield name, DoCompare3(loc, e.Open())
    |]
    |> Array.sortByDescending snd
    |> Array.take 5