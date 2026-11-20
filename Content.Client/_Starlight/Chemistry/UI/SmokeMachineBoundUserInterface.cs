using Content.Shared._Starlight.Chemistry;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Chemistry.UI;

[UsedImplicitly]
public sealed class SmokeMachineBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private SmokeMachineWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<SmokeMachineWindow>();
        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
        _window.OnToggle += () => SendMessage(new SmokeMachineToggleMessage());
        _window.OnSetting += setting => SendMessage(new SmokeMachineSetSettingMessage(setting));
        _window.OnPurge += () => SendMessage(new SmokeMachinePurgeMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is SmokeMachineBoundUserInterfaceState cast)
            _window?.UpdateState(cast);
    }
}
