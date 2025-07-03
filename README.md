# FsCatBoost
An F# thin wrapper around CatBoost API.

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
