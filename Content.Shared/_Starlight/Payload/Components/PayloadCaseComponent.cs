// ReSharper disable CheckNamespace
namespace Content.Shared.Payload.Components;

public sealed partial class PayloadCaseComponent
{
    /// <summary>
    /// Whether examining says if a payload is fitted. Off for cases that only hold a trigger.
    /// </summary>
    [DataField]
    public bool ExaminePayload = true;
}
