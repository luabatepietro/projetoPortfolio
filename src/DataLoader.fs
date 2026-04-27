module PortfolioOptimization.DataLoader

open System
open System.IO
open PortfolioOptimization.Types

/// Os 30 tickers do Dow Jones Industrial Average (composição de 2025)
let dowJonesTickers = [|
    "AAPL"; "AMGN"; "AMZN"; "AXP";  "BA";   "CAT";  "CRM";  "CSCO"; "CVX";  "DIS"
    "DOW";  "GS";   "HD";   "HON";  "IBM";  "INTC"; "JNJ";  "JPM";  "KO";   "MCD"
    "MMM";  "MRK";  "MSFT"; "NKE";  "PG";   "TRV";  "UNH";  "V";    "VZ";   "WMT"
|]

/// Lê os CSVs reais do Yahoo Finance em data/raw/ e devolve a matriz de retornos simples.
/// Retorna float[][] com (N-1) linhas × 30 colunas, na ordem de dowJonesTickers.
let loadDowJonesReturns (dataDir: string) : ReturnsMatrix =
    let parsePrices (ticker: string) : Map<DateTime, float> =
        let path = Path.Combine(dataDir, ticker + ".csv")
        if not (File.Exists(path)) then
            failwithf "Arquivo não encontrado: %s" path
        File.ReadAllLines(path)
        |> Array.skip 1
        |> Array.choose (fun line ->
            let cols = line.Split(',')
            if cols.Length < 6 then None
            else
                let adjClose = cols.[5].Trim()
                if adjClose = "" || adjClose = "null" then None
                else
                    let date = DateTime.ParseExact(cols.[0].Trim(), "yyyy-MM-dd",
                                   System.Globalization.CultureInfo.InvariantCulture)
                    let price = Double.Parse(adjClose, System.Globalization.CultureInfo.InvariantCulture)
                    Some (date, price))
        |> Map.ofArray

    let priceMaps = dowJonesTickers |> Array.map parsePrices

    let commonDates =
        priceMaps
        |> Array.map (fun m -> m |> Map.toSeq |> Seq.map fst |> Set.ofSeq)
        |> Array.reduce Set.intersect
        |> Set.toArray
        |> Array.sort

    Array.init (commonDates.Length - 1) (fun i ->
        let t0 = commonDates.[i]
        let t1 = commonDates.[i + 1]
        dowJonesTickers |> Array.mapi (fun j _ ->
            priceMaps.[j].[t1] / priceMaps.[j].[t0] - 1.0))
