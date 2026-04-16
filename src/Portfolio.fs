module PortfolioOptimization.Portfolio

open PortfolioOptimization.Types

/// Seleciona um subconjunto de colunas (ativos) da matriz de retornos.
/// Função PURA: retorna nova matriz.
let sliceColumns (returns: ReturnsMatrix) (indices: int[]) : ReturnsMatrix =
    let k = indices.Length
    returns
    |> Array.map (fun row ->
        let sub = Array.zeroCreate k
        for j = 0 to k - 1 do
            sub.[j] <- row.[indices.[j]]
        sub)

/// Calcula o vetor de retornos médios diários (um por ativo).
/// Função PURA.
let meanReturns (returns: ReturnsMatrix) : float[] =
    if returns.Length = 0 then [||]
    else
        let nDays = returns.Length
        let nAssets = returns.[0].Length
        let acc = Array.zeroCreate nAssets
        for d = 0 to nDays - 1 do
            let row = returns.[d]
            for j = 0 to nAssets - 1 do
                acc.[j] <- acc.[j] + row.[j]
        for j = 0 to nAssets - 1 do
            acc.[j] <- acc.[j] / float nDays
        acc

/// Calcula matriz de covariância (amostral, divisor n-1) dos retornos.
/// Função PURA.
let covarianceMatrix (returns: ReturnsMatrix) : CovMatrix =
    let nDays = returns.Length
    if nDays < 2 then failwith "Precisa de pelo menos 2 dias para covariância"
    let nAssets = returns.[0].Length
    let means = meanReturns returns
    let cov = Array.init nAssets (fun _ -> Array.zeroCreate nAssets)
    for d = 0 to nDays - 1 do
        let row = returns.[d]
        for i = 0 to nAssets - 1 do
            let devI = row.[i] - means.[i]
            for j = i to nAssets - 1 do
                cov.[i].[j] <- cov.[i].[j] + devI * (row.[j] - means.[j])
    let denom = float (nDays - 1)
    for i = 0 to nAssets - 1 do
        for j = i to nAssets - 1 do
            let v = cov.[i].[j] / denom
            cov.[i].[j] <- v
            cov.[j].[i] <- v
    cov

/// Retorno anualizado da carteira dados retornos médios diários e pesos.
/// r_p = mean(r) . w  ;  anualizado => * 252.
/// Função PURA.
let annualizedReturn (dailyMeans: float[]) (weights: Weights) (tradingDays: float) : float =
    let mutable acc = 0.0
    for i = 0 to weights.Length - 1 do
        acc <- acc + dailyMeans.[i] * weights.[i]
    acc * tradingDays

/// Volatilidade anualizada da carteira: sqrt(w^T C w) * sqrt(252).
/// Função PURA.
let annualizedVolatility (cov: CovMatrix) (weights: Weights) (tradingDays: float) : float =
    let n = weights.Length
    let mutable s = 0.0
    for i = 0 to n - 1 do
        let wi = weights.[i]
        let row = cov.[i]
        let mutable inner = 0.0
        for j = 0 to n - 1 do
            inner <- inner + row.[j] * weights.[j]
        s <- s + wi * inner
    sqrt (max 0.0 s) * sqrt tradingDays

/// Sharpe ratio (risk-free = 0, conforme enunciado: pode desconsiderar para efeito de comparação).
/// Função PURA.
let sharpeRatio (annRet: float) (annVol: float) : float =
    if annVol <= 0.0 then 0.0 else annRet / annVol

/// Gera um vetor de pesos aleatório satisfazendo soma=1, w>=0, w<=maxWeight.
/// Usa distribuição uniforme com rejection por concentração.
/// Função (quase-)PURA: é determinística dado o Random passado.
/// Retorna None se após algumas tentativas não encontrar peso válido.
let tryRandomWeights (rng: System.Random) (n: int) (maxWeight: float) (maxAttempts: int) : Weights option =
    let rec attempt k =
        if k >= maxAttempts then None
        else
            let raw = Array.init n (fun _ -> rng.NextDouble())
            let mutable sum = 0.0
            for i = 0 to n - 1 do sum <- sum + raw.[i]
            if sum <= 0.0 then attempt (k + 1)
            else
                let w = Array.init n (fun i -> raw.[i] / sum)
                let mutable maxW = 0.0
                for i = 0 to n - 1 do
                    if w.[i] > maxW then maxW <- w.[i]
                if maxW <= maxWeight then Some w
                else attempt (k + 1)
    attempt 0

/// Avalia uma carteira: calcula retorno, volatilidade e Sharpe.
/// Função PURA.
let evaluate
        (tickers: string[])
        (dailyMeans: float[])
        (cov: CovMatrix)
        (weights: Weights)
        (tradingDays: float) : EvaluatedPortfolio =
    let ret = annualizedReturn dailyMeans weights tradingDays
    let vol = annualizedVolatility cov weights tradingDays
    {
        Tickers = tickers
        Weights = weights
        AnnualReturn = ret
        AnnualVolatility = vol
        Sharpe = sharpeRatio ret vol
    }

/// Gera TODAS as combinações C(n,k) como arrays de índices (0-based).
/// Função PURA.
let combinations (n: int) (k: int) : seq<int[]> =
    seq {
        let idx = Array.init k id
        let mutable running = true
        yield Array.copy idx
        while running do
            // Encontra o maior i tal que idx.[i] pode avançar
            let mutable i = k - 1
            while i >= 0 && idx.[i] = n - k + i do
                i <- i - 1
            if i < 0 then running <- false
            else
                idx.[i] <- idx.[i] + 1
                for j = i + 1 to k - 1 do
                    idx.[j] <- idx.[j - 1] + 1
                yield Array.copy idx
    }

/// Conta combinações C(n,k).
let countCombinations (n: int) (k: int) : int64 =
    let mutable result = 1L
    let kk = min k (n - k)
    for i = 0 to kk - 1 do
        result <- result * int64 (n - i) / int64 (i + 1)
    result
