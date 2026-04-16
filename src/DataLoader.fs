module PortfolioOptimization.DataLoader

open System
open System.IO
open PortfolioOptimization.Types

/// Os 30 tickers do Dow Jones Industrial Average (composição de 2025)
let dowJonesTickers = [|
    "AAPL"; "AMGN"; "AXP";  "BA";   "CAT";  "CRM";  "CSCO"; "CVX";  "DIS";  "DOW"
    "GS";   "HD";   "HON";  "IBM";  "INTC"; "JNJ";  "JPM";  "KO";   "MCD";  "MMM"
    "MRK";  "MSFT"; "NKE";  "PG";   "TRV";  "UNH";  "V";    "VZ";   "WBA";  "WMT"
|]

/// Carrega matriz de retornos de um CSV.
/// Formato esperado: primeira linha = header com tickers; primeira coluna = data; demais = retornos diários.
let loadReturnsFromCsv (path: string) : string[] * ReturnsMatrix =
    let lines = File.ReadAllLines(path)
    if lines.Length < 2 then
        failwithf "CSV %s tem menos de 2 linhas" path
    let header = lines.[0].Split(',') |> Array.skip 1
    let data =
        lines
        |> Array.skip 1
        |> Array.map (fun line ->
            line.Split(',')
            |> Array.skip 1
            |> Array.map (fun s -> Double.Parse(s, System.Globalization.CultureInfo.InvariantCulture)))
    header, data

/// Gera retornos sintéticos plausíveis para o segundo semestre de 2025 (~126 pregões).
/// Cada ação tem seu próprio drift anual e volatilidade anual; preços seguem movimento
/// browniano geométrico em log-retornos, com um fator de mercado comum (correlação).
let generateSyntheticReturns (tickers: string[]) (numDays: int) (seed: int) : ReturnsMatrix =
    let rng = Random(seed)
    let n = tickers.Length

    // Drifts anuais (retornos esperados) e volatilidades anuais por ação - valores plausíveis
    let annualDrifts =
        tickers |> Array.mapi (fun i _ ->
            // entre -5% e +25% ao ano
            -0.05 + (float i / float n) * 0.30 + (rng.NextDouble() - 0.5) * 0.10)
    let annualVols =
        tickers |> Array.mapi (fun i _ ->
            // entre 15% e 40% ao ano
            0.15 + (float i / float n) * 0.15 + rng.NextDouble() * 0.10)

    // Beta de cada ação em relação ao "mercado" (fator comum) -> gera correlação realista
    let betas = Array.init n (fun _ -> 0.6 + rng.NextDouble() * 0.8)  // 0.6 a 1.4

    // Vol diária do fator de mercado
    let dailyMarketVol = 0.012

    // Box-Muller para amostrar Normal(0,1)
    let nextGaussian () =
        let u1 = max 1e-12 (rng.NextDouble())
        let u2 = rng.NextDouble()
        sqrt(-2.0 * log u1) * cos(2.0 * Math.PI * u2)

    Array.init numDays (fun _ ->
        let marketShock = nextGaussian() * dailyMarketVol
        Array.init n (fun i ->
            let dailyDrift = annualDrifts.[i] / 252.0
            let dailyVol = annualVols.[i] / sqrt 252.0
            let idioShock = nextGaussian() * dailyVol
            dailyDrift + betas.[i] * marketShock + idioShock * 0.7))

/// Salva a matriz de retornos em CSV (para inspeção).
let saveReturnsToCsv (path: string) (tickers: string[]) (returns: ReturnsMatrix) : unit =
    use sw = new StreamWriter(path)
    sw.WriteLine("date," + String.concat "," tickers)
    returns |> Array.iteri (fun dayIdx row ->
        let cells = row |> Array.map (fun v -> v.ToString("F8", System.Globalization.CultureInfo.InvariantCulture))
        sw.WriteLine(sprintf "D%d,%s" dayIdx (String.concat "," cells)))
