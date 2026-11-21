using Content.Shared.Access;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Stacks;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Cargo.Exchange;

/// <summary>
/// A terminal on the Interstellar Trade Guild's exchange. Anyone can trade their own spesos through it; people with
/// its access can also trade part of the cargo budget, and the quartermaster can unlock all of it.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GuildExchangeComponent : Component
{
    /// <summary>
    /// The station account cargo trades come out of and go back into.
    /// </summary>
    [DataField]
    public ProtoId<CargoAccountPrototype> Account = "Cargo";

    /// <summary>
    /// The stack it takes and pays out personal cash in.
    /// </summary>
    [DataField]
    public ProtoId<StackPrototype> CashStack = "Credit";

    /// <summary>
    /// Access that can lift the cap and trade the whole cargo budget.
    /// </summary>
    [DataField]
    public ProtoId<AccessLevelPrototype> FullBudgetAccess = "Quartermaster";
}

[Serializable, NetSerializable]
public enum GuildExchangeUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum ExchangeAccountKind : byte
{
    Personal,
    Cargo,
}

[Serializable, NetSerializable]
public enum ExchangeOrderSide : byte
{
    Buy,
    Sell,
    Short,
    Cover,
}

[Serializable, NetSerializable]
public enum ExchangeOrderType : byte
{
    Market,
    Limit,
}

/// <summary>
/// One tick of one company's trading.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct ExchangeCandle(float Open, float High, float Low, float Close, float Volume);

/// <summary>
/// One company's line on the board.
/// </summary>
[Serializable, NetSerializable]
public sealed class ExchangeCompanyState
{
    public string Id = string.Empty;
    public string Ticker = string.Empty;
    public string Name = string.Empty;
    public string Sector = string.Empty;
    public float Price;
    public float Open;
    public float Bid;
    public float Ask;
    public float Spread;

    /// <summary>
    /// Shares traded last tick.
    /// </summary>
    public float Volume;

    /// <summary>
    /// Recent closing prices, oldest first.
    /// </summary>
    public List<float> Spark = new();
}

[Serializable, NetSerializable]
public enum ExchangeNewsKind : byte
{
    Company,
    Rumour,
    Warning,
    Outcome,
    Factor,
    Event,
    Earnings,
    Dividend,
    Orders,
}

/// <summary>
/// A wire story. <see cref="Time"/> is a game-time instant, shown as station time.
/// </summary>
[Serializable, NetSerializable]
public sealed class ExchangeNewsState(TimeSpan time, ExchangeNewsKind kind, string? ticker, string text)
{
    public TimeSpan Time = time;
    public ExchangeNewsKind Kind = kind;
    public string? Ticker = ticker;
    public string Text = text;
}

[Serializable, NetSerializable]
public sealed class ExchangeTradeLogState(TimeSpan time, string text)
{
    public TimeSpan Time = time;
    public string Text = text;
}

/// <summary>
/// An earnings report or dividend date on the calendar.
/// </summary>
[Serializable, NetSerializable]
public sealed class ExchangeCalendarState(string ticker, TimeSpan time, bool dividend, float consensus, float? actual, float amount)
{
    public string Ticker = ticker;
    public TimeSpan Time = time;
    public bool Dividend = dividend;
    public float Consensus = consensus;
    public float? Actual = actual;
    public float Amount = amount;
}

/// <summary>
/// The market, shared by everyone looking at the terminal.
/// </summary>
[Serializable, NetSerializable]
public sealed class GuildExchangeBuiState : BoundUserInterfaceState
{
    public List<ExchangeCompanyState> Companies = new();
    public List<ExchangeNewsState> News = new();
    public List<ExchangeTradeLogState> Trades = new();
    public List<ExchangeCalendarState> Calendar = new();
    public float Fee;
    public float BorrowFee;
    public float ShortMargin;
    public float MaintenanceMargin;
    public int MaxOrders;
}

[Serializable, NetSerializable]
public sealed class ExchangePositionState(string company, int shares, long cost)
{
    public string Company = company;
    public int Shares = shares;
    public long Cost = cost;
}

