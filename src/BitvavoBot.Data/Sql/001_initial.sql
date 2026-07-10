CREATE TABLE IF NOT EXISTS BotProfiles (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE,
    TradeAmountMode INTEGER NOT NULL,
    TradeAmountValue TEXT NOT NULL,
    Markets TEXT NOT NULL,
    ScrapeIntervalSeconds INTEGER NOT NULL,
    BuyMarginPercentage TEXT NOT NULL,
    SellMarginPercentage TEXT NOT NULL,
    MinOpenOrdersPercentage TEXT NOT NULL,
    AutoReplenishOpenOrders INTEGER NOT NULL,
    MaxTradesPerDay INTEGER NOT NULL,
    MaxLossPerDay TEXT NOT NULL,
    MaxInvestment TEXT NOT NULL,
    StopLossPercentage TEXT NOT NULL,
    TakeProfitPercentage TEXT NOT NULL,
    MaxConcurrentOrders INTEGER NOT NULL,
    WebSocketFallbackSeconds INTEGER NOT NULL,
    Mode INTEGER NOT NULL,
    PapertradingProfileName TEXT NULL,
    State INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS StrategyAssignments (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    BotProfileId INTEGER NOT NULL REFERENCES BotProfiles(Id) ON DELETE CASCADE,
    Market TEXT NOT NULL,
    StrategyType INTEGER NOT NULL,
    ParametersJson TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_StrategyAssignments_BotProfileId ON StrategyAssignments(BotProfileId);

CREATE TABLE IF NOT EXISTS PapertradingProfiles (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE,
    QuoteCurrency TEXT NOT NULL,
    StartingBalance TEXT NOT NULL,
    CurrentBalance TEXT NOT NULL,
    SlippagePercentage TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    ResetAt TEXT NULL
);

CREATE TABLE IF NOT EXISTS Orders (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ExternalId TEXT NOT NULL,
    Mode INTEGER NOT NULL,
    Market TEXT NOT NULL,
    Side INTEGER NOT NULL,
    Type INTEGER NOT NULL,
    Status INTEGER NOT NULL,
    Price TEXT NULL,
    Amount TEXT NOT NULL,
    FilledAmount TEXT NOT NULL,
    AverageFillPrice TEXT NULL,
    FeePaid TEXT NOT NULL,
    FeeCurrency TEXT NOT NULL,
    BotProfileName TEXT NOT NULL,
    StrategyName TEXT NULL,
    PapertradingProfileName TEXT NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    FilledAt TEXT NULL,
    CancelledAt TEXT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS IX_Orders_Mode_ExternalId ON Orders(Mode, ExternalId);
CREATE INDEX IF NOT EXISTS IX_Orders_Mode_Status ON Orders(Mode, Status);
CREATE INDEX IF NOT EXISTS IX_Orders_Mode_BotProfileName ON Orders(Mode, BotProfileName);

CREATE TABLE IF NOT EXISTS Trades (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    OrderId INTEGER NOT NULL REFERENCES Orders(Id),
    Mode INTEGER NOT NULL,
    Market TEXT NOT NULL,
    Side INTEGER NOT NULL,
    Price TEXT NOT NULL,
    Amount TEXT NOT NULL,
    Fee TEXT NOT NULL,
    FeeCurrency TEXT NOT NULL,
    ProfitLoss TEXT NULL,
    Timestamp TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_Trades_Mode_Timestamp ON Trades(Mode, Timestamp);

CREATE TABLE IF NOT EXISTS Positions (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Mode INTEGER NOT NULL,
    Market TEXT NOT NULL,
    Asset TEXT NOT NULL,
    PapertradingProfileName TEXT NULL,
    Amount TEXT NOT NULL,
    AverageEntryPrice TEXT NOT NULL,
    Side INTEGER NULL,
    Leverage TEXT NULL,
    MarginType INTEGER NULL,
    LiquidationPrice TEXT NULL,
    OpenedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    ClosedAt TEXT NULL
);
CREATE INDEX IF NOT EXISTS IX_Positions_Mode_Asset ON Positions(Mode, Asset);
CREATE INDEX IF NOT EXISTS IX_Positions_PapertradingProfileName ON Positions(PapertradingProfileName);
CREATE INDEX IF NOT EXISTS IX_Orders_PapertradingProfileName ON Orders(PapertradingProfileName);

CREATE TABLE IF NOT EXISTS Candles (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Market TEXT NOT NULL,
    Timeframe TEXT NOT NULL,
    OpenTime TEXT NOT NULL,
    Open TEXT NOT NULL,
    High TEXT NOT NULL,
    Low TEXT NOT NULL,
    Close TEXT NOT NULL,
    Volume TEXT NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS IX_Candles_Market_Timeframe_OpenTime ON Candles(Market, Timeframe, OpenTime);

CREATE TABLE IF NOT EXISTS StrategyRuns (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Mode INTEGER NOT NULL,
    StrategyType INTEGER NOT NULL,
    StrategyName TEXT NOT NULL,
    BotProfileName TEXT NOT NULL,
    ParametersJson TEXT NOT NULL,
    Markets TEXT NOT NULL,
    StartedAt TEXT NOT NULL,
    EndedAt TEXT NULL,
    TotalTrades INTEGER NOT NULL,
    WinningTrades INTEGER NOT NULL,
    TotalProfitLoss TEXT NOT NULL,
    ReturnPercentage TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_StrategyRuns_Mode ON StrategyRuns(Mode);

CREATE TABLE IF NOT EXISTS LogEntries (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Timestamp TEXT NOT NULL,
    Type INTEGER NOT NULL,
    Message TEXT NOT NULL,
    Mode INTEGER NULL,
    Market TEXT NULL,
    Source TEXT NULL
);
CREATE INDEX IF NOT EXISTS IX_LogEntries_Timestamp ON LogEntries(Timestamp DESC);
