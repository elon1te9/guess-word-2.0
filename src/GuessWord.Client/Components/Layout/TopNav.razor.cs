namespace GuessWord.Client.Components.Layout;

public partial class TopNav
{
    protected override void OnInitialized() => UserState.OnChange += HandleUserStateChanged;

    private async void HandleUserStateChanged()
    {
        try { await InvokeAsync(StateHasChanged); }
        catch (Exception error) { await DispatchExceptionAsync(error); }
    }

    public void Dispose() => UserState.OnChange -= HandleUserStateChanged;
}
