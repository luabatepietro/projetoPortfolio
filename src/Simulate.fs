module PortfolioOptimization.Simulate

open System
open PortfolioOptimization.Types
open PortfolioOptimization.Portfolio

/// Para uma dada combinação (lista de índices de ativos), sorteia N vetores de pesos
/// válidos e retorna o MELHOR (maior Sharpe) — uma única carteira avaliada.
///
/// Esta é uma função PURA dado (tickers, returns, combo, config, seed):
///   mesma entrada => mesma saída, sem efeitos colaterais observáveis.
/// O Random é INTERNO à função e inicializado com seed derivada (combo + seedBase),
/// então o processo é determinístico e paralelizável com segurança.
let bestPortfolioForCombination
        (allTickers: string[])
        (allReturns: ReturnsMatrix)
        (config: SimConfig)
        (seedBase: int)
        (combo: int[]) : EvaluatedPortfolio option =

    // Seed determinística a partir dos índices escolhidos + seedBase.
    // Assim combos diferentes usam streams diferentes mas reprodutíveis.
    let seed =
        let mutable h = seedBase
        for i = 0 to combo.Length - 1 do
            h <- h * 31 + combo.[i]
        h

    let rng = Random(seed)
    let tickers = combo |> Array.map (fun i -> allTickers.[i])
    let sub = sliceColumns allReturns combo
    let means = meanReturns sub
    let cov = covarianceMatrix sub
    let n = combo.Length

    let mutable best : EvaluatedPortfolio option = None
    let mutable i = 0
    while i < config.SimulationsPerCombination do
        match tryRandomWeights rng n config.MaxWeight 20 with
        | None -> ()  // amostra rejeitada pela restrição de concentração
        | Some w ->
            let evaluated = evaluate tickers means cov w config.TradingDays
            match best with
            | None -> best <- Some evaluated
            | Some b when evaluated.Sharpe > b.Sharpe -> best <- Some evaluated
            | _ -> ()
        i <- i + 1
    best

/// Wrapper Async — mesmo padrão do exemplo `collatzWithSteps` fornecido:
/// envelopa o cálculo puro em um Async para orquestrar com Async.Parallel.
let bestPortfolioForCombinationAsync
        (allTickers: string[])
        (allReturns: ReturnsMatrix)
        (config: SimConfig)
        (seedBase: int)
        (combo: int[]) : Async<EvaluatedPortfolio option> =
    async {
        return bestPortfolioForCombination allTickers allReturns config seedBase combo
    }

/// Processa uma lista de combinações e retorna a MELHOR carteira global.
/// Pipeline funcional: gerar combos -> avaliar (paralelo ou sequencial) -> filtrar -> maximumBy.
let findBestPortfolio
        (allTickers: string[])
        (allReturns: ReturnsMatrix)
        (config: SimConfig) : EvaluatedPortfolio option =

    // Gera todas as combinações (ou um prefixo, se MaxCombinations setado).
    let combos =
        let allCombos = combinations config.TotalAssets config.NumAssetsInPortfolio
        match config.MaxCombinations with
        | Some limit -> allCombos |> Seq.truncate limit |> Seq.toArray
        | None -> allCombos |> Seq.toArray

    printfn "  Avaliando %d combinações × %d simulações de pesos = %s simulações..."
        combos.Length
        config.SimulationsPerCombination
        ((int64 combos.Length * int64 config.SimulationsPerCombination).ToString("N0"))

    let results : EvaluatedPortfolio option[] =
        if config.UseParallel then
            // Paralelismo via Async.Parallel (mesmo padrão do exemplo collatz).
            // Cada Async é uma função pura -> seguro para paralelizar.
            combos
            |> Array.map (bestPortfolioForCombinationAsync allTickers allReturns config config.Seed)
            |> Async.Parallel
            |> Async.RunSynchronously
        else
            // Caminho sequencial (para benchmark).
            combos
            |> Array.map (bestPortfolioForCombination allTickers allReturns config config.Seed)

    results
    |> Array.choose id
    |> Array.filter (fun p -> p.Sharpe > 0.0)   // carteiraValida
    |> fun arr ->
        if arr.Length = 0 then None
        else Some (arr |> Array.maxBy (fun p -> p.Sharpe))   // compararSharpe
