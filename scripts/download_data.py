"""
Script auxiliar usado UMA VEZ para baixar os 30 CSVs do Yahoo Finance.
Não faz parte do pipeline F# — é apenas um utilitário de coleta de dados.

Dependência: pip install yfinance pandas
Uso: python scripts/download_data.py
Saída: data/raw/{TICKER}.csv (formato: Date,Open,High,Low,Close,Adj Close,Volume)
"""

import yfinance as yf
import pandas as pd
import os

TICKERS = [
    "AAPL", "AMGN", "AMZN", "AXP",  "BA",   "CAT",  "CRM",  "CSCO", "CVX",  "DIS",
    "DOW",  "GS",   "HD",   "HON",  "IBM",  "INTC", "JNJ",  "JPM",  "KO",   "MCD",
    "MMM",  "MRK",  "MSFT", "NKE",  "PG",   "TRV",  "UNH",  "V",    "VZ",   "WMT",
]

START = "2025-07-01"
END   = "2026-01-01"
OUT   = "data/raw"

os.makedirs(OUT, exist_ok=True)

for ticker in TICKERS:
    df = yf.download(ticker, start=START, end=END, progress=False, auto_adjust=False)
    if df.empty:
        print(f"ERRO: {ticker} sem dados")
        continue
    df.columns = [col[0] if isinstance(col, tuple) else col for col in df.columns]
    df = df[["Open", "High", "Low", "Close", "Adj Close", "Volume"]]
    df.index.name = "Date"
    path = os.path.join(OUT, f"{ticker}.csv")
    df.to_csv(path)
    print(f"OK: {ticker} — {len(df)} pregões -> {path}")
