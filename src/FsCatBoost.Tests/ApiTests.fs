module ApiTests

open Faqt
open FsToolkit.ErrorHandling
open FsCatBoost.Api
open Xunit

type Test(output: ITestOutputHelper) =

    [<Fact>]
    member _.``getSupportedEvaluatorTypes returns expected result`` () =
        result {
            use model = modelCalcerCreate ()
            
            let! supportedTypes = getSupportedEvaluatorTypes model
            supportedTypes.Should().Be([| CPU; GPU |]) |> ignore
        }
        |> function
            | Ok _ -> ()
            | Error e ->
                output.WriteLine(e)
                Assert.Fail()
        