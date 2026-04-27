module PortfolioOptimization.Program

open System
open System.Diagnostics
open PortfolioOptimization.Types
open PortfolioOptimization.DataLoader
open PortfolioOptimization.Portfolio
open PortfolioOptimization.Simulate

/// Argumentos de linha de comando (simples key=value).
type CliArgs = {
    Mode: string            // "sample" | "benchmark" | "full"
    MaxCombos: int          // quantas combinações avaliar (override)
    SimsPerCombo: int       // sorteios de pesos por combinação
    Seed: int
    Parallel: bool
    BenchmarkRuns: int
}

let defaultArgs = {
    Mode = "sample"
    MaxCombos = 500
    SimsPerCombo = 2000
    Seed = 42
    Parallel = true
    BenchmarkRuns = 5
}

let parseArgs (argv: string[]) : CliArgs =
    argv
    |> Array.fold (fun acc arg ->
        match arg.Split('=', 2) with
        | [| "--mode"; v |]       -> { acc with Mode = v }
        | [| "--max-combos"; v |] -> { acc with MaxCombos = Int32.Parse v }
        | [| "--sims"; v |]       -> { acc with SimsPerCombo = Int32.Parse v }
        | [| "--seed"; v |]       -> { acc with Seed = Int32.Parse v }
        | [| "--parallel"; v |]   -> { acc with Parallel = Boolean.Parse v }
        | [| "--runs"; v |]       -> { acc with BenchmarkRuns = Int32.Parse v }
        | _ -> acc) defaultArgs

let printPortfolio (p: EvaluatedPortfolio) =
    printfn ""
    printfn "  ════════════════════════════════════════════════════════════════"
    printfn "  MELHOR CARTEIRA ENCONTRADA"
    printfn "  ════════════════════════════════════════════════════════════════"
    printfn "  Retorno anualizado    : %7.2f%%"     (p.AnnualReturn * 100.0)
    printfn "  Volatilidade anualiz. : %7.2f%%"     (p.AnnualVolatility * 100.0)
    printfn "  Sharpe Ratio          : %7.4f"       p.Sharpe
    printfn "  Ativos (%d) e pesos:" p.Tickers.Length
    let pairs =
        Array.zip p.Tickers p.Weights
        |> Array.sortByDescending snd
    for (t, w) in pairs do
        let bars = String.replicate (int (w * 100.0)) "█"
        printfn "    %-6s  %6.2f%%  %s" t (w * 100.0) bars
    let total = Array.sum p.Weights
    printfn "  Soma dos pesos        : %7.4f" total
    printfn "  ════════════════════════════════════════════════════════════════"

let runOnce (config: SimConfig) (tickers: string[]) (returns: ReturnsMatrix) =
    let sw = Stopwatch.StartNew()
    let best = findBestPortfolio tickers returns config
    sw.Stop()
    best, sw.Elapsed.TotalSeconds

let runSample (args: CliArgs) =
    printfn ""
    printfn "╔══════════════════════════════════════════════════════════════════╗"
    printfn "║       PORTFOLIO OPTIMIZATION — Sample Run (F# / .NET 8)         ║"
    printfn "╚══════════════════════════════════════════════════════════════════╝"
    printfn ""
    printfn "Configuração:"
    printfn "  Modo               : %s" args.Mode
    printfn "  Paralelo           : %b" args.Parallel
    printfn "  Max combinações    : %d  (C(30,20) = 30.045.015 no total)" args.MaxCombos
    printfn "  Simulações/combo   : %s" (args.SimsPerCombo.ToString("N0"))
    printfn "  Seed               : %d" args.Seed
    printfn "  Cores disponíveis  : %d" Environment.ProcessorCount
    printfn ""

    printfn "Carregando retornos reais do Dow Jones de data/raw/..."
    let returns = loadDowJonesReturns "data/raw"
    printfn "  Matriz carregada: %d dias × %d ativos (retornos diários)" returns.Length dowJonesTickers.Length

    let config = {
        NumAssetsInPortfolio = 20
        TotalAssets = 30
        SimulationsPerCombination = args.SimsPerCombo
        MaxWeight = 0.20
        MaxCombinations = Some args.MaxCombos
        TradingDays = 252.0
        UseParallel = args.Parallel
        Seed = args.Seed
    }

    let totalSims = int64 args.MaxCombos * int64 args.SimsPerCombo
    printfn "  Total de simulações que serão executadas: %s" (totalSims.ToString("N0"))
    printfn ""
    printfn "Rodando..."

    let best, elapsed = runOnce config dowJonesTickers returns

    printfn ""
    printfn "Tempo total: %.2f s" elapsed

    match best with
    | Some p -> printPortfolio p
    | None ->
        printfn ""
        printfn "  Nenhuma carteira válida encontrada (tente aumentar --sims)."

let runBenchmark (args: CliArgs) =
    printfn ""
    printfn "╔══════════════════════════════════════════════════════════════════╗"
    printfn "║       PORTFOLIO OPTIMIZATION — Benchmark (paralelo vs seq)      ║"
    printfn "╚══════════════════════════════════════════════════════════════════╝"
    printfn ""
    printfn "Executando %d rodadas em cada modo..." args.BenchmarkRuns
    printfn ""

    let returns = loadDowJonesReturns "data/raw"
    let baseConfig = {
        NumAssetsInPortfolio = 20
        TotalAssets = 30
        SimulationsPerCombination = args.SimsPerCombo
        MaxWeight = 0.20
        MaxCombinations = Some args.MaxCombos
        TradingDays = 252.0
        UseParallel = true
        Seed = args.Seed
    }

    let runs parallelFlag =
        [| for i in 1 .. args.BenchmarkRuns ->
            let cfg = { baseConfig with UseParallel = parallelFlag }
            let _, t = runOnce cfg dowJonesTickers returns
            printfn "  [%s] run %d: %.2f s" (if parallelFlag then "PAR" else "SEQ") i t
            t |]

    printfn "→ Modo SEQUENCIAL:"
    let seqTimes = runs false
    printfn ""
    printfn "→ Modo PARALELO:"
    let parTimes = runs true

    let avg (xs: float[]) = Array.average xs
    let mn  (xs: float[]) = Array.min xs
    let mx  (xs: float[]) = Array.max xs

    printfn ""
    printfn "  ════════════════════════════════════════════════════════════════"
    printfn "  RESULTADOS (%d runs cada, %d combos × %d sims)" args.BenchmarkRuns args.MaxCombos args.SimsPerCombo
    printfn "  ════════════════════════════════════════════════════════════════"
    printfn "  Sequencial : média = %.2f s  | min = %.2f s  | max = %.2f s" (avg seqTimes) (mn seqTimes) (mx seqTimes)
    printfn "  Paralelo   : média = %.2f s  | min = %.2f s  | max = %.2f s" (avg parTimes) (mn parTimes) (mx parTimes)
    printfn "  Speedup    : %.2fx" (avg seqTimes / avg parTimes)
    printfn "  ════════════════════════════════════════════════════════════════"

[<EntryPoint>]
let main argv =
    let args = parseArgs argv
    match args.Mode with
    | "benchmark" -> runBenchmark args
    | _ -> runSample args
    0