[Serializable, NetSerializable]
public sealed class ExchangeShortState(string company, int shares, long collateral, long margin, long proceeds, long borrowPaid, float health)
{
    public string Company = company;
    public int Shares = shares;
    public long Collateral = collateral;
    public long Margin = margin;
    public long Proceeds = proceeds;
    public long BorrowPaid = borrowPaid;

    /// <summary>
    /// Collateral over the cost of covering now.
    /// </summary>
    public float Health = health;
}

[Serializable, NetSerializable]
public sealed class ExchangeOrderState(int id, string company, bool buy, int shares, float limit)
{
    public int Id = id;
    public string Company = company;
    public bool Buy = buy;
    public int Shares = shares;
    public float Limit = limit;
}

/// <summary>
/// The account one viewer is trading with, sent only to them.
/// </summary>
[Serializable, NetSerializable]
public sealed class GuildExchangeAccountMessage : BoundUserInterfaceMessage
{
    public ExchangeAccountKind Kind;
    public bool HasId;
    public bool CanTradeCargo;

    /// <summary>
    /// Spesos the account can spend: deposited cash, or the cargo budget.
    /// </summary>
    public long Cash;

    /// <summary>
    /// How much more of the cargo budget may go into shares and shorts, for the cargo account.
    /// </summary>
    public long? CapLeft;

    public List<ExchangePositionState> Longs = new();
    public List<ExchangeShortState> Shorts = new();
    public List<ExchangeOrderState> Orders = new();

    public long Realised;
    public long Unrealised;
    public string? Notice;

    /// <summary>
    /// What this viewer's last order did, if it went through.
    /// </summary>
    public string? Confirmation;

    public bool CanUnlockFullBudget;

    /// <summary>
    /// Whether this viewer has the whole cargo budget unlocked.
    /// </summary>
    public bool FullBudget;
}

/// <summary>
/// Candles for the company a viewer is looking at. The last candle closed at <see cref="LastTime"/>.
/// </summary>
[Serializable, NetSerializable]
public sealed class GuildExchangeChartMessage(string company, List<ExchangeCandle> candles, TimeSpan lastTime, TimeSpan interval)
    : BoundUserInterfaceMessage
{
    public string Company = company;
    public List<ExchangeCandle> Candles = candles;
    public TimeSpan LastTime = lastTime;
    public TimeSpan Interval = interval;
}

[Serializable, NetSerializable]
public sealed class GuildExchangeSelectAccountMessage(ExchangeAccountKind kind) : BoundUserInterfaceMessage
{
    public ExchangeAccountKind Kind = kind;
}

[Serializable, NetSerializable]
public sealed class GuildExchangeSelectCompanyMessage(string company) : BoundUserInterfaceMessage
{
    public string Company = company;
}

/// <summary>
/// A market order fills now; a limit order (buy or sell only) rests until the price reaches it.
/// </summary>
[Serializable, NetSerializable]
public sealed class GuildExchangeOrderMessage(string company, ExchangeOrderSide side, ExchangeOrderType type, int shares, float limit)
    : BoundUserInterfaceMessage
{
    public string Company = company;
    public ExchangeOrderSide Side = side;
    public ExchangeOrderType Type = type;
    public int Shares = shares;
    public float Limit = limit;
}

[Serializable, NetSerializable]
public sealed class GuildExchangeCancelOrderMessage(int id) : BoundUserInterfaceMessage
{
    public int Id = id;
}

/// <summary>
/// Unlock (or lock again) the whole cargo budget. Needs quartermaster access.
/// </summary>
[Serializable, NetSerializable]
public sealed class GuildExchangeFullBudgetMessage(bool enabled) : BoundUserInterfaceMessage
{
    public bool Enabled = enabled;
}

/// <summary>
/// Pay out the personal account's cash.
/// </summary>
[Serializable, NetSerializable]
public sealed class GuildExchangeWithdrawMessage : BoundUserInterfaceMessage;
