using Content.Shared._Starlight.Cargo.Exchange;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Cargo.Exchange;

/// <summary>
/// Opens the <see cref="GuildExchangeWindow"/> and passes its orders to the server.
/// </summary>
[UsedImplicitly]
public sealed class GuildExchangeBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private GuildExchangeWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<GuildExchangeWindow>();
        _window.OnTrade += (company, shares) => SendMessage(new GuildExchangeTradeMessage(company, shares));
        _window.OnSelectAccount += kind => SendMessage(new GuildExchangeSelectAccountMessage(kind));
        _window.OnWithdraw += () => SendMessage(new GuildExchangeWithdrawMessage());
        _window.OnFullBudget += enabled => SendMessage(new GuildExchangeFullBudgetMessage(enabled));

        if (State is GuildExchangeBuiState state)
            _window.UpdateMarket(state);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is GuildExchangeBuiState market)
            _window?.UpdateMarket(market);
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);
        if (message is GuildExchangeAccountMessage account)
            _window?.UpdateAccount(account);
    }
}
