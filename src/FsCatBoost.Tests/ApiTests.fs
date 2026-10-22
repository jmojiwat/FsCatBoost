module ApiTests

open Faqt
open FsToolkit.ErrorHandling
open FsCatBoost.Api
open Xunit
open TestHelpers

[<Fact>]
let ``getSupportedEvaluatorTypes returns expected result`` () =
    result {
        use model = modelCalcerCreate ()
        
        let! supportedTypes = getSupportedEvaluatorTypes model
        supportedTypes.Should().Be([| CPU; GPU |]) |> ignore
    }
    |> assertOk
    