namespace ClipTypr.NotifyIcon;

using System.Diagnostics.CodeAnalysis;

public sealed record MenuItem : IMenuItem
{
    private string text;
    private bool? isChecked;
    private bool isDisabled;
    private IReadOnlyList<IMenuItem>? subMenu;

    public required string Text
    {
        get => text;
        [MemberNotNull(nameof(text))]
        set
        {
            if (text == value) return;

            text = value;
            Changed?.Invoke(this, false);
        }
    }

    public bool? IsChecked
    {
        get => isChecked;
        set
        {
            if (isChecked == value) return;

            isChecked = value;
            Changed?.Invoke(this, false);
        }
    }

    public bool IsDisabled
    {
        get => isDisabled;
        set
        {
            if (isDisabled == value) return;

            isDisabled = value;
            Changed?.Invoke(this, false);
        }
    }

    public EventHandler<NotifyIcon>? Click { get; set; }

    public IReadOnlyList<IMenuItem>? SubMenu
    {
        get => subMenu;
        set
        {
            subMenu = value;
            Changed?.Invoke(this, true);
        }
    }

    internal event EventHandler<bool>? Changed;
}