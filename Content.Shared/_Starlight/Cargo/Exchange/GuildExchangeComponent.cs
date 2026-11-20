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

    /// <summary>
    /// Who has the whole budget unlocked right now. It locks again when they toggle it off, close the window, or
    /// someone else trades the cargo account.
    /// </summary>
    [ViewVariables]
    public EntityUid? FullBudgetBy;
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

/// <summary>
/// One company's line on the board.
/// </summary>
[Serializable, NetSerializable]
public sealed class ExchangeCompanyState(string id, string ticker, string name, string description, string sector,
    float price, float open, float liquidity, List<float> history)
{
    public string Id = id;
    public string Ticker = ticker;
    public string Name = name;
    public string Description = description;
    public string Sector = sector;
    public float Price = price;
    public float Open = open;
    public float Liquidity = liquidity;
    public List<float> History = history;
}

[Serializable, NetSerializable]
public sealed class ExchangeNewsState(TimeSpan time, string? ticker, string text, bool unconfirmed)
{
    public TimeSpan Time = time;
    public string? Ticker = ticker;
    public string Text = text;
    public bool Unconfirmed = unconfirmed;
}

/// <summary>
/// The market, shared by everyone looking at the terminal.
/// </summary>
[Serializable, NetSerializable]
public sealed class GuildExchangeBuiState(List<ExchangeCompanyState> companies, List<ExchangeNewsState> news,
    List<string> trades, float fee, float spread, bool halted) : BoundUserInterfaceState
{
    public List<ExchangeCompanyState> Companies = companies;
    public List<ExchangeNewsState> News = news;
    public List<string> Trades = trades;
    public float Fee = fee;
    public float Spread = spread;
    public bool Halted = halted;
}

[Serializable, NetSerializable]
public sealed class ExchangeHoldingState(string company, int shares, int cost)
{
    public string Company = company;
    public int Shares = shares;
    public int Cost = cost;
}

/// <summary>
/// The account one viewer is trading with, sent only to them.
/// </summary>
[Serializable, NetSerializable]
public sealed class GuildExchangeAccountMessage(ExchangeAccountKind kind, bool hasId, bool canTradeCargo, int cash,
    int? capLeft, List<ExchangeHoldingState> holdings, int profit, string? notice, bool canUnlockFullBudget, bool fullBudget)
    : BoundUserInterfaceMessage
{
    public ExchangeAccountKind Kind = kind;
    public bool HasId = hasId;
    public bool CanTradeCargo = canTradeCargo;

    /// <summary>
    /// Spesos the account can spend: deposited cash, or the cargo budget.
    /// </summary>
    public int Cash = cash;

    /// <summary>
    /// How much more of the cargo budget may go into shares, for the cargo account.
    /// </summary>
    public int? CapLeft = capLeft;

    public List<ExchangeHoldingState> Holdings = holdings;

    /// <summary>
    /// Proceeds minus costs so far, plus what the shares held are worth now.
    /// </summary>
    public int Profit = profit;

    public string? Notice = notice;

    public bool CanUnlockFullBudget = canUnlockFullBudget;

    /// <summary>
    /// Whether this viewer has the whole cargo budget unlocked.
    /// </summary>
    public bool FullBudget = fullBudget;
}

[Serializable, NetSerializable]
public sealed class GuildExchangeSelectAccountMessage(ExchangeAccountKind kind) : BoundUserInterfaceMessage
{
    public ExchangeAccountKind Kind = kind;
}

/// <summary>
/// Buy (positive) or sell (negative) shares of a company.
/// </summary>
[Serializable, NetSerializable]
public sealed class GuildExchangeTradeMessage(string company, int shares) : BoundUserInterfaceMessage
{
    public string Company = company;
    public int Shares = shares;
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
