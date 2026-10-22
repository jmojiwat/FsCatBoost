# FsCatBoost
An F# thin wrapper around CatBoost API.

Wraps the CatBoost model applier C API (loading `.cbm` models and making predictions). The CatBoost 1.2.10 native library is bundled in the NuGet package for `win-x64`, `linux-x64`, `linux-arm64` and macOS (`x64`/`arm64`). The native libraries aren't committed to this repository; building from source downloads them from the [CatBoost release](https://github.com/catboost/catboost/releases/tag/v1.2.10) on the first build and verifies their SHA-256.

Example taken from [CatBoostNetTests](https://github.com/catboost/catboost/blob/master/catboost/dotnet/CatBoostNetTests/CatBoostModelEvaluatorTest.cs).

```fsharp
result {
    let dsPath = Path.Combine(CatBoostNet.directory, @"mushrooms\mushrooms.csv")
    let modelPath = Path.Combine(CatBoostNet.directory, @"mushrooms\mushroom_model.cbm")
            
    let df = Frame.ReadCsv(dsPath, hasHeaders = false)
    let target =
        df.Rows.Select(_.Value["Column1"]).Values
        |> Seq.map string
        |> Seq.toArray

    df.DropColumn("Column1")
    let data = df.ToArray2D<string>()
    let floatFeatures = Array2D.zeroCreate (data.GetLength(0)) 0 
            
    use! model =
        modelCalcerCreate ()
        |> loadFullModelFromFile modelPath
            
    let! res = calcModelPrediction floatFeatures data model
            
    let targetLabelList = [| "e"; "p" |]
            
    [| 0 .. res.GetLength(0) - 1 |]
    |> Array.iter (fun i ->
        let argmax = if res[i, 0] > 0 then 1 else 0
        let predLabel = targetLabelList[argmax]
                
        predLabel.Should().Be(target[i], $"Mushroom test crashed on sample {i + 1}") |> ignore)
}
|> Result.mapError (fun e ->
    output.WriteLine(e)
    Assert.Fail())
```
